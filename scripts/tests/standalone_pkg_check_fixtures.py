#!/usr/bin/env python3
"""CI-only negative/positive controls for scripts/standalone_pkg_check.py.

Stdlib only. Builds tiny in-memory nupkg fixtures (metadata fixtures with
dummy DLL bytes -- NOT product builds) in a temporary directory, then runs
the checker via subprocess and asserts the expected pass/fail outcome.

Usage: standalone_pkg_check_fixtures.py  (exit 0 iff all controls behave)
"""
import os
import subprocess
import sys
import tempfile
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
CHECKER = os.path.join(HERE, os.pardir, "standalone_pkg_check.py")
DUMMY_DLL = b"PK-FIXTURE-DUMMY-DLL-NOT-A-REAL-BINARY"

NETSTD_GROUP = (
    '<group targetFramework="netstandard2.0">'
    '<dependency id="System.Memory" version="4.5.5" />'
    '<dependency id="System.Text.Json" version="8.0.0" />'
    "</group>"
)


def nuspec_xml(groups: str = NETSTD_GROUP, ungrouped: str = "") -> bytes:
    return (
        '<?xml version="1.0"?><package><metadata>'
        "<id>Ktav</id><version>1.0.0</version>"
        "<dependencies>%s%s</dependencies>"
        "</metadata></package>" % (groups, ungrouped)
    ).encode()


def build_fixture(path: str, nuspec: bytes, extra_entries=None) -> None:
    with zipfile.ZipFile(path, "w") as zf:
        zf.writestr("Ktav.nuspec", nuspec)
        zf.writestr("lib/net8.0/Ktav.dll", DUMMY_DLL)
        for name, data in (extra_entries or {}).items():
            zf.writestr(name, data)


def net8_group(deps: str = "") -> str:
    return '<group targetFramework="net8.0">%s</group>' % deps


CASES = [
    ("valid: empty net8.0 group + netstandard2.0 deps",
     lambda p: build_fixture(p, nuspec_xml(NETSTD_GROUP + net8_group())), True),
    ("missing net8.0 group",
     lambda p: build_fixture(p, nuspec_xml()), False),
    ("duplicate net8.0 groups",
     lambda p: build_fixture(p, nuspec_xml(net8_group() + net8_group())), False),
    ("ungrouped direct <dependency>",
     lambda p: build_fixture(
         p, nuspec_xml(net8_group(),
                       ungrouped='<dependency id="Foo.Bar" version="1.0.0" />')), False),
    ("net8.0 group with System.Text.Json dep",
     lambda p: build_fixture(
         p, nuspec_xml(net8_group('<dependency id="System.Text.Json" version="8.0.0" />'))),
     False),
    ("net8.0 group with vendor dep",
     lambda p: build_fixture(
         p, nuspec_xml(net8_group('<dependency id="Vendor.Thing" version="1.2.3" />'))),
     False),
    ("lookalike TFM net8.0-windows only (missing net8.0 group)",
     lambda p: build_fixture(
         p, nuspec_xml('<group targetFramework="net8.0-windows"></group>')), False),
    ("lookalike TFM net80 only (missing net8.0 group)",
     lambda p: build_fixture(p, nuspec_xml('<group targetFramework="net80"></group>')),
     False),
    ("duplicate lib/net8.0/Ktav.dll zip entry",
     lambda p: build_fixture(p, nuspec_xml(net8_group()),
                             {"lib/net8.0/Ktav.dll": DUMMY_DLL}), False),
    ("unexpected element under <dependencies>",
     lambda p: build_fixture(p, nuspec_xml(net8_group() + "<bogus/>")), False),
]


def main() -> None:
    failures = []
    with tempfile.TemporaryDirectory(prefix="ktav-pkgcheck-fixtures-") as tmp:
        for i, (label, make, should_pass) in enumerate(CASES):
            out_dir = os.path.join(tmp, "out%d" % i)
            os.mkdir(out_dir)
            pkg = os.path.join(tmp, "case%d.nupkg" % i)
            make(pkg)
            proc = subprocess.run(
                [sys.executable, CHECKER, pkg, out_dir], capture_output=True, text=True
            )
            ok = (proc.returncode == 0)
            if ok != should_pass:
                failures.append(
                    "%s: expected %s, checker exited %d (%s)"
                    % (label, "pass" if should_pass else "fail",
                       proc.returncode, (proc.stderr or proc.stdout).strip())
                )
            else:
                print("ok: %s" % label)
    if failures:
        sys.exit("FIXTURE CONTROLS FAILED:\n" + "\n".join(failures))
    print("all %d fixture controls behaved as expected" % len(CASES))


if __name__ == "__main__":
    main()
