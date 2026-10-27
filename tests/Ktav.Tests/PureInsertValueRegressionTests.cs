using System;
using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>Pure InsertValue wrapper regressions from Rust cabi/edit_tests.rs, commit 861c64a.</summary>
[TestFixture]
public sealed class PureInsertValueRegressionTests
{
    private const KtavBackend Pure = KtavBackend.Pure;
    private static KtavValue S(string value) => new KtavString(value);

    [Test]
    public void InsertStringsPreserveExactEncodingAndSemantics()
    {
        foreach (var (value, spelling) in new[]
        {
            ("123", @"\u003123"), ("true", @"\u0074rue"), ("one,two", @"one\,two"),
            ("back\\slash", "back\\\\slash"), ("carriage\rreturn", "carriage\\rreturn")
        })
        {
            var text = Ktav.InsertValue("{}\n", "value", S(value), Pure);
            Assert.That(text, Is.EqualTo("{value: " + spelling + "}\n"));
            var parsed = (KtavObject)Ktav.Loads(text, Pure);
            Assert.That(((KtavString)parsed.TryGet("value")!).Value, Is.EqualTo(value));
        }
    }

    [Test]
    public void InsertStringWithNewlineUsesReferenceBlockLayout()
    {
        const string source = "a: {x: 1}\n";
        const string expected = "a: {\n    x: 1\n    value: ((\nline\nfeed\n    ))\n}\n";
        var output = Ktav.InsertValue(source, "a.value", S("line\nfeed"), Pure);
        Assert.That(output, Is.EqualTo(expected));
        var parent = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("a")!;
        Assert.That(((KtavInteger)parent.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavString)parent.TryGet("value")!).Value, Is.EqualTo("line\nfeed"));
    }

    [Test]
    public void NestedVerbatimLeadingSpacesSurviveInsert()
    {
        const string source = "a: {\n    x: 1\n}\n";
        const string expected = "a: {\n    x: 1\n    value: {\n        text: ((\n    four\n      six\n        ))\n    }\n}\n";
        var value = new KtavObject(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, KtavValue>("text", S("    four\n      six"))
        });
        var textual = Ktav.InsertValue(source, "a.value", value, Pure);
        var segmented = Ktav.InsertValueSegments(source, new[] { "a", "value" }, value, Pure);
        Assert.That(textual, Is.EqualTo(expected));
        Assert.That(segmented, Is.EqualTo(expected));
        foreach (var output in new[] { textual, segmented })
        {
            var parent = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("a")!;
            var inserted = (KtavObject)parent.TryGet("value")!;
            Assert.That(((KtavInteger)parent.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
            Assert.That(((KtavString)inserted.TryGet("text")!).Value, Is.EqualTo("    four\n      six"));
        }
    }

    [Test]
    public void StructuralAndEscapedCommaParityIsPreserved()
    {
        var structural = Ktav.InsertValue("a: {x: 1,}\n", "a.y", S("two"), Pure);
        Assert.That(structural, Is.EqualTo("a: {x: 1, y: two,}\n"));
        var structuralObject = (KtavObject)((KtavObject)Ktav.Loads(structural, Pure)).TryGet("a")!;
        Assert.That(((KtavInteger)structuralObject.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavString)structuralObject.TryGet("y")!).Value, Is.EqualTo("two"));
        var escaped = Ktav.InsertValue("a: {x: one\\,}\n", "a.y", S("two"), Pure);
        Assert.That(escaped, Is.EqualTo(@"a: {x: one\,, y: two}" + "\n"));
        var parsed = (KtavObject)((KtavObject)Ktav.Loads(escaped, Pure)).TryGet("a")!;
        Assert.That(((KtavString)parsed.TryGet("x")!).Value, Is.EqualTo("one,"));
        Assert.That(((KtavString)parsed.TryGet("y")!).Value, Is.EqualTo("two"));
    }

    [Test]
    public void ExplicitRootsAndNestedCompoundsInsertExactly()
    {
        var empty = Ktav.InsertValue("{\n}\n", "x", S("two"), Pure);
        Assert.That(empty, Is.EqualTo("{\n    x: two\n}\n"));
        Assert.That(((KtavString)((KtavObject)Ktav.Loads(empty, Pure)).TryGet("x")!).Value, Is.EqualTo("two"));
        var root = Ktav.InsertValue("{\n    x: 1\n}\n", "y", S("two"), Pure);
        Assert.That(root, Is.EqualTo("{\n    x: 1\n    y: two\n}\n"));
        var rootObject = (KtavObject)Ktav.Loads(root, Pure);
        Assert.That(((KtavInteger)rootObject.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavString)rootObject.TryGet("y")!).Value, Is.EqualTo("two"));
        var nested = Ktav.InsertValue("parent: {\n    child: [1]\n}\n", "parent.extra", S("ok"), Pure);
        Assert.That(nested, Is.EqualTo("parent: {\n    child: [1]\n    extra: ok\n}\n"));
        var parent = (KtavObject)((KtavObject)Ktav.Loads(nested, Pure)).TryGet("parent")!;
        Assert.That(((KtavArray)parent.TryGet("child")!).Items.Count, Is.EqualTo(1));
        Assert.That(((KtavString)parent.TryGet("extra")!).Value, Is.EqualTo("ok"));
    }

    [Test]
    public void NonemptyCompoundCrLfAndUtf8OffsetsRemainExact()
    {
        var source = "a: {\r\n    café: {keep: yes}\r\n}\r\n";
        var output = Ktav.InsertValue(source, "a.café.added", S("value"), Pure);
        Assert.That(output, Is.EqualTo("a: {\r\n    café: {keep: yes, added: value}\r\n}\r\n"));
        var utf8BadPath = Assert.Throws<KtavException>(() => Ktav.InsertValue("a: 1\n", "é\\q", S("value"), Pure));
        Assert.That(utf8BadPath!.Error, Is.EqualTo("EditInvalidPath"));
        Assert.That(utf8BadPath.Body, Is.EqualTo("2"));
        var parsed = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("a")!;
        var cafe = (KtavObject)parsed.TryGet("café")!;
        Assert.That(((KtavString)cafe.TryGet("keep")!).Value, Is.EqualTo("yes"));
        Assert.That(((KtavString)cafe.TryGet("added")!).Value, Is.EqualTo("value"));
        var objectValue = (KtavObject)Ktav.Loads("keep: yes\n", Pure);
        var withObject = Ktav.InsertValue("a: {\r\n    existing: old\r\n}\r\n", "a.added", objectValue, Pure);
        Assert.That(withObject, Is.EqualTo("a: {\r\n    existing: old\r\n    added: {\r\n        keep: yes\r\n    }\r\n}\r\n"));
        var insertedObject = (KtavObject)((KtavObject)Ktav.Loads(withObject, Pure)).TryGet("a")!;
        Assert.That(((KtavString)((KtavObject)insertedObject.TryGet("added")!).TryGet("keep")!).Value, Is.EqualTo("yes"));
    }

    [Test]
    public void PureInsertIntoArrayObjectMatchesReference()
    {
        const string source = "items: [\n  {x: 1}\n]\n";
        const string expected = "items: [\n  {x: 1, y: two}\n]\n";
        var textual = Ktav.InsertValue(source, "items.0.y", S("two"), Pure);
        var segmented = Ktav.InsertValueSegments(source, new[] { "items", "0", "y" }, S("two"), Pure);
        Assert.That(textual, Is.EqualTo(expected));
        Assert.That(segmented, Is.EqualTo(expected));
        var parsed = (KtavObject)Ktav.Loads(textual, Pure);
        var items = (KtavArray)parsed.TryGet("items")!;
        Assert.That(items.Items.Count, Is.EqualTo(1));
        var item = (KtavObject)items.Items[0];
        Assert.That(item.Entries.Count, Is.EqualTo(2));
        Assert.That(item.TryGet("x"), Is.TypeOf<KtavInteger>());
        Assert.That(((KtavInteger)item.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(item.TryGet("y"), Is.TypeOf<KtavString>());
        Assert.That(((KtavString)item.TryGet("y")!).Value, Is.EqualTo("two"));
    }

    [Test]
    public void RootAndNestedInsertHaveIndependentSemanticResults()
    {
        var rootText = Ktav.InsertValue("base: 7\n", "added", KtavInteger.Of(long.MaxValue), Pure);
        Assert.That(rootText, Is.EqualTo("base: 7\nadded: 9223372036854775807\n"));
        var root = (KtavObject)Ktav.Loads(rootText, Pure);
        Assert.That(root.TryGet("base"), Is.TypeOf<KtavInteger>());
        Assert.That(((KtavInteger)root.TryGet("base")!).Text, Is.EqualTo("7"));
        Assert.That(root.TryGet("added"), Is.TypeOf<KtavInteger>());
        Assert.That(((KtavInteger)root.TryGet("added")!).ToInt64(), Is.EqualTo(long.MaxValue));

        var nestedText = Ktav.InsertValueSegments("parent: {keep: yes}\n", new[] { "parent", "new" }, S("value"), Pure);
        Assert.That(nestedText, Is.EqualTo("parent: {keep: yes, new: value}\n"));
        var parent = (KtavObject)((KtavObject)Ktav.Loads(nestedText, Pure)).TryGet("parent")!;
        Assert.That(parent.TryGet("keep"), Is.TypeOf<KtavString>());
        Assert.That(((KtavString)parent.TryGet("keep")!).Value, Is.EqualTo("yes"));
        Assert.That(parent.TryGet("new"), Is.TypeOf<KtavString>());
        Assert.That(((KtavString)parent.TryGet("new")!).Value, Is.EqualTo("value"));
    }

    [Test]
    public void InlineTrailingCommaAndEscapedPathDelimiterArePreserved()
    {
        var structuralWithTrailingComma = Ktav.InsertValue("a: {x: 1,}\n", "a.y", S("two"), Pure);
        Assert.That(structuralWithTrailingComma, Is.EqualTo("a: {x: 1, y: two,}\n"));
        var source = "a: {\"x.y\": 1}\n";
        var output = Ktav.InsertValue(source, @"a.new\.key", KtavInteger.Of(2), Pure);
        Assert.That(output, Is.EqualTo("a: {\"x.y\": 1, \"new.key\": 2}\n"));
    }

    [Test]
    public void BomCrLfCommentsAndArrayTraversalRemainExactAndSemantic()
    {
        var source = "\uFEFF## heading\r\nitems: [\r\n  {keep: old}\r\n]\r\n## tail\r\n";
        var output = Ktav.InsertValueSegments(source, new[] { "items", "0", "added" }, S("yes"), Pure);
        Assert.That(output, Is.EqualTo("\uFEFF## heading\r\nitems: [\r\n  {keep: old, added: yes}\r\n]\r\n## tail\r\n"));
        var items = (KtavArray)((KtavObject)Ktav.Loads(output, Pure)).TryGet("items")!;
        var first = (KtavObject)items.Items[0];
        Assert.That(first.TryGet("keep"), Is.TypeOf<KtavString>());
        Assert.That(((KtavString)first.TryGet("keep")!).Value, Is.EqualTo("old"));
        Assert.That(first.TryGet("added"), Is.TypeOf<KtavString>());
        Assert.That(((KtavString)first.TryGet("added")!).Value, Is.EqualTo("yes"));
    }

    [Test]
    public void InvalidIndexAndOccupiedNestedPathReportTypedErrors()
    {
        var index = Assert.Throws<KtavException>(() => Ktav.InsertValue("items: [x]\n", "items.1.child", S("v"), Pure));
        Assert.That(index!.Error, Is.EqualTo("EditNotFound"));
        var occupied = Assert.Throws<KtavException>(() => Ktav.InsertValueSegments("a: {b: 1}\n", new[] { "a", "b" }, S("v"), Pure));
        Assert.That(occupied!.Error, Is.EqualTo("EditAlreadyExists"));
        Assert.That(occupied.Body, Is.Null);
        Assert.That(occupied.Span, Is.Null);
    }

    [Test]
    public void RootAppendPreservesCommentsAndExistingBytes()
    {
        Assert.That(Ktav.InsertValue("a: 1\n## tail\n", "b", S("v"), Pure), Is.EqualTo("a: 1\n## tail\nb: v\n"));
        Assert.That(Ktav.InsertValueSegments("a: 1\n## tail\n", new[] { "b" }, S("v"), Pure), Is.EqualTo("a: 1\n## tail\nb: v\n"));
    }

    [Test]
    public void BlockInlineAndDottedParentsKeepReferenceOutput()
    {
        Assert.That(Ktav.InsertValue("a: {x: 1}\n", "a.y", S("two"), Pure), Is.EqualTo("a: {x: 1, y: two}\n"));
        Assert.That(Ktav.InsertValueSegments("a: {x: 1}\n", new[] { "a", "y" }, S("two"), Pure), Is.EqualTo("a: {x: 1, y: two}\n"));
        Assert.That(Ktav.InsertValue("a: {\n  x: 1\n}\n", "a.y", S("two"), Pure), Is.EqualTo("a: {\n  x: 1\n  y: two\n}\n"));
        var dotted = Assert.Throws<KtavException>(() => Ktav.InsertValue("a.b: 1\n", "a.c", S("two"), Pure));
        Assert.That(dotted!.Error, Is.EqualTo("EditUnsupported"));
        var dottedSegments = Assert.Throws<KtavException>(() => Ktav.InsertValueSegments("a.b: 1\n", new[] { "a", "c" }, S("two"), Pure));
        Assert.That(dottedSegments!.Error, Is.EqualTo("EditUnsupported"));
        Assert.That(Ktav.InsertValue("a: {x: 1}\n", "a.y", S("two"), Pure), Is.EqualTo("a: {x: 1, y: two}\n"));
    }

    [Test]
    public void LiteralDottedAndUnicodeKeysUseBothPathForms()
    {
        Assert.That(Ktav.InsertValue("a: 1\n", @"a\.b", KtavInteger.Of(2), Pure), Is.EqualTo("a: 1\n\"a.b\": 2\n"));
        Assert.That(Ktav.InsertValueSegments("k: 1\n", new[] { "ключ" }, KtavInteger.Of(2), Pure), Is.EqualTo("k: 1\nключ: 2\n"));
    }

    [Test]
    public void BomAndCrLfRemainByteExact()
    {
        var bom = "\uFEFFa: 1\r\n";
        Assert.That(Ktav.InsertValue(bom, "b", S("v"), Pure), Is.EqualTo(bom + "b: v\r\n"));
        Assert.That(Ktav.InsertValueSegments(bom, new[] { "b" }, S("v"), Pure), Is.EqualTo(bom + "b: v\r\n"));
        var lf = "a: 1\n";
        Assert.That(Ktav.InsertValue(lf, "b", S("v"), Pure), Is.EqualTo(lf + "b: v\n"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CollisionAndMissingParentHaveTypedErrors(bool segments)
    {
        KtavException Error(Action action) => Assert.Throws<KtavException>(() => action())!;
        var collision = Error(() => { if (segments) Ktav.InsertValueSegments("a: 1\n", new[] { "a" }, S("x"), Pure); else Ktav.InsertValue("a: 1\n", "a", S("x"), Pure); });
        Assert.That(collision.Error, Is.EqualTo("EditAlreadyExists"));
        Assert.That(collision.Body, Is.Null);
        Assert.That(collision.Span, Is.Null);
        var missing = Error(() => { if (segments) Ktav.InsertValueSegments("a: 1\n", new[] { "b", "c" }, S("x"), Pure); else Ktav.InsertValue("a: 1\n", "b.c", S("x"), Pure); });
        Assert.That(missing.Error, Is.EqualTo("EditNotFound"));
        Assert.That(missing.Body, Is.Null);
        Assert.That(missing.Span, Is.Null);
    }

    [Test]
    public void SourceErrorsPrecedeInvalidPathAndValue()
    {
        const string broken = "a: {\n";
        var invalidPath = Assert.Throws<KtavException>(() => Ktav.InsertValue(broken, "a\\q", S("x"), Pure));
        Assert.That(invalidPath!.Error, Is.EqualTo("UnclosedCompound"));
        var missing = Assert.Throws<KtavException>(() => Ktav.InsertValue(broken, "missing", new KtavFloat("12x"), Pure));
        Assert.That(missing!.Error, Is.EqualTo("UnclosedCompound"));
        var emptySegments = Assert.Throws<KtavException>(() => Ktav.InsertValueSegments(broken, Array.Empty<string>(), S("x"), Pure));
        Assert.That(emptySegments!.Error, Is.EqualTo("UnclosedCompound"));
    }

    [Test]
    public void RequestedPreciseInsertRegressions()
    {
        // Both public path forms must retain block layouts while independently
        // proving that inserted compounds and untouched siblings still parse.
        foreach (bool segments in new[] { false, true })
        {
            string Insert(string source, string[] path, KtavValue value) => segments
                ? Ktav.InsertValueSegments(source, path, value, Pure)
                : Ktav.InsertValue(source, string.Join(".", path), value, Pure);
            const string source = "outer: {\n  keep: yes\n}\nother: untouched\n";
            var compound = (KtavObject)Ktav.Loads("inside: [1]\nchild: {leaf: ok}\n", Pure);
            var output = Insert(source, new[] { "outer", "added" }, compound);
            Assert.That(output, Is.EqualTo("outer: {\n  keep: yes\n  added: {\n      inside: [\n          1\n      ]\n      child: {\n          leaf: ok\n      }\n  }\n}\nother: untouched\n"));
            var root = (KtavObject)Ktav.Loads(output, Pure);
            Assert.That(((KtavString)root.TryGet("other")!).Value, Is.EqualTo("untouched"));
            var outer = (KtavObject)root.TryGet("outer")!;
            Assert.That(((KtavString)outer.TryGet("keep")!).Value, Is.EqualTo("yes"));
            var added = (KtavObject)outer.TryGet("added")!;
            Assert.That(((KtavArray)added.TryGet("inside")!).Items.Count, Is.EqualTo(1));
            Assert.That(((KtavString)((KtavObject)added.TryGet("child")!).TryGet("leaf")!).Value, Is.EqualTo("ok"));
        }

        const string nestedSource = "root: {\n  keep: yes\n}\n";
        var nestedValue = (KtavObject)Ktav.Loads("object: {number: 12}\narray: [{text:: 123}]\n", Pure);
        var nestedOutput = Ktav.InsertValue(nestedSource, "root.inserted", nestedValue, Pure);
        const string expected = "root: {\n  keep: yes\n  inserted: {\n      object: {\n          number: 12\n      }\n      array: [\n          {\n              text:: 123\n          }\n      ]\n  }\n}\n";
        Assert.That(nestedOutput, Is.EqualTo(expected));

        var inserted = (KtavObject)((KtavObject)Ktav.Loads(nestedOutput, Pure)).TryGet("root")!;
        var insertedCompound = (KtavObject)inserted.TryGet("inserted")!;
        Assert.That(((KtavInteger)((KtavObject)insertedCompound.TryGet("object")!).TryGet("number")!).ToInt64(), Is.EqualTo(12L));
        var nestedArray = (KtavArray)insertedCompound.TryGet("array")!;
        Assert.That(((KtavString)((KtavObject)nestedArray.Items[0]).TryGet("text")!).Value, Is.EqualTo("123"));

        foreach (bool segments in new[] { false, true })
        {
            const string crlfSource = "root: {\r\n  keep: old\r\n}\r\n";
            var crlf = segments
                ? Ktav.InsertValueSegments(crlfSource, new[] { "root", "added" }, nestedValue, Pure)
                : Ktav.InsertValue(crlfSource, "root.added", nestedValue, Pure);
            Assert.That(crlf, Is.EqualTo("root: {\r\n  keep: old\r\n  added: {\r\n      object: {\r\n          number: 12\r\n      }\r\n      array: [\r\n          {\r\n              text:: 123\r\n          }\r\n      ]\r\n  }\r\n}\r\n"));
            var crlfRoot = (KtavObject)Ktav.Loads(crlf, Pure);
            var crlfInserted = (KtavObject)((KtavObject)crlfRoot.TryGet("root")!).TryGet("added")!;
            Assert.That(((KtavString)((KtavObject)((KtavArray)crlfInserted.TryGet("array")!).Items[0]).TryGet("text")!).Value, Is.EqualTo("123"));

            const string mixedSource = "root: {\r\n  keep: old\n}\r\n";
            var mixed = segments
                ? Ktav.InsertValueSegments(mixedSource, new[] { "root", "added" }, nestedValue, Pure)
                : Ktav.InsertValue(mixedSource, "root.added", nestedValue, Pure);
            Assert.That(mixed, Is.EqualTo("root: {\r\n  keep: old\n  added: {\r\n      object: {\r\n          number: 12\r\n      }\r\n      array: [\r\n          {\r\n              text:: 123\r\n          }\r\n      ]\r\n  }\r\n}\r\n"));
            var mixedRoot = (KtavObject)Ktav.Loads(mixed, Pure);
            var mixedInserted = (KtavObject)((KtavObject)mixedRoot.TryGet("root")!).TryGet("added")!;
            Assert.That(((KtavString)((KtavObject)((KtavArray)mixedInserted.TryGet("array")!).Items[0]).TryGet("text")!).Value, Is.EqualTo("123"));
        }

        var nbsp = Ktav.InsertValue("a: {x: 1,}\n", "a.y", S("\u00A0suffix"), Pure);
        Assert.That(nbsp, Is.EqualTo("a: {x: 1, y: \\u00A0suffix,}\n"));
        Assert.That(((KtavString)((KtavObject)((KtavObject)Ktav.Loads(nbsp, Pure)).TryGet("a")!).TryGet("y")!).Value, Is.EqualTo("\u00A0suffix"));
        var nbspAfterComma = Ktav.InsertValue("a: {x: 1,\u00A0}\n", "a.y", S("v"), Pure);
        Assert.That(nbspAfterComma, Is.EqualTo("a: {x: 1, y: v,\u00A0}\n"));
        Assert.That(((KtavString)((KtavObject)((KtavObject)Ktav.Loads(nbspAfterComma, Pure)).TryGet("a")!).TryGet("y")!).Value, Is.EqualTo("v"));
        var emptyInline = Ktav.InsertValue("a: {}\n", "a.x", S(""), Pure);
        Assert.That(emptyInline, Is.EqualTo("a: {x: }\n"));
        Assert.That(((KtavString)((KtavObject)((KtavObject)Ktav.Loads(emptyInline, Pure)).TryGet("a")!).TryGet("x")!).Value, Is.EqualTo(""));

        var slashesEven = Ktav.InsertValue("a: {x: one\\\\,}\n", "a.y", S("two"), Pure);
        Assert.That(slashesEven, Is.EqualTo("a: {x: one\\\\, y: two,}\n"));
        Assert.That(((KtavString)((KtavObject)((KtavObject)Ktav.Loads(slashesEven, Pure)).TryGet("a")!).TryGet("x")!).Value, Is.EqualTo("one\\"));

        const string siblingSource = "a: {\n    first: {x: 1}\n    second: {keep: yes}\n}\n";
        var rootIndented = Ktav.InsertValue("{\n    x: 1\n}\n", "y", S("v"), Pure);
        Assert.That(rootIndented, Is.EqualTo("{\n    x: 1\n    y: v\n}\n"));
        var explicitRoot = (KtavObject)Ktav.Loads(rootIndented, Pure);
        Assert.That(((KtavInteger)explicitRoot.TryGet("x")!).ToInt64(), Is.EqualTo(1));
        Assert.That(((KtavString)explicitRoot.TryGet("y")!).Value, Is.EqualTo("v"));
        foreach (bool segments in new[] { false, true })
        {
            var sibling = segments
                ? Ktav.InsertValueSegments(siblingSource, new[] { "a", "second", "new" }, S("v"), Pure)
                : Ktav.InsertValue(siblingSource, "a.second.new", S("v"), Pure);
            Assert.That(sibling, Is.EqualTo("a: {\n    first: {x: 1}\n    second: {keep: yes, new: v}\n}\n"));
            var siblingParent = (KtavObject)((KtavObject)Ktav.Loads(sibling, Pure)).TryGet("a")!;
            Assert.That(((KtavInteger)((KtavObject)siblingParent.TryGet("first")!).TryGet("x")!).ToInt64(), Is.EqualTo(1));
            Assert.That(((KtavString)((KtavObject)siblingParent.TryGet("second")!).TryGet("new")!).Value, Is.EqualTo("v"));
        }

        foreach (bool segments in new[] { false, true })
        {
            var unevenRoot = segments
                ? Ktav.InsertValueSegments("{\n    first: 1\n  second: 2\n}\n", new[] { "third" }, S("v"), Pure)
                : Ktav.InsertValue("{\n    first: 1\n  second: 2\n}\n", "third", S("v"), Pure);
            Assert.That(unevenRoot, Is.EqualTo("{\n    first: 1\n  second: 2\n  third: v\n}\n"));
            var unevenObject = (KtavObject)Ktav.Loads(unevenRoot, Pure);
            Assert.That(((KtavInteger)unevenObject.TryGet("first")!).ToInt64(), Is.EqualTo(1));
            Assert.That(((KtavInteger)unevenObject.TryGet("second")!).ToInt64(), Is.EqualTo(2));
            Assert.That(((KtavString)unevenObject.TryGet("third")!).Value, Is.EqualTo("v"));

            var fromEmpty = segments
                ? Ktav.InsertValueSegments("", new[] { "x" }, S("v"), Pure)
                : Ktav.InsertValue("", "x", S("v"), Pure);
            Assert.That(fromEmpty, Is.EqualTo("x: v"));
            var emptyObject = (KtavObject)Ktav.Loads(fromEmpty, Pure);
            Assert.That(((KtavString)emptyObject.TryGet("x")!).Value, Is.EqualTo("v"));
        }

        var bomOnly = Ktav.InsertValue("\uFEFF", "x", S("v"), Pure);
        Assert.That(bomOnly, Is.EqualTo("\uFEFF\nx: v"));
        Assert.That(((KtavString)((KtavObject)Ktav.Loads(bomOnly, Pure)).TryGet("x")!).Value, Is.EqualTo("v"));
        var bomCrlf = Ktav.InsertValue("\uFEFFa: 1\r\n", "b", S("v"), Pure);
        Assert.That(bomCrlf, Is.EqualTo("\uFEFFa: 1\r\nb: v\r\n"));
        var bomRoot = (KtavObject)Ktav.Loads(bomCrlf, Pure);
        Assert.That(((KtavInteger)bomRoot.TryGet("a")!).ToInt64(), Is.EqualTo(1));
        Assert.That(((KtavString)bomRoot.TryGet("b")!).Value, Is.EqualTo("v"));
        Assert.That(Ktav.InsertValue("a: 1", "b", S("v"), Pure), Is.EqualTo("a: 1\nb: v"));
        var marker = Ktav.InsertValue("a: {}\n", "a.x", S("before\n))\nafter"), Pure);
        Assert.That(marker, Is.EqualTo("a: {x: before\\n))\\nafter}\n"));
        Assert.That(((KtavString)((KtavObject)((KtavObject)Ktav.Loads(marker, Pure)).TryGet("a")!).TryGet("x")!).Value, Is.EqualTo("before\n))\nafter"));
    }

    [Test]
    public void InlineRootWithLeadingCommentsInsertsExactlyForBothPathApis()
    {
        const string source = "## leading comment\n{x: 1}\n";
        const string expected = "## leading comment\n{x: 1, y: two}\n";

        var textual = Ktav.InsertValue(source, "y", S("two"), Pure);
        var segmented = Ktav.InsertValueSegments(source, new[] { "y" }, S("two"), Pure);
        Assert.That(textual, Is.EqualTo(expected));
        Assert.That(segmented, Is.EqualTo(expected));

        foreach (var output in new[] { textual, segmented })
        {
            var root = (KtavObject)Ktav.Loads(output, Pure);
            Assert.That(((KtavInteger)root.TryGet("x")!).ToInt64(), Is.EqualTo(1L));
            Assert.That(((KtavString)root.TryGet("y")!).Value, Is.EqualTo("two"));
        }
    }

    [Test]
    public void MinimalNestedBlockLayoutPreservesUntouchedSiblings()
    {
        const string source = "root: {\n  branch: {\n    keep: original\n  }\n  sibling: stable\n}\n";
        var output = Ktav.InsertValueSegments(source, new[] { "root", "branch", "new" }, S("line1\nline2"), Pure);
        Assert.That(output, Is.EqualTo("root: {\n  branch: {\n    keep: original\n    new: ((\nline1\nline2\n    ))\n  }\n  sibling: stable\n}\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var value = (KtavObject)((KtavObject)root.TryGet("root")!).TryGet("branch")!;
        Assert.That(((KtavString)value.TryGet("keep")!).Value, Is.EqualTo("original"));
        Assert.That(((KtavString)value.TryGet("new")!).Value, Is.EqualTo("line1\nline2"));
        Assert.That(((KtavString)((KtavObject)root.TryGet("root")!).TryGet("sibling")!).Value, Is.EqualTo("stable"));
    }

    [Test]
    public void InvalidTextualPathIsRejected()
    {
        var error = Assert.Throws<KtavException>(() => Ktav.InsertValue("a: 1\n", "a..b", S("x"), Pure));
        Assert.That(error!.Error, Is.EqualTo("EditInvalidPath"));
        var missing = Assert.Throws<KtavException>(() => Ktav.InsertValue("a: 1\n", "missing.child", S("x"), Pure));
        Assert.That(missing!.Error, Is.EqualTo("EditNotFound"));
        Assert.That(missing.Body, Is.Null);
        Assert.That(missing.Span, Is.Null);
    }

}
