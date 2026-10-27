using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Ktav;
using KtavApi = global::Ktav.Ktav;

internal static class Program
{
    private const int WarmupIterations = 200;
    private const int MeasurementIterations = 2000;
    private const int SourceEntries = 64;
    private const string NewKey = "inserted";
    private const string RenameFrom = "key31";
    private const string RenameTo = "renamed";
    private static readonly string Source = MakeSource();
    private static readonly KtavString InsertValue = new KtavString("payload");

    private sealed class Case
    {
        public readonly string Name, Source, Expected;
        public readonly Func<KtavBackend, string> Action;
        public readonly Action<string, KtavBackend> Oracle;
        public Case(string name, string source, string expected, Func<KtavBackend, string> action, Action<string, KtavBackend> oracle)
        { Name = name; Source = source; Expected = expected; Action = action; Oracle = oracle; }
    }

    private static readonly Case[] Cases = CreateCases();

    private static int Main(string[] args)
    {
        string backendOption = "both";
        int iterations = MeasurementIterations;
        foreach (string arg in args)
        {
            if (arg.StartsWith("--backend=")) backendOption = arg.Substring("--backend=".Length);
            else if (arg == "--help") { Help(); return 0; }
            else if (!int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < 1)
            { Console.Error.WriteLine("Expected positive iterations and --backend=pure|native|both"); return 2; }
        }
        if (backendOption != "both" && backendOption != "pure" && backendOption != "native")
        { Console.Error.WriteLine("--backend must be pure, native, or both"); return 2; }
        Console.WriteLine("case,backend,iterations,total_ms,ns_per_op,source_bytes");
        KtavBackend[] backends = backendOption == "pure" ? new[] { KtavBackend.Pure } : backendOption == "native" ? new[] { KtavBackend.Native } : new[] { KtavBackend.Native, KtavBackend.Pure };
        foreach (var backend in backends) foreach (var item in Cases) Run(item, backend, iterations);
        return 0;
    }

    private static void Help()
    {
        Console.WriteLine("dotnet run -c Release --project benchmarks/Mutations -- [iterations] [--backend=pure|native|both]");
        Console.WriteLine("Iterations default to 2000; backend defaults to both.");
    }

    private static void Run(Case item, KtavBackend backend, int iterations)
    {
        item.Oracle(item.Expected, backend);
        string actual = item.Action(backend);
        if (!String.Equals(actual, item.Expected, StringComparison.Ordinal)) throw new InvalidOperationException(item.Name + " exact-source oracle failed for " + backend);
        item.Oracle(actual, backend);
        for (int i = 0; i < WarmupIterations; i++) item.Action(backend);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) actual = item.Action(backend);
        timer.Stop();
        if (!String.Equals(actual, item.Expected, StringComparison.Ordinal)) throw new InvalidOperationException(item.Name + " final result failed for " + backend);
        item.Oracle(actual, backend);
        double ns = timer.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
        Console.WriteLine(string.Join(",", item.Name, backend.ToString(), iterations.ToString(CultureInfo.InvariantCulture), timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture), ns.ToString("F1", CultureInfo.InvariantCulture), Encoding.UTF8.GetByteCount(item.Source).ToString(CultureInfo.InvariantCulture)));
    }

    private static Case[] CreateCases()
    {
        string arraySource = "## root leading\n## top\na: {b: [one, two]}\n## tail\nz: 3\n";
        string arrayExpected = "## root leading\n## top\na: {b: [one]}\n## tail\nz: 3\n";
        string quotedSource = "## root leading\n\"quoted.key\": 1\nother: {keep: yes}\n";
        string quotedExpected = "## root leading\nrenamed: 1\nother: {keep: yes}\n";
        string nestedSource = "a: {\n    x: 1\n}\n";
        var nestedValue = new KtavObject(new[] { new System.Collections.Generic.KeyValuePair<string, KtavValue>("text", new KtavString("    four\n      six")) });
        string nestedExpected = "a: {\n    x: 1\n    value: {\n        text: ((\n    four\n      six\n        ))\n    }\n}\n";
        string crlfSource = "a: {\r\n    existing: old\r\n}\r\n";
        var crlfValue = (KtavObject)KtavApi.Loads("keep: yes\n", KtavBackend.Pure);
        string crlfExpected = "a: {\r\n    existing: old\r\n    added: {\r\n        keep: yes\r\n    }\r\n}\r\n";
        return new[] {
            new Case("wide-insert", Source, Source + "inserted: payload\n", b => KtavApi.InsertValue(Source, NewKey, InsertValue, b), CheckWideInsert),
            new Case("wide-remove", Source, Source.Replace("key31: value31\n", ""), b => KtavApi.RemoveValue(Source, "key31", b), CheckWideRemove),
            new Case("wide-rename", Source, Source.Replace("key31: value31\n", "renamed: value31\n"), b => KtavApi.RenameKey(Source, RenameFrom, RenameTo, b), CheckWideRename),
            new Case("array-remove-comment", arraySource, arrayExpected, b => KtavApi.RemoveValue(arraySource, "a.b.1", b), CheckArrayRemove),
            new Case("quoted-key-rename", quotedSource, quotedExpected, b => KtavApi.RenameKey(quotedSource, @"quoted\.key", "renamed", b), CheckQuotedRename),
            new Case("nested-insert", nestedSource, nestedExpected, b => KtavApi.InsertValue(nestedSource, "a.value", nestedValue, b), CheckNested),
            new Case("nested-insert-crlf", crlfSource, crlfExpected, b => KtavApi.InsertValue(crlfSource, "a.added", crlfValue, b), CheckCrlf)
        };
    }
    private static void CheckWideInsert(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); if(r.Entries.Count!=SourceEntries+1 || !Equals(r.Entries[SourceEntries].Key,NewKey) || !Equals(r.TryGet(NewKey),InsertValue)) throw new InvalidOperationException("wide insert semantics"); for(int i=0;i<SourceEntries;i++) if(!Equals(r.Entries[i].Key,"key"+i) || !Equals(r.TryGet("key"+i),new KtavString("value"+i))) throw new InvalidOperationException("wide insert entries"); }
    private static void CheckWideRemove(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); if(r.Entries.Count!=SourceEntries-1 || r.TryGet("key31")!=null) throw new InvalidOperationException("wide remove semantics"); for(int i=0,j=0;i<SourceEntries;i++) if(i!=31) { if(!Equals(r.Entries[j].Key,"key"+i) || !Equals(r.TryGet("key"+i),new KtavString("value"+i))) throw new InvalidOperationException("wide remove entries"); j++; } }
    private static void CheckWideRename(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); if(r.Entries.Count!=SourceEntries || r.TryGet("key31")!=null) throw new InvalidOperationException("wide rename semantics"); for(int i=0;i<SourceEntries;i++) { string key=i==31?RenameTo:"key"+i; if(!Equals(r.Entries[i].Key,key) || !Equals(r.TryGet(key),new KtavString("value"+i))) throw new InvalidOperationException("wide rename entries"); } }
    private static void CheckArrayRemove(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); var a=(KtavObject)r.TryGet("a")!; var b=(KtavArray)a.TryGet("b")!; if(r.Entries.Count!=2 || !Equals(r.Entries[0].Key,"a") || !Equals(r.Entries[1].Key,"z") || a.Entries.Count!=1 || !Equals(a.Entries[0].Key,"b") || b.Items.Count!=1 || !Equals(((KtavString)b.Items[0]).Value,"one") || !Equals(r.TryGet("z"),KtavInteger.Of(3))) throw new InvalidOperationException("array removal semantics"); }
    private static void CheckQuotedRename(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); var sibling=(KtavObject)r.TryGet("other")!; if(r.Entries.Count!=2 || !Equals(r.Entries[0].Key,"renamed") || !Equals(r.Entries[1].Key,"other") || !Equals(r.TryGet("quoted.key"),null) || !Equals(r.TryGet("renamed"),KtavInteger.Of(1)) || sibling.Entries.Count!=1 || !Equals(sibling.Entries[0].Key,"keep") || !Equals(sibling.TryGet("keep"),new KtavString("yes"))) throw new InvalidOperationException("quoted rename semantics"); }
    private static void CheckNested(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); var a=(KtavObject)r.TryGet("a")!; var ins=(KtavObject)a.TryGet("value")!; if(r.Entries.Count!=1 || !Equals(r.Entries[0].Key,"a") || a.Entries.Count!=2 || !Equals(a.Entries[0].Key,"x") || !Equals(a.Entries[1].Key,"value") || !Equals(((KtavInteger)a.TryGet("x")!).ToInt64(),1L) || ins.Entries.Count!=1 || !Equals(ins.Entries[0].Key,"text") || !Equals(((KtavString)ins.TryGet("text")!).Value,"    four\n      six")) throw new InvalidOperationException("nested insert semantics"); }
    private static void CheckCrlf(string t, KtavBackend backend) { var r=(KtavObject)KtavApi.Loads(t,KtavBackend.Pure); var a=(KtavObject)r.TryGet("a")!; var added=(KtavObject)a.TryGet("added")!; if(r.Entries.Count!=1 || !Equals(r.Entries[0].Key,"a") || a.Entries.Count!=2 || !Equals(a.Entries[0].Key,"existing") || !Equals(a.Entries[1].Key,"added") || !Equals(((KtavString)a.TryGet("existing")!).Value,"old") || added.Entries.Count!=1 || !Equals(((KtavString)added.TryGet("keep")!).Value,"yes")) throw new InvalidOperationException("CRLF nested semantics"); }
    private static string MakeSource() { var t=new StringBuilder(); for(int i=0;i<SourceEntries;i++) t.Append("key").Append(i).Append(": value").Append(i).Append('\n'); return t.ToString(); }
}
