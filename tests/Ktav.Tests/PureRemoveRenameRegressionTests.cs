using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>Pure-backend remove/rename source mutations.</summary>
[TestFixture]
public sealed class PureRemoveRenameRegressionTests
{
    private const KtavBackend Pure = KtavBackend.Pure;

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveNestedArrayElementPreservesCommentsAndSemantics(bool segments)
    {
        const string source = "## root leading\n## top\na: {b: [one, two]}\n## tail\nz: 3\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "a", "b", "1" }, Pure)
            : Ktav.RemoveValue(source, "a.b.1", Pure);
        Assert.That(output, Is.EqualTo("## root leading\n## top\na: {b: [one]}\n## tail\nz: 3\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var a = (KtavObject)root.TryGet("a")!;
        var b = (KtavArray)a.TryGet("b")!;
        Assert.That(b.Items.Count, Is.EqualTo(1));
        Assert.That(((KtavString)b.Items[0]).Value, Is.EqualTo("one"));
        Assert.That(((KtavInteger)root.TryGet("z")!).ToInt64(), Is.EqualTo(3L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveDottedKeyDoesNotPruneAndArrayRemovalShifts(bool segments)
    {
        const string dotted = "a.b: 1\na.c: 2\n";
        var dottedOutput = segments
            ? Ktav.RemoveValueSegments(dotted, new[] { "a", "b" }, Pure)
            : Ktav.RemoveValue(dotted, "a.b", Pure);
        Assert.That(dottedOutput, Is.EqualTo("a.c: 2\n"));
        var dottedRoot = (KtavObject)Ktav.Loads(dottedOutput, Pure);
        var dottedA = (KtavObject)dottedRoot.TryGet("a")!;
        Assert.That(dottedA.TryGet("b"), Is.Null);
        Assert.That(((KtavInteger)dottedA.TryGet("c")!).ToInt64(), Is.EqualTo(2L));

        const string singleDotted = "a.b: 1\nz: 3\n";
        var singleOutput = segments
            ? Ktav.RemoveValueSegments(singleDotted, new[] { "a", "b" }, Pure)
            : Ktav.RemoveValue(singleDotted, "a.b", Pure);
        Assert.That(singleOutput, Is.EqualTo("a: {}\nz: 3\n"));
        var singleRoot = (KtavObject)Ktav.Loads(singleOutput, Pure);
        Assert.That(singleRoot.TryGet("a"), Is.TypeOf<KtavObject>());
        Assert.That(((KtavObject)singleRoot.TryGet("a")!).Entries, Is.Empty);
        Assert.That(((KtavInteger)singleRoot.TryGet("z")!).ToInt64(), Is.EqualTo(3L));

        const string arraySource = "items: [a, b, c]\n";
        var arrayOutput = segments
            ? Ktav.RemoveValueSegments(arraySource, new[] { "items", "1" }, Pure)
            : Ktav.RemoveValue(arraySource, "items.1", Pure);
        Assert.That(arrayOutput, Is.EqualTo("items: [a, c]\n"));
        var items = (KtavArray)((KtavObject)Ktav.Loads(arrayOutput, Pure)).TryGet("items")!;
        Assert.That(items.Items.Count, Is.EqualTo(2));
        Assert.That(((KtavString)items.Items[0]).Value, Is.EqualTo("a"));
        Assert.That(((KtavString)items.Items[1]).Value, Is.EqualTo("c"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameQuotedKeyNoopAndCollision(bool segments)
    {
        const string source = "## root leading\n\"quoted.key\": 1\nother: 2\n";
        string Rename(string path, string newKey) => segments
            ? Ktav.RenameKeySegments(source, path == "quoted.key" ? new[] { "quoted.key" } : new[] { path }, newKey, Pure)
            : Ktav.RenameKey(source, path, newKey, Pure);

        var renamed = Rename(segments ? "quoted.key" : @"quoted\.key", "renamed");
        Assert.That(renamed, Is.EqualTo("## root leading\nrenamed: 1\nother: 2\n"));
        var renamedRoot = (KtavObject)Ktav.Loads(renamed, Pure);
        Assert.That(renamedRoot.TryGet("quoted.key"), Is.Null);
        Assert.That(((KtavInteger)renamedRoot.TryGet("renamed")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)renamedRoot.TryGet("other")!).ToInt64(), Is.EqualTo(2L));

        Assert.That(Rename("other", "other"), Is.EqualTo(source));
        var collision = Assert.Throws<KtavException>(() => Rename("other", "quoted.key"));
        Assert.That(collision!.Error, Is.EqualTo("EditAlreadyExists"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameInlineQuotedCommaKeyUsesLexicalPairStart(bool segments)
    {
        const string source = "root: { first: 1, \"comma,key\": 2, last: 3 }\ntail: 4\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "comma,key" }, "renamed", Pure)
            : Ktav.RenameKey(source, "root.comma,key", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { first: 1, renamed: 2, last: 3 }\ntail: 4\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var nested = (KtavObject)root.TryGet("root")!;
        Assert.That(nested.TryGet("comma,key"), Is.Null);
        Assert.That(((KtavInteger)nested.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)nested.TryGet("first")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)nested.TryGet("last")!).ToInt64(), Is.EqualTo(3L));
        Assert.That(((KtavInteger)root.TryGet("tail")!).ToInt64(), Is.EqualTo(4L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameInlineQuotedDottedKeyWithNbsp(bool segments)
    {
        const string source = "root: { \u00a0\"quoted.key\"\u00a0: 2, prior: value }\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "quoted.key" }, "renamed", Pure)
            : Ktav.RenameKey(source, @"root.quoted\.key", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { \u00a0renamed\u00a0: 2, prior: value }\n"));
        var nested = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("root")!;
        Assert.That(((KtavInteger)nested.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavString)nested.TryGet("prior")!).Value, Is.EqualTo("value"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameInlinePriorScalarQuoteDoesNotStartQuotedKey(bool segments)
    {
        const string source = "root: { prior: don't, \"quoted.key\": 2 }\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "quoted.key" }, "renamed", Pure)
            : Ktav.RenameKey(source, @"root.quoted\.key", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { prior: don't, renamed: 2 }\n"));
        var nested = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("root")!;
        Assert.That(((KtavString)nested.TryGet("prior")!).Value, Is.EqualTo("don't"));
        Assert.That(((KtavInteger)nested.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameInlineQuotedDottedKeyPreservesSiblings(bool segments)
    {
        const string source = "root: { before: 1, \"x.y\": 2, after: 3 }\ntail: 4\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "x.y" }, "renamed", Pure)
            : Ktav.RenameKey(source, @"root.x\.y", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { before: 1, renamed: 2, after: 3 }\ntail: 4\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var nested = (KtavObject)root.TryGet("root")!;
        Assert.That(((KtavInteger)nested.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)nested.TryGet("before")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)nested.TryGet("after")!).ToInt64(), Is.EqualTo(3L));
        Assert.That(((KtavInteger)root.TryGet("tail")!).ToInt64(), Is.EqualTo(4L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameEmptyLastValueAfterMidScalarBracket(bool segments)
    {
        const string source = "root: { prior: a[b, target:}\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "target" }, "renamed", Pure)
            : Ktav.RenameKey(source, "root.target", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { prior: a[b, renamed:}\n"));
        var obj = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("root")!;
        Assert.That(((KtavString)obj.TryGet("prior")!).Value, Is.EqualTo("a[b"));
        Assert.That(((KtavString)obj.TryGet("renamed")!).Value, Is.EqualTo(""));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameSiblingAfterMidScalarBracketPreservesSemantics(bool segments)
    {
        const string source = "root: { first: value[tail, target: 2, last: 3 }\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "target" }, "renamed", Pure)
            : Ktav.RenameKey(source, "root.target", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { first: value[tail, renamed: 2, last: 3 }\n"));
        var obj = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("root")!;
        Assert.That(((KtavString)obj.TryGet("first")!).Value, Is.EqualTo("value[tail"));
        Assert.That(((KtavInteger)obj.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)obj.TryGet("last")!).ToInt64(), Is.EqualTo(3L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameSiblingAfterMidScalarQuotePreservesSemantics(bool segments)
    {
        const string source = "root: { first: don't, target: 2, last: 3 }\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "root", "target" }, "renamed", Pure)
            : Ktav.RenameKey(source, "root.target", "renamed", Pure);
        Assert.That(output, Is.EqualTo("root: { first: don't, renamed: 2, last: 3 }\n"));
        var obj = (KtavObject)((KtavObject)Ktav.Loads(output, Pure)).TryGet("root")!;
        Assert.That(((KtavString)obj.TryGet("first")!).Value, Is.EqualTo("don't"));
        Assert.That(((KtavInteger)obj.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)obj.TryGet("last")!).ToInt64(), Is.EqualTo(3L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameRootInlinePairBothPathForms(bool segments)
    {
        const string source = "{ first: 1, second: 2 }\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "second" }, "renamed", Pure)
            : Ktav.RenameKey(source, "second", "renamed", Pure);
        Assert.That(output, Is.EqualTo("{ first: 1, renamed: 2 }\n"));
        var value = (KtavObject)Ktav.Loads(output, Pure);
        Assert.That(((KtavInteger)value.TryGet("first")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)value.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameNestedInlineQuotedColonAndArrayParent(bool segments)
    {
        const string source = "items: [{ \"a:b\": 1, target: 2 }]\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "items", "0", "target" }, "renamed", Pure)
            : Ktav.RenameKey(source, "items.0.target", "renamed", Pure);
        Assert.That(output, Is.EqualTo("items: [{ \"a:b\": 1, renamed: 2 }]\n"));
        var items = (KtavArray)((KtavObject)Ktav.Loads(output, Pure)).TryGet("items")!;
        var item = (KtavObject)items.Items[0];
        Assert.That(((KtavInteger)item.TryGet("a:b")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)item.TryGet("renamed")!).ToInt64(), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveRootArrayElementBothPathForms(bool segments)
    {
        const string source = "[one, two, three]\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "1" }, Pure)
            : Ktav.RemoveValue(source, "1", Pure);
        Assert.That(output, Is.EqualTo("[one, three]\n"));
        var array = (KtavArray)Ktav.Loads(output, Pure);
        Assert.That(array.Items.Count, Is.EqualTo(2));
        Assert.That(((KtavString)array.Items[0]).Value, Is.EqualTo("one"));
        Assert.That(((KtavString)array.Items[1]).Value, Is.EqualTo("three"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemovePlainScalarContainingQuoteCharactersPreservesText(bool segments)
    {
        const string source = "{ keep: don't \"stop\" `now`, remove: 1 }\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "remove" }, Pure)
            : Ktav.RemoveValue(source, "remove", Pure);
        Assert.That(output, Is.EqualTo("{ keep: don't \"stop\" `now` }\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        Assert.That(((KtavString)root.TryGet("keep")!).Value, Is.EqualTo("don't \"stop\" `now`"));
        Assert.That(root.TryGet("remove"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RenameAroundUnicodeWhitespacePreservesExactSource(bool segments)
    {
        const string source = "before: 1\n\u00a0\u0085old\u00a0: 2\nafter: 3\n";
        var output = segments
            ? Ktav.RenameKeySegments(source, new[] { "old" }, "new", Pure)
            : Ktav.RenameKey(source, "old", "new", Pure);
        Assert.That(output, Is.EqualTo("before: 1\n\u00a0\u0085new\u00a0: 2\nafter: 3\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        Assert.That(root.TryGet("old"), Is.Null);
        Assert.That(((KtavInteger)root.TryGet("new")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)root.TryGet("before")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(((KtavInteger)root.TryGet("after")!).ToInt64(), Is.EqualTo(3L));
    }

    [TestCase("\r\n", true, false)]
    [TestCase("\n", false, true)]
    public void RemovePreservesBomEolAndEof(string eol, bool finalNewline, bool segments)
    {
        string source = "\uFEFFkeep: 1" + eol + "remove: 2" + (finalNewline ? eol : "");
        string expected = "\uFEFFkeep: 1" + (finalNewline ? eol : "");
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "remove" }, Pure)
            : Ktav.RemoveValue(source, "remove", Pure);
        Assert.That(output, Is.EqualTo(expected));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        Assert.That(((KtavInteger)root.TryGet("keep")!).ToInt64(), Is.EqualTo(1L));
        Assert.That(root.TryGet("remove"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveDeepDottedMaterializationPreservesSemantics(bool segments)
    {
        const string source = "a.b.c.value: 1\na.b.c.other: 2\nz: 3\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "a", "b", "c", "value" }, Pure)
            : Ktav.RemoveValue(source, "a.b.c.value", Pure);
        Assert.That(output, Is.EqualTo("a.b.c.other: 2\nz: 3\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var a = (KtavObject)root.TryGet("a")!;
        var b = (KtavObject)a.TryGet("b")!;
        var c = (KtavObject)b.TryGet("c")!;
        Assert.That(c.TryGet("value"), Is.Null);
        Assert.That(((KtavInteger)c.TryGet("other")!).ToInt64(), Is.EqualTo(2L));
        Assert.That(((KtavInteger)root.TryGet("z")!).ToInt64(), Is.EqualTo(3L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveArrayObjectElementPreservesSemanticNeighbors(bool segments)
    {
        const string source = "items: [{ name: one }, { name: two }, { name: three }]\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "items", "1" }, Pure)
            : Ktav.RemoveValue(source, "items.1", Pure);
        Assert.That(output, Is.EqualTo("items: [{ name: one }, { name: three }]\n"));
        var items = (KtavArray)((KtavObject)Ktav.Loads(output, Pure)).TryGet("items")!;
        Assert.That(items.Items.Count, Is.EqualTo(2));
        Assert.That(((KtavString)((KtavObject)items.Items[0]).TryGet("name")!).Value, Is.EqualTo("one"));
        Assert.That(((KtavString)((KtavObject)items.Items[1]).TryGet("name")!).Value, Is.EqualTo("three"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveLastDeepDottedBlockLeafLeavesEmptyParent(bool segments)
    {
        const string source = "outer: {\r\n  a.b.c: 1\r\n}\r\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "outer", "a", "b", "c" }, Pure)
            : Ktav.RemoveValue(source, "outer.a.b.c", Pure);
        Assert.That(output, Is.EqualTo("outer: {\r\n  a.b: {}\r\n}\r\n"));
        var root = (KtavObject)Ktav.Loads(output, Pure);
        var outer = (KtavObject)root.TryGet("outer")!;
        var a = (KtavObject)outer.TryGet("a")!;
        var b = (KtavObject)a.TryGet("b")!;
        Assert.That(b.Entries, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveRootArrayIndexPreservesBomCommentsAndNonbreakingSpaces(bool segments)
    {
        const string source = "\uFEFF## { leading\r\n\u00A0[one, two, three]\u00A0\r\n## trailing\r\n";
        const string expected = "\uFEFF## { leading\r\n\u00A0[one, three]\u00A0\r\n## trailing\r\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "1" }, Pure)
            : Ktav.RemoveValue(source, "1", Pure);
        Assert.That(output, Is.EqualTo(expected));
        var array = (KtavArray)Ktav.Loads(output, Pure);
        Assert.That(array.Items.Count, Is.EqualTo(2));
        Assert.That(((KtavString)array.Items[0]).Value, Is.EqualTo("one"));
        Assert.That(((KtavString)array.Items[1]).Value, Is.EqualTo("three"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveDeepDottedLeafFromArrayObjectBlock(bool segments)
    {
        const string source = "items: [\r\n  {\r\n    a.b.c: 1\r\n    keep: 2\r\n  }\r\n]\r\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "items", "0", "a", "b", "c" }, Pure)
            : Ktav.RemoveValue(source, "items.0.a.b.c", Pure);
        Assert.That(output, Is.EqualTo("items: [\r\n  {\r\n    a.b: {}\r\n    keep: 2\r\n  }\r\n]\r\n"));
        var items = (KtavArray)((KtavObject)Ktav.Loads(output, Pure)).TryGet("items")!;
        var first = (KtavObject)items.Items[0];
        var a = (KtavObject)first.TryGet("a")!;
        Assert.That(((KtavObject)a.TryGet("b")!).Entries, Is.Empty);
        Assert.That(((KtavInteger)first.TryGet("keep")!).ToInt64(), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveInlineObjectFirstMiddleAndLastPreservesOrderedSiblingTypes(bool segments)
    {
        const string source = "root: { first: 1, middle: two, last: true }\ntail: null\n";
        foreach (var (key, expected) in new[]
        {
            ("first", "root: { middle: two, last: true }\ntail: null\n"),
            ("middle", "root: { first: 1, last: true }\ntail: null\n"),
            ("last", "root: { first: 1, middle: two }\ntail: null\n")
        })
        {
            var output = segments
                ? Ktav.RemoveValueSegments(source, new[] { "root", key }, Pure)
                : Ktav.RemoveValue(source, "root." + key, Pure);
            Assert.That(output, Is.EqualTo(expected));

            var root = (KtavObject)Ktav.Loads(output, Pure);
            Assert.That(root.Entries.Count, Is.EqualTo(2));
            Assert.That(root.Entries[0].Key, Is.EqualTo("root"));
            Assert.That(root.Entries[1].Key, Is.EqualTo("tail"));
            var obj = (KtavObject)root.TryGet("root")!;
            var remaining = key switch
            {
                "first" => new (string, KtavValue)[] { ("middle", new KtavString("two")), ("last", KtavBool.True) },
                "middle" => new (string, KtavValue)[] { ("first", KtavInteger.Of(1)), ("last", KtavBool.True) },
                _ => new (string, KtavValue)[] { ("first", KtavInteger.Of(1)), ("middle", new KtavString("two")) }
            };
            Assert.That(obj.Entries.Count, Is.EqualTo(2));
            for (int i = 0; i < remaining.Length; i++)
            {
                Assert.That(obj.Entries[i].Key, Is.EqualTo(remaining[i].Item1));
                Assert.That(obj.Entries[i].Value, Is.EqualTo(remaining[i].Item2));
            }
            Assert.That(root.TryGet("tail"), Is.EqualTo(KtavNull.Instance));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveNestedInlineObjectPreservesOrderedSiblingTypes(bool segments)
    {
        const string source = "outer: { inner: { first: 1, remove: false, last: kept }, tail: 2 }\n";
        const string expected = "outer: { inner: { first: 1, last: kept }, tail: 2 }\n";
        var output = segments
            ? Ktav.RemoveValueSegments(source, new[] { "outer", "inner", "remove" }, Pure)
            : Ktav.RemoveValue(source, "outer.inner.remove", Pure);
        Assert.That(output, Is.EqualTo(expected));

        var root = (KtavObject)Ktav.Loads(output, Pure);
        Assert.That(root.Entries.Count, Is.EqualTo(1));
        Assert.That(root.Entries[0].Key, Is.EqualTo("outer"));
        var outer = (KtavObject)root.TryGet("outer")!;
        Assert.That(outer.Entries.Count, Is.EqualTo(2));
        Assert.That(outer.Entries[0].Key, Is.EqualTo("inner"));
        Assert.That(outer.Entries[0].Value, Is.TypeOf<KtavObject>());
        Assert.That(outer.Entries[1].Key, Is.EqualTo("tail"));
        Assert.That(outer.Entries[1].Value, Is.EqualTo(KtavInteger.Of(2)));
        var inner = (KtavObject)outer.TryGet("inner")!;
        Assert.That(inner.Entries.Count, Is.EqualTo(2));
        Assert.That(inner.Entries[0].Key, Is.EqualTo("first"));
        Assert.That(inner.Entries[0].Value, Is.EqualTo(KtavInteger.Of(1)));
        Assert.That(inner.Entries[1].Key, Is.EqualTo("last"));
        Assert.That(inner.Entries[1].Value, Is.EqualTo(new KtavString("kept")));
        Assert.That(inner.TryGet("remove"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveMissingObjectMemberReportsNotFound(bool segments)
    {
        const string source = "root: { present: 1 }\n";
        var error = Assert.Throws<KtavException>(() =>
        {
            if (segments) Ktav.RemoveValueSegments(source, new[] { "root", "missing" }, Pure);
            else Ktav.RemoveValue(source, "root.missing", Pure);
        });
        Assert.That(error!.Error, Is.EqualTo("EditNotFound"));
    }

    [Test]
    public void RemoveContainerWithCommentsIsUnsupported()
    {
        const string source = "a: {\n  ## nested comment\n  b: 1\n}\n";
        var error = Assert.Throws<KtavException>(() => Ktav.RemoveValue(source, "a", Pure));
        Assert.That(error!.Error, Is.EqualTo("EditUnsupported"));
    }
}
