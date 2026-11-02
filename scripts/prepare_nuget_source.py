"""Применить только согласованный licensing overlay к managed package source."""

import copy
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

LICENSES = ('LICENSE-MIT', 'LICENSE-APACHE')
PLATFORM = 'Microsoft.NETCore.Platforms'
CONDITION = "'$(TargetFramework)' == 'netstandard2.0'"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def baseline(document):
    root = copy.deepcopy(document)
    for group in root:
        for node in list(group):
            if node.tag == 'PackageReference' and node.get('Include') == PLATFORM:
                group.remove(node)
            if node.tag == 'None' and node.get('Include') in ('../../LICENSE-MIT', '../../LICENSE-APACHE'):
                group.remove(node)
    for node in root.iter():
        if node.text is not None and not node.text.strip():
            node.text = None
        if node.tail is not None and not node.tail.strip():
            node.tail = None
    return ET.tostring(root)


def patch(data):
    original = ET.fromstring(data)
    if original.tag != 'Project':
        raise ValueError('unexpected managed project format')
    if [node.text for node in original.iter('TargetFrameworks')] != ['net8.0;netstandard2.0'] or [node.text for node in original.iter('Version')] != ['0.8.1']:
        raise ValueError('managed frameworks or version require review')
    references = [(group, node) for group in original for node in group if node.tag == 'PackageReference' and node.get('Include') == PLATFORM]
    if len(references) > 1:
        raise ValueError('duplicate platform reference')
    if references:
        group, node = references[0]
        if group.get('Condition') != CONDITION or node.get('Version') != '3.1.0' or node.get('PrivateAssets') not in (None, 'all') or set(node.attrib) - {'Include', 'Version', 'PrivateAssets'} or len(node):
            raise ValueError('unreviewed platform reference')
    for name in LICENSES:
        nodes = [node for node in original.iter('None') if node.get('Include') == '../../' + name]
        if len(nodes) > 1 or any(node.attrib != {'Include': '../../' + name, 'Pack': 'true', 'PackagePath': '\\'} or len(node) for node in nodes):
            raise ValueError('unreviewed license package item')
    text = data.decode('utf-8')
    for name in LICENSES:
        if not any(node.get('Include') == '../../' + name for node in original.iter('None')):
            anchor = '    <None Include="../../README.md" Pack="true" PackagePath="\\" />'
            if text.count(anchor) != 1:
                raise ValueError('managed package item layout requires review')
            text = text.replace(anchor, anchor + f'\n    <None Include="../../{name}" Pack="true" PackagePath="\\" />')
    if references:
        old = '<PackageReference Include="Microsoft.NETCore.Platforms" Version="3.1.0" PrivateAssets="all" />'
        if references[0][1].get('PrivateAssets') == 'all':
            if text.count(old) != 1:
                raise ValueError('platform declaration layout requires review')
            text = text.replace(old, '<PackageReference Include="Microsoft.NETCore.Platforms" Version="3.1.0" />')
    else:
        anchor = '  <ItemGroup Condition="' + CONDITION + '">'
        if text.count(anchor) != 1:
            raise ValueError('managed framework declaration requires review')
        text = text.replace(anchor, anchor + '\n    <PackageReference Include="Microsoft.NETCore.Platforms" Version="3.1.0" />')
    result = text.encode('utf-8')
    packaged = ET.fromstring(result)
    if baseline(original) != baseline(packaged):
        raise ValueError('licensing overlay changed unrelated managed project content')
    if len([node for node in packaged.iter('PackageReference') if node.get('Include') == PLATFORM and node.get('Version') == '3.1.0' and node.get('PrivateAssets') is None]) != 1:
        raise ValueError('public MIT platform reference was not staged')
    return result


def main():
    if len(sys.argv) != 3 or not re.fullmatch(r'[0-9a-f]{40}', sys.argv[1]):
        raise SystemExit('usage: prepare_nuget_source.py EXACT_SOURCE_REF PROVENANCE_JSON')
    try:
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        if revision != sys.argv[1]:
            raise ValueError('managed source checkout does not match native source')
        project = Path('src/Ktav/Ktav.csproj')
        original = project.read_bytes()
        packaged = patch(original)
        project.write_bytes(packaged)
        with Path(sys.argv[2]).open('x', encoding='utf-8') as output:
            json.dump({'source_ref': revision, 'tooling_ref': os.environ['GITHUB_SHA'], 'original_csproj_sha256': sha(original), 'packaged_csproj_sha256': sha(packaged), 'approved_overlay': 'Own license package items and public MIT platform metadata 3.1.0 only; frameworks/version/other references/code unchanged.'}, output, sort_keys=True, indent=2)
    except (OSError, ValueError, KeyError, ET.ParseError, subprocess.CalledProcessError) as error:
        raise SystemExit(str(error)) from error


if __name__ == '__main__':
    main()
