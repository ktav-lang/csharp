// Mutable object under construction (§6.3 insert tables) and its iterative
// materialisation. Split out of PureInline to keep every file under the
// 1000-line budget.

using System;
using System.Collections.Generic;

namespace Ktav.Pure;

/// <summary>Mutable object under construction (§6.3 insert tables operate on this).</summary>
internal sealed class PureObjBuilder
{
    public readonly List<KeyValuePair<string, object?>> Entries = new();
    private readonly Dictionary<string, int> _index = new();

    /// <summary>Value shapes for §6.3: inserted leaf or object to descend into.</summary>
    public readonly struct Slot
    {
        public readonly bool IsObject;
        public readonly string Label;   // kind_label
        public readonly object? Value;  // KtavValue leaf or PureObjBuilder

        public Slot(bool isObject, string label, object? value)
        {
            IsObject = isObject; Label = label; Value = value;
        }
    }

    public static Slot SlotOf(object? v)
    {
        return v switch
        {
            PureObjBuilder => new Slot(true, "object", v),
            KtavNull => new Slot(false, "null", v),
            KtavBool => new Slot(false, "bool", v),
            KtavInteger => new Slot(false, "integer", v),
            KtavFloat => new Slot(false, "float", v),
            KtavString => new Slot(false, "string", v),
            KtavArray => new Slot(false, "array", v),
            List<object?> => new Slot(false, "array", v),
            _ => new Slot(false, "string", v),
        };
    }

    /// <summary>insert_leaf: false when occupied (slot untouched).</summary>
    public bool InsertLeaf(string key, Slot slot)
    {
        if (_index.ContainsKey(key)) return false;
        _index[key] = Entries.Count;
        Entries.Add(new KeyValuePair<string, object?>(key, slot.Value));
        return true;
    }

    /// <summary>descend: null when a non-object value blocks the path (§6.3).</summary>
    public PureObjBuilder? Descend(string key)
    {
        if (_index.TryGetValue(key, out int idx))
            return Entries[idx].Value as PureObjBuilder;
        var sub = new PureObjBuilder();
        _index[key] = Entries.Count;
        Entries.Add(new KeyValuePair<string, object?>(key, sub));
        return sub;
    }

    /// <summary>Materialise once at finish — O(nodes). Iterative post-order:
    /// a 2,000-segment dotted key builds a 2,000-deep builder chain, and
    /// materialisation must not recurse over it (no host stack overflow, no
    /// new depth limits — dotted-key depth is unbounded per insert.rs).</summary>
    public KtavObject ToValue() => (KtavObject)Materialise(this);

    internal static KtavValue Materialise(object? root)
    {
        var rootSlot = new KtavValue[1];
        var frames = new List<MFrame>();
        PushMaterialise(frames, root, rootSlot, 0);
        while (frames.Count > 0)
        {
            var f = frames[frames.Count - 1];
            if (f.Next < f.Count)
            {
                int i = f.Next++;
                object? child = f.IsList ? f.Items![i] : f.Pairs![i].Value;
                if (!PushMaterialise(frames, child, f.Slots, i))
                    f.Slots[i] = (KtavValue)child!;
                continue;
            }
            KtavValue done;
            if (f.IsList)
            {
                var items = new List<KtavValue>(f.Count);
                for (int i = 0; i < f.Count; i++) items.Add(f.Slots[i]);
                done = new KtavArray(items);
            }
            else
            {
                var entries = new List<KeyValuePair<string, KtavValue>>(f.Count);
                for (int i = 0; i < f.Count; i++)
                    entries.Add(new KeyValuePair<string, KtavValue>(f.Pairs![i].Key, f.Slots[i]));
                done = new KtavObject(entries);
            }
            f.ParentSlots![f.ParentIndex] = done;
            frames.RemoveAt(frames.Count - 1);
        }
        return rootSlot[0];
    }

    /// <summary>Queue a materialise frame for a compound; false when v is a leaf.</summary>
    private static bool PushMaterialise(List<MFrame> frames, object? v,
        KtavValue[] parentSlots, int parentIndex)
    {
        if (v is PureObjBuilder b)
        {
            frames.Add(new MFrame(b.Entries, parentSlots, parentIndex));
            return true;
        }
        if (v is List<object?> list)
        {
            frames.Add(new MFrame(list, parentSlots, parentIndex));
            return true;
        }
        return false;
    }

    private sealed class MFrame
    {
        public readonly bool IsList;
        public readonly List<KeyValuePair<string, object?>>? Pairs;
        public readonly List<object?>? Items;
        public readonly KtavValue[] Slots;
        public readonly KtavValue[]? ParentSlots;
        public readonly int ParentIndex;
        public int Next;

        public MFrame(List<KeyValuePair<string, object?>> pairs, KtavValue[] parentSlots, int parentIndex)
        {
            IsList = false; Pairs = pairs; Count = pairs.Count;
            Slots = new KtavValue[pairs.Count];
            ParentSlots = parentSlots; ParentIndex = parentIndex;
        }

        public MFrame(List<object?> items, KtavValue[] parentSlots, int parentIndex)
        {
            IsList = true; Items = items; Count = items.Count;
            Slots = new KtavValue[items.Count];
            ParentSlots = parentSlots; ParentIndex = parentIndex;
        }

        public int Count { get; }
    }
}

