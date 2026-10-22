// §5.9 canonical emitter. Mirrors rust/src/document/render/canonical/{walk.rs,
// strings.rs,num.rs} + render/helpers.rs. Key order is NEVER sorted (§5.9);
// emission is a single pass over the value tree with an explicit work stack
// (no host recursion on externally controlled depth). All §5.9.0 rejections
// surface as KtavException with the same envelope as the native writer
// (UnrepresentableAt / reason / path / §5.9.0).

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureCanonical
{
    /// <summary>Emit the §5.9 canonical form of a KtavObject/KtavArray root.</summary>
    public static string Emit(KtavValue value)
    {
        var stack = new Stack(EstimateSize(value));
        switch (value)
        {
            case KtavObject o:
                stack.PushPairs(o, 0, isRoot: true);
                break;
            case KtavArray arr when arr.Items.Count == 0:
                stack.Out.Append("[]\n"); // §5.9.3 empty Array root
                return stack.Out.ToString();
            case KtavArray arr:
                EmitArrayRoot(arr, stack);
                break;
            default:
                throw PureRender.WriterError("ScalarRoot", new List<string>());
        }
        stack.Run();
        return stack.Out.ToString();
    }

    private static void EmitArrayRoot(KtavArray arr, Stack stack)
    {
        bool wrap = arr.Items.Count > 0 && FirstItemNeedsWrap(arr.Items[0]);
        if (wrap)
        {
            stack.Out.Append("[\n"); // first content line is `[` itself (§5.9.3)
            stack.Push(new Task(Task.KClose, 0, ']'));
            stack.Push(new Task(Task.KItems, arr, 0, 1, rootFirst: false));
        }
        else
        {
            stack.Push(new Task(Task.KItems, arr, 0, 0, rootFirst: true));
        }
    }

    private static bool FirstItemNeedsWrap(KtavValue item) => item is KtavObject or KtavArray;

    // ------------------------------------------------------------------
    // Explicit-stack walk
    // ------------------------------------------------------------------

    private sealed class Stack
    {
        public readonly StringBuilder Out;
        private readonly List<Task> _tasks = new();
        private readonly List<string> _path = new();

        public Stack(int capacity) { Out = new StringBuilder(capacity); }

        public void PushPairs(KtavObject obj, int indent, bool isRoot)
            => _tasks.Add(new Task(Task.KPairs, obj, 0, indent, isRoot));

        public void Push(in Task t) => _tasks.Add(t);

        public void Run()
        {
            while (_tasks.Count > 0)
            {
                var t = _tasks[_tasks.Count - 1];
                _tasks.RemoveAt(_tasks.Count - 1);
                switch (t.Code)
                {
                    case Task.KPathPush:
                        _path.Add(t.Key!);
                        break;
                    case Task.KPathPop:
                        _path.RemoveAt(_path.Count - 1);
                        break;
                    case Task.KClose:
                        PureRender.PushIndent(Out, t.Indent);
                        Out.Append(t.Closer).Append('\n');
                        break;
                    case Task.KPairs:
                    {
                        if (t.Index >= t.Obj!.Entries.Count) break;
                        var e = t.Obj.Entries[t.Index];
                        bool rootFirstKey = t.IsRoot && t.Index == 0;
                        _tasks.Add(new Task(Task.KPairs, t.Obj, t.Index + 1, t.Indent, t.IsRoot));
                        EmitPair(e.Key, e.Value, t.Indent, rootFirstKey);
                        break;
                    }
                    case Task.KItems:
                    {
                        if (t.Index >= t.Arr!.Items.Count) break;
                        var item = t.Arr.Items[t.Index];
                        bool rootFirstItem = t.RootFirst && t.Index == 0;
                        _tasks.Add(new Task(Task.KItems, t.Arr, t.Index + 1, t.Indent, t.RootFirst));
                        EmitArrayItem(item, t.Indent, rootFirstItem);
                        break;
                    }
                }
            }
        }

        private void EmitPair(string key, KtavValue value, int indent, bool rootFirstKey)
        {
            if (key.Length == 0)
                throw PureRender.WriterError("EmptyKeyName", _path);
            PureRender.PushIndent(Out, indent);
            PureRender.PushEscapedKeySegment(key, rootFirstKey, Out);
            switch (value)
            {
                case KtavNull:
                    Out.Append(": null\n");
                    break;
                case KtavBool b:
                    Out.Append(": ").Append(b.Value ? "true" : "false").Append('\n');
                    break;
                case KtavInteger i:
                    Out.Append(": ").Append(i.Text).Append('\n'); // §5.9.8 base-10
                    break;
                case KtavFloat f:
                    Out.Append(": ").Append(PureRender.CanonicalFloatChecked(f.Text, _path)).Append('\n');
                    break;
                case KtavString s:
                    EmitStringInPair(s.Value, indent);
                    break;
                case KtavArray arr:
                    if (arr.Items.Count == 0) { Out.Append(": []\n"); break; }
                    Out.Append(": [\n");
                    // LIFO exec order: PathPush (enter key), children, Close, PathPop.
                    _tasks.Add(new Task(Task.KPathPop, ""));
                    _tasks.Add(new Task(Task.KClose, indent, ']'));
                    _tasks.Add(new Task(Task.KItems, arr, 0, indent + 1, rootFirst: false));
                    _tasks.Add(new Task(Task.KPathPush, key));
                    break;
                case KtavObject obj:
                    if (obj.Entries.Count == 0) { Out.Append(": {}\n"); break; }
                    Out.Append(": {\n");
                    _tasks.Add(new Task(Task.KPathPop, ""));
                    _tasks.Add(new Task(Task.KClose, indent, '}'));
                    _tasks.Add(new Task(Task.KPairs, obj, 0, indent + 1, isRoot: false));
                    _tasks.Add(new Task(Task.KPathPush, key));
                    break;
            }
        }

        private void EmitArrayItem(KtavValue value, int indent, bool rootFirstItem)
        {
            PureRender.PushIndent(Out, indent);
            switch (value)
            {
                case KtavNull: Out.Append("null\n"); break;
                case KtavBool b: Out.Append(b.Value ? "true" : "false").Append('\n'); break;
                case KtavInteger i: Out.Append(i.Text).Append('\n'); break;
                case KtavFloat f:
                    Out.Append(PureRender.CanonicalFloatChecked(f.Text, _path)).Append('\n');
                    break;
                case KtavString s: EmitStringAsItem(s.Value, indent, rootFirstItem); break;
                case KtavArray arr:
                    if (arr.Items.Count == 0) { Out.Append("[]\n"); break; }
                    Out.Append("[\n");
                    _tasks.Add(new Task(Task.KClose, indent, ']'));
                    _tasks.Add(new Task(Task.KItems, arr, 0, indent + 1, rootFirst: false));
                    break;
                case KtavObject obj:
                    if (obj.Entries.Count == 0) { Out.Append("{}\n"); break; }
                    Out.Append("{\n");
                    _tasks.Add(new Task(Task.KClose, indent, '}'));
                    _tasks.Add(new Task(Task.KPairs, obj, 0, indent + 1, isRoot: false));
                    break;
            }
        }

        /// <summary>canonical/strings.rs emit_string_in_pair.</summary>
        private void EmitStringInPair(string s, int indent)
        {
            if (s.Length == 0) { Out.Append(":\n"); return; } // §5.9.7
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: false, _path);
                Out.Append(form == PureRender.MlVerbatim ? ": ((\n" : ": (\n");
                Out.Append(s).Append('\n'); // body at indent 0 (§5.9.6)
                PureRender.PushIndent(Out, indent);
                Out.Append(form == PureRender.MlVerbatim ? "))\n" : ")\n");
                return;
            }
            if (PureRender.NeedsRawMarker(s)) Out.Append(":: ");
            else Out.Append(": ");
            Out.Append(s).Append('\n');
        }

        /// <summary>canonical/strings.rs emit_string_as_item.</summary>
        private void EmitStringAsItem(string s, int indent, bool isRootArrayFirst)
        {
            if (s.Length == 0) { Out.Append("::\n"); return; }
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: false, _path);
                Out.Append(form == PureRender.MlVerbatim ? "((\n" : "(\n");
                Out.Append(s).Append('\n');
                PureRender.PushIndent(Out, indent);
                Out.Append(form == PureRender.MlVerbatim ? "))\n" : ")\n");
                return;
            }
            if (PureRender.ItemNeedsRawMarker(s))
            {
                Out.Append(":: ");
            }
            else if (isRootArrayFirst)
            {
                // §5.9.6/§5.9.12 first-item safeguards: pair-candidate probe (on
                // the body's UTF-8 bytes) and the U+FEFF byte-0 collision.
                var bytes = Encoding.UTF8.GetBytes(s);
                if (PureClassify.IsPairShape(bytes, 0, bytes.Length)
                    || (s.Length > 0 && s[0] == '\uFEFF'))
                {
                    Out.Append(":: ");
                }
            }
            Out.Append(s).Append('\n');
        }
    }

    private readonly struct Task
    {
        public const int KPathPush = 0, KPathPop = 1, KClose = 2, KPairs = 3, KItems = 4;

        public readonly int Code;
        public readonly int Index, Indent;
        public readonly bool IsRoot, RootFirst;
        public readonly char Closer;
        public readonly string? Key;
        public readonly KtavObject? Obj;
        public readonly KtavArray? Arr;

        public Task(int code, string key)
        { Code = code; Key = key; Index = 0; Indent = 0; IsRoot = false; RootFirst = false; Closer = '\0'; }

        public Task(int code, int indent, char closer)
        { Code = code; Indent = indent; Closer = closer; Index = 0; IsRoot = false; RootFirst = false; Key = null; }

        public Task(int code, KtavObject obj, int index, int indent, bool isRoot)
        { Code = code; Obj = obj; Index = index; Indent = indent; IsRoot = isRoot; RootFirst = false; Closer = '\0'; Key = null; }

        public Task(int code, KtavArray arr, int index, int indent, bool rootFirst)
        { Code = code; Arr = arr; Index = index; Indent = indent; RootFirst = rootFirst; IsRoot = false; Closer = '\0'; Key = null; }
    }

    /// <summary>Rough pre-size (walk.rs estimate_size): key/text bytes + line
    /// overhead. Iterative — deep trees must not overflow the host stack here
    /// either.</summary>
    private static int EstimateSize(KtavValue root)
    {
        int total = 0;
        var work = new List<KtavValue> { root };
        while (work.Count > 0)
        {
            var v = work[work.Count - 1];
            work.RemoveAt(work.Count - 1);
            switch (v)
            {
                case KtavNull: total += 5; break;
                case KtavBool: total += 6; break;
                case KtavInteger i: total += i.Text.Length + 8; break;
                case KtavFloat f: total += f.Text.Length + 8; break;
                case KtavString s: total += s.Value.Length + 8; break;
                case KtavArray a:
                    total += 4;
                    foreach (var it in a.Items) work.Add(it);
                    break;
                case KtavObject o:
                    total += 4;
                    foreach (var e in o.Entries) { total += e.Key.Length + 4; work.Add(e.Value); }
                    break;
            }
        }
        return total;
    }
}
