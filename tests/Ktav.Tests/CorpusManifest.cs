using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Ktav.Tests;

internal sealed record CorpusFixture(string Path, HashSet<string> Flags);

internal sealed class CorpusManifest
{
    internal static readonly IReadOnlyDictionary<string, int> PinnedCounts =
        new Dictionary<string, int>
        {
            ["valid"] = 223,
            ["invalid"] = 74,
            ["unrepresentable"] = 5,
            ["parseable-unrepresentable"] = 4,
            ["strict-lossy"] = 13,
        };

    internal static readonly HashSet<string> Categories = new(PinnedCounts.Keys, StringComparer.Ordinal);
    internal static readonly HashSet<string> KnownFlags = new(StringComparer.Ordinal) { "raw_bytes" };

    private CorpusManifest(Dictionary<string, List<CorpusFixture>> categories) => CategoriesByName = categories;

    internal Dictionary<string, List<CorpusFixture>> CategoriesByName { get; }

    internal static CorpusManifest Parse(byte[] bytes, string root,
        IReadOnlyDictionary<string, int>? expectedCounts = null)
    {
        string json = new UTF8Encoding(false, true).GetString(bytes);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        ValidateNoDuplicateProperties(doc.RootElement, "$" );
        var top = Object(doc.RootElement, "$", "$comment", "schema_version", "categories", "fixture_flags");
        if (top.TryGetProperty("$comment", out var comment) && comment.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("manifest $comment must be a string");
        if (!top.TryGetProperty("schema_version", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int schema) || schema != 1)
            throw new InvalidDataException("manifest schema_version must be exactly 1");

        if (!top.TryGetProperty("categories", out var categoryElement))
            throw new InvalidDataException("manifest categories is required");
        var categoryObject = Object(categoryElement, "categories", PinnedCounts.Keys.ToArray());
        var expected = expectedCounts ?? PinnedCounts;
        var declaredCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string category in PinnedCounts.Keys)
        {
            if (!categoryObject.TryGetProperty(category, out var categoryValue))
                throw new InvalidDataException($"missing manifest category '{category}'");
            var fields = Object(categoryValue, $"categories.{category}", "count");
            if (!fields.TryGetProperty("count", out var countElement) || countElement.ValueKind != JsonValueKind.Number ||
                !countElement.TryGetInt32(out int count) || count < 0)
                throw new InvalidDataException($"categories.{category}.count must be a nonnegative integer");
            if (!expected.TryGetValue(category, out int expectedCount) || count != expectedCount)
                throw new InvalidDataException($"manifest category '{category}' declares {count} fixtures; expected {expectedCount}");
            declaredCounts.Add(category, count);
        }

        if (!top.TryGetProperty("fixture_flags", out var flagsElement) || flagsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("manifest fixture_flags must be an array");
        var flagsByFixture = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in flagsElement.EnumerateArray())
        {
            var fields = Object(entry, "fixture_flags[]", "category", "fixture", "flags", "note");
            string category = RequiredString(fields, "category", "fixture_flags[]");
            if (!Categories.Contains(category)) throw new InvalidDataException($"unknown fixture flag category '{category}'");
            string fixture = RequiredString(fields, "fixture", $"fixture_flags[{category}]");
            ValidateFixtureStem(fixture);
            if (fields.TryGetProperty("note", out var note) && note.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"fixture flag note must be a string: {category}/{fixture}");
            if (!fields.TryGetProperty("flags", out var values) || values.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"fixture flags must be an array: {category}/{fixture}");
            if (values.GetArrayLength() == 0)
                throw new InvalidDataException($"fixture flags must not be empty: {category}/{fixture}");
            string key = FixtureKey(category, fixture);
            if (!flagsByFixture.TryAdd(key, new HashSet<string>(StringComparer.Ordinal)))
                throw new InvalidDataException($"duplicate fixture_flags entry: {category}/{fixture}");
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException($"fixture flag must be a string: {category}/{fixture}");
                string flag = value.GetString()!;
                if (!KnownFlags.Contains(flag)) throw new InvalidDataException($"unknown flag '{flag}': {category}/{fixture}");
                if (!flagsByFixture[key].Add(flag)) throw new InvalidDataException($"duplicate flag '{flag}': {category}/{fixture}");
            }
        }

        return ValidateCorpus(root, declaredCounts, flagsByFixture);
    }

    internal static CorpusManifest ValidatePinned(string root)
    {
        string manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("spec corpus manifest missing", manifestPath);
        return Parse(File.ReadAllBytes(manifestPath), root);
    }

    private static CorpusManifest ValidateCorpus(string root, Dictionary<string, int> counts,
        Dictionary<string, HashSet<string>> flagsByFixture)
    {
        var rootEntries = Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        var expectedRootEntries = new HashSet<string>(Categories, StringComparer.Ordinal)
        {
            "manifest.json",
            "boundary-fixtures.json",
        };
        if (!rootEntries.SetEquals(expectedRootEntries))
            throw new InvalidDataException("corpus root entries must be manifest.json, boundary-fixtures.json, and the five categories");

        var categories = new Dictionary<string, List<CorpusFixture>>(StringComparer.Ordinal);
        foreach (string category in PinnedCounts.Keys)
        {
            string directory = Path.Combine(root, category);
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(p => Path.GetRelativePath(directory, p).Replace('\\', '/'), StringComparer.Ordinal);
            var primary = files.Keys.Where(p => IsPrimary(category, p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
            var stems = primary.Select(Stem).ToHashSet(StringComparer.Ordinal);
            foreach (string key in flagsByFixture.Keys.Where(k => k.StartsWith(category + '\0', StringComparison.Ordinal)))
            {
                string flaggedStem = key[(category.Length + 1)..];
                if (!stems.Contains(flaggedStem))
                    throw new InvalidDataException($"dangling fixture_flags entry: {category}/{flaggedStem}");
            }
            if (primary.Count != counts[category])
                throw new InvalidDataException($"category '{category}' has {primary.Count} fixtures; expected {counts[category]}");

            var allowedFiles = new HashSet<string>(StringComparer.Ordinal);
            var fixtures = new List<CorpusFixture>();
            foreach (string path in primary)
            {
                string stem = Stem(path);
                string key = FixtureKey(category, stem);
                var fixtureFlags = flagsByFixture.TryGetValue(key, out var declaredFlags)
                    ? declaredFlags
                    : new HashSet<string>(StringComparer.Ordinal);
                if (category == "unrepresentable")
                {
                    allowedFiles.Add(path);
                }
                else
                {
                    allowedFiles.Add(path);
                    string oracle = stem + ".json";
                    if (!files.ContainsKey(oracle)) throw new InvalidDataException($"oracle companion missing: {category}/{oracle}");
                    allowedFiles.Add(oracle);
                    if (category == "valid")
                    {
                        string canonical = stem + ".canonical.ktav";
                        if (!files.ContainsKey(canonical)) throw new InvalidDataException($"canonical companion missing: {category}/{canonical}");
                        allowedFiles.Add(canonical);
                    }
                }

                bool isRaw = fixtureFlags.Contains("raw_bytes");
                if (isRaw && category != "invalid")
                    throw new InvalidDataException($"raw_bytes only applies to invalid primary inputs: {category}/{stem}");
                var fixtureFiles = category == "unrepresentable"
                    ? new[] { path }
                    : category == "valid"
                        ? new[] { path, stem + ".json", stem + ".canonical.ktav" }
                        : new[] { path, stem + ".json" };
                foreach (string fixtureFile in fixtureFiles)
                {
                    bool rawInput = isRaw && fixtureFile == path;
                    if (rawInput == IsStrictUtf8(File.ReadAllBytes(files[fixtureFile])))
                        throw new InvalidDataException(rawInput
                            ? $"raw_bytes fixture is valid UTF-8: {category}/{fixtureFile}"
                            : $"fixture file is malformed UTF-8: {category}/{fixtureFile}");
                }
                fixtures.Add(new CorpusFixture(path, fixtureFlags));
            }

            if (!allowedFiles.SetEquals(files.Keys))
                throw new InvalidDataException($"unknown, dangling, or missing companion file in category '{category}'");
            categories.Add(category, fixtures);
        }

        ValidateBoundaryMetadata(Path.Combine(root, "boundary-fixtures.json"), categories["valid"]);
        return new CorpusManifest(categories);
    }

    private static void ValidateBoundaryMetadata(string path, List<CorpusFixture> validFixtures)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string json;
        try { json = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("boundary metadata is not valid UTF-8", ex); }
        using var doc = JsonDocument.Parse(json);
        ValidateNoDuplicateProperties(doc.RootElement, "boundary-fixtures");
        var root = Object(doc.RootElement, "boundary-fixtures", "$comment", "boundary_dependent_leaves");
        if (root.TryGetProperty("$comment", out var comment) && comment.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("boundary metadata $comment must be a string");
        if (!root.TryGetProperty("boundary_dependent_leaves", out var leaves) || leaves.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("boundary_dependent_leaves must be an array");
        var fixtureStems = validFixtures.Select(f => Stem(f.Path)).ToHashSet(StringComparer.Ordinal);
        foreach (var leaf in leaves.EnumerateArray())
        {
            var fields = Object(leaf, "boundary_dependent_leaves[]", "fixture", "path", "boundary_class");
            string fixture = RequiredString(fields, "fixture", "boundary_dependent_leaves[]");
            string pointer = RequiredString(fields, "path", $"boundary_dependent_leaves[{fixture}]");
            string boundaryClass = RequiredString(fields, "boundary_class", $"boundary_dependent_leaves[{fixture}]");
            if (!fixtureStems.Contains(fixture) || !pointer.StartsWith("/", StringComparison.Ordinal) ||
                boundaryClass is not ("integer_range" or "float_range" or "float_underflow" or "float_precision"))
                throw new InvalidDataException($"invalid boundary metadata entry: {fixture}");
        }
    }

    private static bool IsPrimary(string category, string path) => category switch
    {
        "unrepresentable" => path.EndsWith(".json", StringComparison.Ordinal),
        "valid" => path.EndsWith(".ktav", StringComparison.Ordinal) && !path.EndsWith(".canonical.ktav", StringComparison.Ordinal),
        _ => path.EndsWith(".ktav", StringComparison.Ordinal),
    };

    private static string Stem(string path) =>
        Path.ChangeExtension(path, null) ?? path;

    private static string FixtureKey(string category, string fixture) => category + '\0' + fixture;

    private static string RequiredString(JsonElement element, string name, string at)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(value.GetString()))
            throw new InvalidDataException($"{at}.{name} must be a non-empty string");
        return value.GetString()!;
    }

    private static void ValidateFixtureStem(string fixture)
    {
        if (fixture.Contains('\\') || Path.IsPathRooted(fixture) ||
            fixture.Split('/').Any(part => part.Length == 0 || part == "." || part == "..") ||
            fixture.EndsWith(".ktav", StringComparison.Ordinal) || fixture.EndsWith(".json", StringComparison.Ordinal))
            throw new InvalidDataException($"invalid fixture stem: {fixture}");
    }

    private static bool IsStrictUtf8(byte[] bytes)
    {
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private static JsonElement Object(JsonElement value, string at, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{at} must be an object");
        var allow = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!allow.Contains(property.Name)) throw new InvalidDataException($"unknown field '{property.Name}' at {at}");
        return value;
    }

    private static void ValidateNoDuplicateProperties(JsonElement value, string at)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"duplicate field '{property.Name}' at {at}");
                ValidateNoDuplicateProperties(property.Value, at + "." + property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in value.EnumerateArray()) ValidateNoDuplicateProperties(item, at + "[" + index++ + "]");
        }
    }
}
