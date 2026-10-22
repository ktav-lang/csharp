// Pretty writer (Dumps) + force-strings writer (DumpsForceStrings). Mirrors
// rust/src/document/render/emitters/{render,pair,array_item,object}.rs and the
// §5.9.0 walk in render/representable.rs: rejections are detected inline in
// document order, the key path is threaded with explicit stack tasks (no host
// recursion on externally controlled depth), and a rejection discards the local
// buffer — no partial output.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ktav.Pure;

internal static class PureWriter
{
    /// <summary>render.rs render: pretty render of an Object/Array root.</summary>
    public static string Dumps(KtavValue value)
    {
        PrecheckRoot(value);
        var w = new Walker(EstimateSize(value));
        w.Root(value);
        w.Run();
        return w.Out.ToString();
    }

    /// <summary>render.rs to_string_force_strings: every leaf scalar coerced to
    /// its textual form via the raw-string `::` spelling; compounds preserved.
    /// A coerced Float cannot trip NonFiniteFloat (render.rs) — the host-side
    /// ScalarRoot check is the caller's job, EmptyKeyName/CR/collisions fire here.</summary>
    public static string DumpsForceStrings(KtavValue value)
    {
        if (value is not (KtavObject or KtavArray))
            throw KtavException.WriterError("ScalarRoot", Array.Empty<string>());
        var w = new Walker(EstimateSize(value)) { ForceStrings = true };
        w.Root(value);
        w.Run();
        return w.Out.ToString();
    }

    private static void PrecheckRoot(KtavValue value)
    {
        if (value is not (KtavObject or KtavArray))
            throw KtavException.WriterError("ScalarRoot", Array.Empty<string>());
    }

    private sealed class Walker
    {
        public readonly StringBuilder Out;
        public bool ForceStrings;
        private readonly List<Task> _tasks = new();
        private readonly List<string> _path = new();

        public Walker(int capacity) { Out = new StringBuilder(capacity); }

        public void Root(KtavValue value)
        {
            switch (value)
            {
                case KtavObject o:
                    _tasks.Add(new Task(Task.KPairs, o, 0, 0, isRoot: true));
                    break;
                case KtavArray arr when arr.Items.Count == 0:
                    Out.Append("[]\n"); // empty Array root must stay explicit
                    break;
                case KtavArray arr:
                    if (arr.Items[0] is KtavObject or KtavArray)
                    {
                        Out.Append("[\n");
                        _tasks.Add(new Task(Task.KClose, 1, ']'));
                        _tasks.Add(new Task(Task.KItems, arr, 0, 1, rootFirst: false));
                    }
                    else
                    {
                        _tasks.Add(new Task(Task.KItems, arr, 0, 0, rootFirst: true));
                    }
                    break;
            }
        }

        public void Run()
        {
            while (_tasks.Count > 0)
            {
                var t = _tasks[_tasks.Count - 1];
                _tasks.RemoveAt(_tasks.Count - 1);
                switch (t.Code)
                {
                    case Task.KPathPush: _path.Add(t.Key!); break;
                    case Task.KPathPop: _path.RemoveAt(_path.Count - 1); break;
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
            if (ForceStrings && ForcedStringText(value) is string text)
            {
                EmitStringInPair(text, indent);
                return;
            }
            switch (value)
            {
                case KtavNull: Out.Append(": null\n"); break;
                case KtavBool b: Out.Append(": ").Append(b.Value ? "true" : "false").Append('\n'); break;
                case KtavInteger i: Out.Append(": ").Append(i.Text).Append('\n'); break;
                case KtavFloat f:
                    PureRender.RejectNonFinite(f.Text, _path);
                    Out.Append(": ").Append(f.Text).Append('\n');
                    break;
                case KtavString s: EmitStringInPair(s.Value, indent); break;
                case KtavArray arr:
                    if (arr.Items.Count == 0) { Out.Append(": []\n"); break; }
                    Out.Append(": [\n");
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
            if (ForceStrings && ForcedStringText(value) is string text)
            {
                EmitStringAsItem(text, indent, rootFirstItem);
                return;
            }
            switch (value)
            {
                case KtavNull: Out.Append("null\n"); break;
                case KtavBool b: Out.Append(b.Value ? "true" : "false").Append('\n'); break;
                case KtavInteger i: Out.Append(i.Text).Append('\n'); break;
                case KtavFloat f:
                    PureRender.RejectNonFinite(f.Text, _path);
                    Out.Append(f.Text).Append('\n');
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

        /// <summary>pair.rs render_string_in_pair (prefer stripped multi-line).</summary>
        private void EmitStringInPair(string s, int indent)
        {
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: true, _path);
                if (form == PureRender.MlStripped)
                {
                    Out.Append(": (\n");
                    int contentIndent = indent + 1;
                    EmitStrippedBody(s, contentIndent, indent);
                }
                else
                {
                    Out.Append(": ((\n");
                    Out.Append(s).Append('\n');
                    PureRender.PushIndent(Out, indent);
                    Out.Append("))\n");
                }
                return;
            }
            Out.Append(PureRender.NeedsRawMarker(s) ? ":: " : ": ");
            Out.Append(s).Append('\n');
        }

        /// <summary>array_item.rs render_string_as_item.</summary>
        private void EmitStringAsItem(string s, int indent, bool isRootArrayFirst)
        {
            if (PureRender.ContainsCr(s))
                throw PureRender.WriterError("CRByte", _path);
            if (PureRender.StringNeedsMultiline(s))
            {
                int form = PureRender.ChooseMultilineForm(s, preferStripped: true, _path);
                if (form == PureRender.MlStripped)
                {
                    Out.Append("(\n");
                    EmitStrippedBody(s, indent + 1, indent);
                }
                else
                {
                    Out.Append("((\n");
                    Out.Append(s).Append('\n');
                    PureRender.PushIndent(Out, indent);
                    Out.Append("))\n");
                }
                return;
            }
            if (s.Length == 0)
            {
                // An empty item would be a bare blank line — force `::`.
                Out.Append("::\n");
                return;
            }
            if (PureRender.ItemNeedsRawMarker(s))
            {
                Out.Append(":: ");
            }
            else if (isRootArrayFirst)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                if (PureClassify.IsPairShape(bytes, 0, bytes.Length)
                    || (s.Length > 0 && s[0] == '\uFEFF'))
                {
                    Out.Append(":: ");
                }
            }
            Out.Append(s).Append('\n');
        }

        /// <summary>Stripped body: content lines at contentIndent, empty lines stay
        /// empty (pair.rs / array_item.rs stripped arm).</summary>
        private void EmitStrippedBody(string s, int contentIndent, int indent)
        {
            int start = 0;
            while (true)
            {
                int nl = s.IndexOf('\n', start);
                int end = nl < 0 ? s.Length : nl;
                if (end > start)
                {
                    PureRender.PushIndent(Out, contentIndent);
                    Out.Append(s, start, end - start);
                }
                Out.Append('\n');
                if (nl < 0) break;
                start = nl + 1;
            }
            PureRender.PushIndent(Out, indent);
            Out.Append(")\n");
        }
    }

    /// <summary>
    /// Explicit walker task (render.rs Task enum), private nested inside
    /// Walker like PureCanonical/PureFormatter. All members are assigned in
    /// every constructor; unused members keep their zero values.
    /// </summary>
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

    /// <summary>render.rs forced_string_text: null for compounds.</summary>
    private static string? ForcedStringText(KtavValue v) => v switch
    {
        KtavNull => "null",
        KtavBool b => b.Value ? "true" : "false",
        KtavInteger i => i.Text,
        KtavFloat f => f.Text,
        KtavString s => s.Value,
        _ => null,
    };

    /// <summary>Rough pre-size (render.rs estimate_size). Iterative — deep
    /// trees must not overflow the host stack here either.</summary>
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
                case KtavInteger i: total += i.Text.Length + 4; break;
                case KtavFloat f: total += f.Text.Length + 4; break;
                case KtavString s: total += s.Value.Length + 4; break;
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
