#!/usr/bin/env python3
"""Standalone-consumer nupkg gate for the pure net8.0 Ktav assembly.

Usage: standalone_pkg_check.py <path.nupkg> <out-dir>

Asserts, using the Python standard library only:
  * exactly one .nuspec exists in the package;
  * the .nuspec <dependencies> contains EXACTLY ONE dependency group whose
    targetFramework is exactly "net8.0" (case-insensitive, whitespace
    stripped; lookalike TFMs such as net8.0-windows or net80 do NOT match)
    and that group carries ZERO <dependency> elements;
  * no <dependency> element is a DIRECT child of <dependencies> (ungrouped);
  * no unexpected/malformed child elements under <dependencies> or inside
    the net8.0 group;
  * other groups (e.g. netstandard2.0 with System.Memory/System.Text.Json)
    may only contain <dependency> children;
  * the archive contains EXACTLY ONE entry named lib/net8.0/Ktav.dll;
then extracts exactly that single entry (and nothing else) to
<out-dir>/Ktav.dll, created exclusively (an existing file is never
overwritten). No other archive entries are read or written, and no
directories are deleted.
"""
import os
import sys
import xml.etree.ElementTree as ET
import zipfile

REQUIRED_ENTRY = "lib/net8.0/Ktav.dll"
NET8_TFM = "net8.0"


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def check_nuspec(raw: bytes) -> None:
    root = ET.fromstring(raw)
    if local_name(root.tag) != "package":
        raise SystemExit("nuspec root element is not <package>")
    deps_list = [e for e in root.iter() if local_name(e.tag) == "dependencies"]
    if len(deps_list) != 1:
        raise SystemExit("expected exactly one <dependencies>, got %d" % len(deps_list))
    deps = deps_list[0]

    net8_groups = []
    for child in deps:
        name = local_name(child.tag)
        if name == "group":
            tfm = (child.get("targetFramework") or "").strip().lower()
            if tfm == NET8_TFM:
                net8_groups.append(child)
            else:
                # Other groups (e.g. netstandard2.0): only <dependency> children.
                for sub in child:
                    if local_name(sub.tag) != "dependency":
                        raise SystemExit(
                            "unexpected element <%s> in %s dependency group"
                            % (local_name(sub.tag), tfm or "(no tfm)")
                        )
        elif name == "dependency":
            dep_id = (child.get("id") or "").strip()
            raise SystemExit("ungrouped direct <dependency> '%s'" % dep_id)
        else:
            raise SystemExit("unexpected element <%s> under <dependencies>" % name)

    if len(net8_groups) != 1:
        raise SystemExit(
            "expected exactly one net8.0 dependency group, got %d" % len(net8_groups)
        )
    if len(net8_groups[0]) != 0:
        for dep in net8_groups[0]:
            raise SystemExit(
                "dependency '%s' present in net8.0 group; zero required"
                % ((dep.get("id") or "").strip())
            )


def is_safe_entry(name: str) -> bool:
    if name.startswith("/") or "\\" in name or ":" in name:
        return False
    parts = name.split("/")
    return ".." not in parts and all(p for p in parts)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    pkg_path, out_dir = sys.argv[1], sys.argv[2]
    with zipfile.ZipFile(pkg_path) as zf:
        names = zf.namelist()
        nuspecs = [n for n in names if n.endswith(".nuspec") and "/" not in n]
        if len(nuspecs) != 1:
            raise SystemExit("expected exactly one root .nuspec, got %r" % nuspecs)
        if not is_safe_entry(nuspecs[0]):
            raise SystemExit("unsafe nuspec entry name: %r" % nuspecs[0])
        required = [n for n in names if n == REQUIRED_ENTRY]
        if len(required) != 1:
            raise SystemExit(
                "expected exactly one %s entry, got %d" % (REQUIRED_ENTRY, len(required))
            )
        check_nuspec(zf.read(nuspecs[0]))
        info = zf.getinfo(REQUIRED_ENTRY)
        if not is_safe_entry(info.filename):
            raise SystemExit("unsafe entry name: %r" % info.filename)
        data = zf.read(info)
    out_path = os.path.join(out_dir, "Ktav.dll")
    fd = os.open(out_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o644)
    with os.fdopen(fd, "wb") as fh:
        fh.write(data)
    print("extracted %s (%d bytes) -> %s" % (REQUIRED_ENTRY, len(data), out_path))


if __name__ == "__main__":
    main()
