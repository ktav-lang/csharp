"""Сверить actual NuGet native bytes и полные notices с проверенным staging."""

import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import struct
import sys
import xml.etree.ElementTree as ET
import zipfile

spec = importlib.util.spec_from_file_location('embedded_notices', Path(__file__).with_name('check_embedded_notices.py'))
embedded = importlib.util.module_from_spec(spec)
spec.loader.exec_module(embedded)

RIDS = {
    'linux-x64': ('libktav_cabi.so', 'elf', 62),
    'linux-arm64': ('libktav_cabi.so', 'elf', 183),
    'osx-x64': ('libktav_cabi.dylib', 'macho', 0x1000007),
    'osx-arm64': ('libktav_cabi.dylib', 'macho', 0x100000c),
    'win-x64': ('ktav_cabi.dll', 'pe', 0x8664),
    'win-arm64': ('ktav_cabi.dll', 'pe', 0xaa64),
}
MAGIC = (b'PK\x03\x04', b'\x7fELF', b'MZ', b'\x00asm', b'!<arch>\n', b'\xcf\xfa\xed\xfe', b'\xfe\xed\xfa\xcf', b'\xca\xfe\xba\xbe', b'\xbe\xba\xfe\xca')
MANAGED = {'lib/net8.0/Ktav.dll', 'lib/netstandard2.0/Ktav.dll'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def architecture(binary, kind, machine):
    if kind == 'elf':
        valid = len(binary) >= 20 and binary.startswith(b'\x7fELF') and binary[4:6] == b'\x02\x01' and struct.unpack_from('<H', binary, 18)[0] == machine
    elif kind == 'macho':
        valid = len(binary) >= 8 and binary.startswith(b'\xcf\xfa\xed\xfe') and struct.unpack_from('<I', binary, 4)[0] == machine
    else:
        valid = len(binary) >= 64 and binary.startswith(b'MZ')
        if valid:
            offset = struct.unpack_from('<I', binary, 60)[0]
            valid = offset + 6 <= len(binary) and binary[offset:offset + 4] == b'PE\0\0' and struct.unpack_from('<H', binary, offset + 4)[0] == machine
    if not valid:
        raise ValueError('NuGet native architecture or format mismatch')


def member(archive, name, limit=128 * 1024 * 1024):
    if archive.getinfo(name).file_size > limit:
        raise ValueError('NuGet member exceeds size limit')
    with archive.open(name) as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise ValueError('NuGet member exceeds read limit')
    return data


def verify(package, staging, project, source_ref):
    expected = {f'runtimes/{rid}/native/{name}' for rid, (name, _, _) in RIDS.items()}
    if package.stat().st_size > 256 * 1024 * 1024:
        raise ValueError('NuGet package exceeds size limit')
    records = []
    with zipfile.ZipFile(package) as archive:
        if len(archive.infolist()) > 30000 or sum(item.file_size for item in archive.infolist()) > 512 * 1024 * 1024:
            raise ValueError('NuGet package inventory exceeds limits')
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError('duplicate NuGet member')
        native = {name for name in names if name.startswith('runtimes/') and '/native/' in name and not name.endswith('/')}
        if native != expected:
            raise ValueError('unexpected NuGet native payload inventory')
        for name in names:
            if name.endswith('/'):
                continue
            if '..' in PurePosixPath(name).parts or '\\' in name or ':' in name or name.startswith('/'):
                raise ValueError('unsafe NuGet member path')
            data = member(archive, name)
            if (data.startswith(MAGIC) or name.endswith(('.dll', '.so', '.dylib', '.exe', '.a', '.pyd'))) and name not in expected | MANAGED:
                raise ValueError('hidden or unexpected NuGet executable payload')
        for name in MANAGED:
            if not member(archive, name).startswith(b'MZ'):
                raise ValueError('managed NuGet target missing or invalid')
        for name in ('LICENSE-MIT', 'LICENSE-APACHE'):
            if member(archive, name) != (project / name).read_bytes():
                raise ValueError('NuGet Ktav license bytes differ from source')
        for rid, (name, kind, machine) in RIDS.items():
            prefix = 'runtimes/' + rid + '/'
            binary = member(archive, prefix + 'native/' + name)
            notice = member(archive, prefix + 'NOTICE.json', 64 * 1024 * 1024)
            manifest = json.loads(member(archive, prefix + 'NOTICE.manifest.json', 1024 * 1024))
            if binary != (staging / rid / 'native' / name).read_bytes() or notice != (staging / rid / 'NOTICE.json').read_bytes():
                raise ValueError('NuGet native or notice bytes differ from verified staging')
            decoded, _ = embedded._extract(binary)
            if decoded != notice:
                raise ValueError('NuGet embedded notice differs from plain notice')
            bundle = json.loads(notice)
            if bundle.get('schema_version') != 1 or manifest.get('binary') != 'native/' + name or manifest.get('source_ref') != source_ref:
                raise ValueError('NuGet notice schema or native source identity mismatch')
            if manifest.get('binary_sha256') != sha(binary) or manifest.get('notice_sha256') != sha(notice) or manifest.get('cargo_lock_sha256') != bundle.get('cargo_lock_sha256'):
                raise ValueError('NuGet native notice manifest mismatch')
            staged_manifest = json.loads((staging / rid / 'NOTICE.manifest.json').read_text(encoding='utf-8'))
            if manifest != staged_manifest:
                raise ValueError('NuGet manifest differs from verified staging')
            architecture(binary, kind, machine)
            records.append({'rid': rid, 'binary_sha256': sha(binary), 'notice_sha256': sha(notice), 'notice_bytes': len(notice)})
        specs = [name for name in names if name.endswith('.nuspec')]
        if len(specs) != 1:
            raise ValueError('expected one nuspec')
        document = ET.fromstring(member(archive, specs[0], 1024 * 1024))
        versions = [node.text for node in document.iter() if node.tag.rsplit('}', 1)[-1] == 'version']
        if versions != ['0.8.1']:
            raise ValueError('NuGet project version changed')
    return {'schema_version': 1, 'source_ref': source_ref, 'package': package.name, 'package_sha256': sha(package.read_bytes()), 'runtimes': records}


def main():
    if len(sys.argv) != 5:
        raise SystemExit('usage: check_native_nupkg.py PACKAGE_DIRECTORY STAGED_RUNTIMES PROJECT_ROOT SOURCE_REF')
    try:
        directory, staging, project = map(Path, sys.argv[1:4])
        source_ref = sys.argv[4]
        tag = os.environ.get('RELEASE_TAG', '')
        if tag and tag != 'v0.8.1':
            raise ValueError('release tag does not match NuGet project version')
        packages = list(directory.glob('*.nupkg'))
        if len(packages) != 1:
            raise ValueError('expected exactly one NuGet package')
        result = verify(packages[0], staging, project, source_ref)
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error)) from error
    print(json.dumps(result, sort_keys=True, indent=2))


if __name__ == '__main__':
    main()
