"""Проверить, что MIT platform metadata приходит из нашего NuGet, не override consumer."""

import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

RIDS = ('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'win-x64', 'win-arm64')


def local(tag):
    return tag.rsplit('}', 1)[-1]


if len(sys.argv) != 3:
    raise SystemExit('usage: check-platform-consumer.py PACKAGE_DIRECTORY WORKSPACE')
packages, workspace = map(Path, sys.argv[1:])
workspace.mkdir()
archives = list(packages.glob('*.nupkg'))
if len(archives) != 1:
    raise SystemExit('expected exactly one source package')
source = ET.parse('src/Ktav/Ktav.csproj').getroot()
if [node.text for node in source.iter() if local(node.tag) == 'TargetFrameworks'] != ['net8.0;netstandard2.0']:
    raise SystemExit('public target frameworks changed')
if [node.text for node in source.iter() if local(node.tag) == 'Version'] != ['0.8.1']:
    raise SystemExit('project version changed')
with zipfile.ZipFile(archives[0]) as archive:
    nuspecs = [name for name in archive.namelist() if name.endswith('.nuspec')]
    if len(nuspecs) != 1:
        raise SystemExit('expected one nuspec')
    spec = ET.fromstring(archive.read(nuspecs[0]))
    groups = [node for node in spec.iter() if local(node.tag) == 'group' and 'netstandard' in node.get('targetFramework', '').lower()]
    if len(groups) != 1:
        raise SystemExit('expected exactly one netstandard dependency group')
    net8 = [node for node in spec.iter() if local(node.tag) == 'group' and node.get('targetFramework', '').lower() == 'net8.0']
    if len(net8) != 1 or any(node.get('id') == 'Microsoft.NETCore.Platforms' for node in net8[0]):
        raise SystemExit('net8 consumer dependency group changed')
    platforms = [node for node in groups[0] if node.get('id') == 'Microsoft.NETCore.Platforms']
    if len(platforms) != 1 or platforms[0].get('version', '').replace(' ', '') not in ('3.1.0', '[3.1.0,)'):
        raise SystemExit('MIT platform requirement is not exposed to consumers')
    for name in ('LICENSE-MIT', 'LICENSE-APACHE'):
        if archive.namelist().count(name) != 1 or archive.read(name) != Path(name).read_bytes():
            raise SystemExit('source package license bytes missing or changed')

consumer = workspace / 'consumer'
consumer.mkdir()
project = ET.Element('Project', {'Sdk': 'Microsoft.NET.Sdk'})
properties = ET.SubElement(project, 'PropertyGroup')
ET.SubElement(properties, 'TargetFramework').text = 'netstandard2.0'
ET.SubElement(properties, 'RuntimeIdentifiers').text = ';'.join(RIDS)
references = ET.SubElement(project, 'ItemGroup')
ET.SubElement(references, 'PackageReference', {'Include': 'Ktav', 'Version': '0.8.1'})
ET.ElementTree(project).write(consumer / 'Consumer.csproj', encoding='utf-8', xml_declaration=True)
configuration = ET.Element('configuration')
sources = ET.SubElement(configuration, 'packageSources')
ET.SubElement(sources, 'clear')
ET.SubElement(sources, 'add', {'key': 'local', 'value': str(packages.resolve())})
ET.SubElement(sources, 'add', {'key': 'nuget', 'value': 'https://api.nuget.org/v3/index.json'})
mapping = ET.SubElement(configuration, 'packageSourceMapping')
ET.SubElement(ET.SubElement(mapping, 'packageSource', {'key': 'local'}), 'package', {'pattern': 'Ktav'})
ET.SubElement(ET.SubElement(mapping, 'packageSource', {'key': 'nuget'}), 'package', {'pattern': '*'})
ET.ElementTree(configuration).write(consumer / 'NuGet.Config', encoding='utf-8', xml_declaration=True)
cache = workspace / 'packages'
environment = {**os.environ, 'NUGET_PACKAGES': str(cache.resolve())}
subprocess.run(['dotnet', 'restore', str(consumer / 'Consumer.csproj'), '--configfile', str(consumer / 'NuGet.Config')], env=environment, check=True)
assets = json.loads((consumer / 'obj/project.assets.json').read_text(encoding='utf-8'))
platforms = [name for name in assets['libraries'] if name.lower().startswith('microsoft.netcore.platforms/')]
if len(platforms) != 1 or platforms[0].lower() != 'microsoft.netcore.platforms/3.1.0':
    raise SystemExit(f'consumer resolved non-MIT platform metadata: {platforms}')
if any(not any(name.endswith('/' + rid) for name in assets['targets']) for rid in RIDS):
    raise SystemExit('consumer did not restore all six supported RIDs')
ktav = [name for name in assets['libraries'] if name.lower() == 'ktav/0.8.1']
if len(ktav) != 1:
    raise SystemExit('consumer did not restore exactly one Ktav package')
package = assets['libraries'][ktav[0]]
expected = base64.b64encode(hashlib.sha512(archives[0].read_bytes()).digest()).decode()
if package['sha512'].removeprefix('sha512-') != expected:
    raise SystemExit('consumer did not install the exact locally packed Ktav package')
metadata = ET.parse(next((cache / 'microsoft.netcore.platforms/3.1.0').glob('*.nuspec'))).getroot()
licenses = [node for node in metadata.iter() if local(node.tag) == 'license']
if len(licenses) != 1 or licenses[0].get('type') != 'expression' or licenses[0].text != 'MIT':
    raise SystemExit('consumer platform package is not declared MIT')
license_files = [path for path in (cache / 'microsoft.netcore.platforms/3.1.0').iterdir() if path.is_file() and path.name.upper().startswith('LICENSE')]
if len(license_files) != 1 or b'Permission is hereby granted, free of charge' not in license_files[0].read_bytes():
    raise SystemExit('consumer platform package has no original MIT license text')
print('Exact local NuGet consumer resolved MIT platform metadata 3.1.0 for all six RIDs without a consumer override')
print('Target frameworks and Ktav version unchanged; no claim about untested legacy RID fallback imports')
