// End-to-end demo: parse a Ktav document, pull out typed fields,
// pattern-match the sealed KtavValue hierarchy, then build a fresh
// document in code and render it back to Ktav text.
//
// Run from the repo root:
//
//   cargo build --release -p ktav-cabi
//   set KTAV_LIB_PATH=%CD%\target\release\ktav_cabi.dll        (cmd)
//   $env:KTAV_LIB_PATH = "$PWD\target\release\ktav_cabi.dll"   (PowerShell)
//   dotnet run --project examples/Basic -c Release

using System;
using System.Collections.Generic;
using System.Linq;

using Ktav;

const string Src = """
                   service: web
                   port:i 8080
                   ratio:f 0.75
                   tls: true
                   tags: [
                       prod
                       eu-west-1
                   ]
                   db.host: primary.internal
                   db.timeout:i 30
                   """;

var top = (KtavObject)Ktav.Ktav.Loads(Src);

// ── 1. Pull typed fields out of the parsed document ──────────────
string  service = ((KtavString)  top.TryGet("service")!).Value;
long    port    = ((KtavInteger) top.TryGet("port")!).ToInt64();
double  ratio   = ((KtavFloat)   top.TryGet("ratio")!).ToDouble();
bool    tls     = ((KtavBool)    top.TryGet("tls")!).Value;

var tags = ((KtavArray) top.TryGet("tags")!)
    .Items
    .Select(v => ((KtavString)v).Value)
    .ToList();

var db = (KtavObject) top.TryGet("db")!;
string dbHost   = ((KtavString)  db.TryGet("host")!).Value;
long   dbTimeout = ((KtavInteger) db.TryGet("timeout")!).ToInt64();

Console.WriteLine($"service={service} port={port} tls={tls} ratio={ratio:F2}");
Console.WriteLine($"tags=[{string.Join(", ", tags)}]");
Console.WriteLine($"db: {dbHost} (timeout={dbTimeout}s)");

// ── 2. Walk the document, dispatching on the KtavValue variant ───
Console.WriteLine("\nshape:");
foreach (var entry in top.Entries)
{
    string kind = entry.Value switch
    {
        KtavNull        => "null",
        KtavBool b      => $"bool={b.Value}",
        KtavInteger i   => $"int={i.Text}",
        KtavFloat f     => $"float={f.Text}",
        KtavString s    => $"str=\"{s.Value}\"",
        KtavArray a     => $"array({a.Items.Count})",
        KtavObject o    => $"object({o.Entries.Count})",
        _               => throw new InvalidOperationException(),
    };
    Console.WriteLine($"  {entry.Key,-12} -> {kind}");
}

// ── 3. Build a config in code, render it as Ktav text ────────────
var doc = new KtavObject(new[]
{
    new KeyValuePair<string, KtavValue>("name",      new KtavString("frontend")),
    new KeyValuePair<string, KtavValue>("port",      KtavInteger.Of(8443)),
    new KeyValuePair<string, KtavValue>("tls",       KtavBool.True),
    new KeyValuePair<string, KtavValue>("ratio",     KtavFloat.Of(0.95)),
    new KeyValuePair<string, KtavValue>("upstreams", new KtavArray(new KtavValue[]
    {
        Upstream("a.example", 1080),
        Upstream("b.example", 1080),
        Upstream("c.example", 1080),
    })),
    new KeyValuePair<string, KtavValue>("notes",     KtavNull.Instance),
});

string rendered = Ktav.Ktav.Dumps(doc);
Console.WriteLine("\n--- rendered ---");
Console.Write(rendered);

return;

static KtavObject Upstream(string host, long port) =>
    new(new[]
    {
        new KeyValuePair<string, KtavValue>("host", new KtavString(host)),
        new KeyValuePair<string, KtavValue>("port", KtavInteger.Of(port)),
    });
