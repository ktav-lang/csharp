#!/usr/bin/env node
// Rebuilds this repository's README/CHANGELOG/CONTRIBUTING/SECURITY from
// root-docs/, using @ktav-lang/polydoc. Run with --check for a
// CI-friendly, read-only verification instead of regenerating the files.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { configure, buildRootDocs } from '@ktav-lang/polydoc';

const LANGS = ['en', 'ru', 'zh'];
configure({
  langs: LANGS,
  rootDocuments: ['README', 'CHANGELOG', 'CONTRIBUTING', 'SECURITY'],
});

// Unlike the Rust repo, whose generated artifacts sit at the repository
// root, this one keeps the translations under docs/ and only the English
// artifacts at the root (README.md is packed into the NuGet package via
// <PackageReadmeFile>, so it has to stay where MSBuild expects it).
// polydoc's own writeRootDocs/checkRootDocs hardcode `<repoRoot>/<DOC>.md`
// and `<repoRoot>/<DOC>.<lang>.md`, so this repository writes and compares
// over the map below instead. Everything else — the assembly, the
// release-token substitution, the structural parity check — is polydoc's.
const OUTPUT_PATHS = {
  README:       { en: 'README.md',             ru: 'docs/ru/README.ru.md',       zh: 'docs/zh/README.zh.md' },
  CHANGELOG:    { en: 'CHANGELOG.md',          ru: 'docs/ru/CHANGELOG.ru.md',    zh: 'docs/zh/CHANGELOG.zh.md' },
  CONTRIBUTING: { en: 'docs/CONTRIBUTING.md',  ru: 'docs/ru/CONTRIBUTING.ru.md', zh: 'docs/zh/CONTRIBUTING.zh.md' },
  SECURITY:     { en: 'docs/SECURITY.md',      ru: 'docs/ru/SECURITY.ru.md',     zh: 'docs/zh/SECURITY.zh.md' },
};

// SECURITY.md's supported-version line uses @@MINOR_LINE@@ so it can
// never quietly fall behind src/Ktav/Ktav.csproj's <Version> the way a
// hand-written "0.1.x" once did.
function readPackageVersion(root) {
  const csproj = fs.readFileSync(path.join(root, 'src', 'Ktav', 'Ktav.csproj'), 'utf8');
  const match = csproj.match(/<Version>([^<]+)<\/Version>/u);
  if (!match) throw new Error('src/Ktav/Ktav.csproj: no <Version> element found');
  return match[1];
}

// Per-unit validation proves every meaning has every language. It does
// NOT prove the languages describe the same DOCUMENT: a heading demoted
// from ## to ### in one translation, or an extra heading in another,
// passes unit validation untouched. This check (originally from this
// repo's own pre-polydoc docs-gen tool) is what catches that class of
// drift.
function headingSkeleton(markdown) {
  const levels = [];
  let fenceChar = null;
  let fenceLen = 0;
  for (const line of markdown.split('\n')) {
    const fence = line.match(/^\s{0,3}(`{3,}|~{3,})/u);
    if (fence) {
      const char = fence[1][0];
      const len = fence[1].length;
      if (fenceChar === null) { fenceChar = char; fenceLen = len; }
      else if (char === fenceChar && len >= fenceLen) { fenceChar = null; }
      continue;
    }
    if (fenceChar !== null) continue;
    const heading = line.match(/^(#{1,6})\s+\S/u);
    if (heading) levels.push(heading[1].length);
  }
  return levels;
}

function structuralProblems(label, perLang) {
  const [reference, ...others] = LANGS;
  const base = headingSkeleton(perLang.get(reference).toString('utf8'));
  const problems = [];
  for (const lang of others) {
    const other = headingSkeleton(perLang.get(lang).toString('utf8'));
    if (other.length !== base.length) {
      problems.push(`${label}: ${lang} has ${other.length} heading(s) but ${reference} has ` +
        `${base.length} — the translations describe different documents`);
      continue;
    }
    const at = base.findIndex((level, i) => level !== other[i]);
    if (at !== -1) {
      problems.push(`${label}: heading #${at + 1} is level ${other[at]} in ${lang} but ` +
        `level ${base[at]} in ${reference}`);
    }
  }
  return problems;
}

/// Write every built document to its OUTPUT_PATHS target. Same effect as
/// polydoc's writeRootDocs, but the destination is this repo's own layout.
function writeDocs(root, docs) {
  for (const [doc, perLang] of docs) {
    for (const lang of LANGS) {
      const target = path.join(root, OUTPUT_PATHS[doc][lang]);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, perLang.get(lang));
    }
  }
}

/// Compare every built document against what is on disk. Same report as
/// polydoc's checkRootDocs — byte offset and 1-based line of the first
/// divergence, and a pointer back at the unit tree — over OUTPUT_PATHS.
/// Returns a list of human-readable divergences; empty means identical.
function checkDocs(root, docs) {
  const problems = [];
  for (const [doc, perLang] of docs) {
    for (const lang of LANGS) {
      const name = OUTPUT_PATHS[doc][lang];
      const expected = perLang.get(lang);
      let actual;
      try {
        actual = fs.readFileSync(path.join(root, name));
      } catch (e) {
        problems.push(`${name} is missing or unreadable (${e.message}); it is generated from ` +
          `root-docs/${doc}/`);
        continue;
      }
      if (actual.equals(expected)) continue;
      let off = 0;
      const min = Math.min(actual.length, expected.length);
      while (off < min && actual[off] === expected[off]) off++;
      const line = expected.subarray(0, off).toString('utf8').split('\n').length;
      problems.push(
        `${name} differs from what root-docs/${doc}/ generates, first at byte ${off} ` +
        `(line ${line}); edit the unit source, never the generated file`);
    }
  }
  return problems;
}

function usage() {
  process.stderr.write(
    'usage: node scripts/build-docs.mjs [--check]\n' +
    '  (no args)  regenerate README.md/.ru.md/.zh.md and CHANGELOG.md/.ru.md/.zh.md,\n' +
    '             plus docs/CONTRIBUTING.md, docs/SECURITY.md and their translations\n' +
    '  --check    verify the generated files match root-docs/ without writing\n'
  );
}

function cli() {
  const scriptDir = path.dirname(fileURLToPath(import.meta.url));
  const root = path.resolve(scriptDir, '..');

  const args = process.argv.slice(2);
  if (args.includes('-h') || args.includes('--help')) { usage(); process.exit(0); }
  if (args.length > 1 || (args.length === 1 && args[0] !== '--check')) {
    usage();
    process.exit(1);
  }
  const checkMode = args[0] === '--check';

  let docs;
  try {
    const version = readPackageVersion(root);
    docs = buildRootDocs(root, { release: { version, released: '' } });
    for (const [name, perLang] of docs) {
      const problems = structuralProblems(name, perLang);
      if (problems.length > 0) {
        throw new Error(problems.join('\n'));
      }
    }
  } catch (e) {
    process.stderr.write(`build-docs: ${e.message}\n`);
    process.exit(1);
  }

  if (!checkMode) {
    writeDocs(root, docs);
    process.stdout.write(`build-docs: assembled ${docs.size} document(s) from root-docs/\n`);
    process.exit(0);
  }

  const problems = checkDocs(root, docs);
  if (problems.length > 0) {
    for (const problem of problems) {
      process.stderr.write(`build-docs --check: ${problem}\n`);
    }
    process.exit(1);
  }
  process.exit(0);
}

const isMain = process.argv[1] !== undefined &&
  import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isMain) cli();
