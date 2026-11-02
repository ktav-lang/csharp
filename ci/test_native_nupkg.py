"""Проверить actual NuGet container и согласованность embedded/staged notices."""

import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import warnings
import zipfile

spec = importlib.util.spec_from_file_location('native_nupkg', Path(__file__).resolve().parents[1] / 'scripts/check_native_nupkg.py')
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)
SOURCE = 'b' * 40


class NativeNugetTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.stage = self.root / 'runtimes'
        self.payloads = {
            'LICENSE-MIT': b'MIT terms', 'LICENSE-APACHE': b'Apache terms',
            'lib/net8.0/Ktav.dll': b'MZmanaged', 'lib/netstandard2.0/Ktav.dll': b'MZmanaged',
            'Ktav.nuspec': b'<package><metadata><version>0.8.1</version></metadata></package>',
        }
        for name in ('LICENSE-MIT', 'LICENSE-APACHE'):
            (self.root / name).write_bytes(self.payloads[name])
        self.notice = json.dumps({'schema_version': 1, 'cargo_lock_sha256': 'a' * 64}).encode()
        for rid, (name, kind, machine) in checker.RIDS.items():
            binary = self.binary(kind, machine, self.notice)
            manifest = {'binary': 'native/' + name, 'binary_sha256': checker.sha(binary), 'notice_sha256': checker.sha(self.notice), 'cargo_lock_sha256': 'a' * 64, 'source_ref': SOURCE}
            for relative, data in [('native/' + name, binary), ('NOTICE.json', self.notice), ('NOTICE.manifest.json', json.dumps(manifest).encode())]:
                path = self.stage / rid / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                self.payloads['runtimes/' + rid + '/' + relative] = data

    def binary(self, kind, machine, notice):
        header = bytearray(128)
        if kind == 'elf':
            header[:6] = b'\x7fELF\x02\x01'
            struct.pack_into('<H', header, 18, machine)
        elif kind == 'macho':
            header[:4] = b'\xcf\xfa\xed\xfe'
            struct.pack_into('<I', header, 4, machine)
        else:
            header[:2] = b'MZ'
            struct.pack_into('<I', header, 60, 64)
            header[64:68] = b'PE\0\0'
            struct.pack_into('<H', header, 68, machine)
        compressed = gzip.compress(notice, mtime=0)
        return bytes(header) + checker.embedded.MARKER + len(compressed).to_bytes(8, 'little') + compressed

    def package(self):
        path = self.root / 'Ktav.0.8.1.nupkg'
        with zipfile.ZipFile(path, 'w') as archive:
            for name, value in self.payloads.items():
                archive.writestr(name, value)
        return path

    def verify(self, source=SOURCE):
        return checker.verify(self.package(), self.stage, self.root, source)

    def replace_binary(self, rid, binary):
        name = checker.RIDS[rid][0]
        relative = 'runtimes/' + rid + '/'
        self.payloads[relative + 'native/' + name] = binary
        (self.stage / rid / 'native' / name).write_bytes(binary)
        manifest = json.loads(self.payloads[relative + 'NOTICE.manifest.json'])
        manifest['binary_sha256'] = checker.sha(binary)
        data = json.dumps(manifest).encode()
        self.payloads[relative + 'NOTICE.manifest.json'] = data
        (self.stage / rid / 'NOTICE.manifest.json').write_bytes(data)

    def test_exact_six_native_payloads(self):
        self.assertEqual(len(self.verify()['runtimes']), 6)

    def test_missing_runtime_rejected(self):
        del self.payloads['runtimes/linux-x64/native/libktav_cabi.so']
        with self.assertRaisesRegex(ValueError, 'native payload inventory'):
            self.verify()

    def test_notice_inside_native_directory_rejected(self):
        self.payloads['runtimes/linux-x64/native/NOTICE.json'] = self.notice
        with self.assertRaisesRegex(ValueError, 'native payload inventory'):
            self.verify()

    def test_hidden_executable_rejected(self):
        self.payloads['content/hidden.dat'] = b'MZhidden'
        with self.assertRaisesRegex(ValueError, 'unexpected NuGet executable'):
            self.verify()

    def test_packaged_notice_change_rejected(self):
        self.payloads['runtimes/linux-x64/NOTICE.json'] = b'changed'
        with self.assertRaisesRegex(ValueError, 'verified staging'):
            self.verify()

    def test_embedded_notice_change_rejected(self):
        other = json.dumps({'schema_version': 1, 'cargo_lock_sha256': 'c' * 64}).encode()
        self.replace_binary('linux-x64', self.binary('elf', 62, other))
        with self.assertRaisesRegex(ValueError, 'embedded notice'):
            self.verify()

    def test_architecture_change_rejected(self):
        self.replace_binary('linux-arm64', self.binary('elf', 62, self.notice))
        with self.assertRaisesRegex(ValueError, 'architecture'):
            self.verify()

    def test_source_mismatch_rejected(self):
        with self.assertRaisesRegex(ValueError, 'source identity'):
            self.verify('c' * 40)

    def test_manifest_change_rejected(self):
        path = 'runtimes/linux-x64/NOTICE.manifest.json'
        manifest = json.loads(self.payloads[path])
        manifest['notice_sha256'] = '0' * 64
        self.payloads[path] = json.dumps(manifest).encode()
        with self.assertRaisesRegex(ValueError, 'manifest mismatch'):
            self.verify()

    def test_own_license_change_rejected(self):
        self.payloads['LICENSE-MIT'] = b'changed'
        with self.assertRaisesRegex(ValueError, 'license bytes'):
            self.verify()

    def test_duplicate_member_rejected(self):
        path = self.package()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore', UserWarning)
            with zipfile.ZipFile(path, 'a') as archive:
                archive.writestr('LICENSE-MIT', b'duplicate')
        with self.assertRaisesRegex(ValueError, 'duplicate NuGet'):
            checker.verify(path, self.stage, self.root, SOURCE)

    def test_unsafe_path_rejected(self):
        self.payloads['../outside'] = b'data'
        with self.assertRaisesRegex(ValueError, 'unsafe NuGet'):
            self.verify()


if __name__ == '__main__':
    unittest.main()
