"""Проверить узкий licensing overlay без изменения managed code/contracts."""

import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('prepare_nuget_source', Path(__file__).resolve().parents[1] / 'scripts/prepare_nuget_source.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)

PROJECT = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>
    <Version>0.8.1</Version>
  </PropertyGroup>
  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="\\" />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
    <PackageReference Include="System.Memory" Version="4.5.5" />
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
  </ItemGroup>
</Project>
'''.encode()


class NugetSourceOverlayTest(unittest.TestCase):
    def test_old_source_gets_only_approved_items(self):
        result = helper.patch(PROJECT)
        before, after = ET.fromstring(PROJECT), ET.fromstring(result)
        self.assertEqual(helper.baseline(before), helper.baseline(after))
        platform = [node for node in after.iter('PackageReference') if node.get('Include') == helper.PLATFORM]
        self.assertEqual(len(platform), 1)
        self.assertEqual(platform[0].attrib, {'Include': helper.PLATFORM, 'Version': '3.1.0'})
        self.assertEqual(len([node for node in after.iter('None') if node.get('Include') in ('../../LICENSE-MIT', '../../LICENSE-APACHE')]), 2)

    def test_overlay_is_idempotent(self):
        result = helper.patch(PROJECT)
        self.assertEqual(helper.patch(result), result)

    def test_private_reference_becomes_public(self):
        source = PROJECT.replace(b'    <PackageReference Include="System.Memory"', b'    <PackageReference Include="Microsoft.NETCore.Platforms" Version="3.1.0" PrivateAssets="all" />\n    <PackageReference Include="System.Memory"')
        result = helper.patch(source)
        self.assertNotIn(b'PrivateAssets', result)
        self.assertEqual(helper.baseline(ET.fromstring(source)), helper.baseline(ET.fromstring(result)))

    def test_framework_change_rejected(self):
        with self.assertRaisesRegex(ValueError, 'frameworks or version'):
            helper.patch(PROJECT.replace(b'netstandard2.0', b'netstandard2.1'))

    def test_project_version_change_rejected(self):
        with self.assertRaisesRegex(ValueError, 'frameworks or version'):
            helper.patch(PROJECT.replace(b'<Version>0.8.1</Version>', b'<Version>0.8.2</Version>'))

    def test_unreviewed_platform_version_rejected(self):
        source = helper.patch(PROJECT).replace(b'Include="Microsoft.NETCore.Platforms" Version="3.1.0"', b'Include="Microsoft.NETCore.Platforms" Version="6.0.0"')
        with self.assertRaisesRegex(ValueError, 'platform reference'):
            helper.patch(source)

    def test_duplicate_platform_rejected(self):
        line = b'    <PackageReference Include="Microsoft.NETCore.Platforms" Version="3.1.0" />'
        source = helper.patch(PROJECT).replace(line, line + b'\n' + line)
        with self.assertRaisesRegex(ValueError, 'duplicate platform'):
            helper.patch(source)

    def test_unreviewed_package_license_path_rejected(self):
        source = helper.patch(PROJECT).replace(b'Include="../../LICENSE-MIT" Pack="true" PackagePath="\\"', b'Include="../../LICENSE-MIT" Pack="true" PackagePath="other"')
        with self.assertRaisesRegex(ValueError, 'license package item'):
            helper.patch(source)

    def test_other_reference_versions_are_preserved(self):
        result = helper.patch(PROJECT)
        before = [node.attrib for node in ET.fromstring(PROJECT).iter('PackageReference')]
        after = [node.attrib for node in ET.fromstring(result).iter('PackageReference') if node.get('Include') != helper.PLATFORM]
        self.assertEqual(before, after)


if __name__ == '__main__':
    unittest.main()
