#!/usr/bin/env python3
"""Static inventory, XML/event/resource references and document links.
Not an XAML/C# compiler, application test or API/type verifier.
"""
from __future__ import annotations
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'src/Win10WebmCut'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
EVENTS = {'Closing','PreviewKeyDown','Click','SizeChanged','MouseLeftButtonDown','MouseMove',
          'MouseLeftButtonUp','LostMouseCapture','TextChanged','Checked','SelectionChanged',
          'ValueChanged','PreviewMouseLeftButtonDown','PreviewMouseLeftButtonUp'}
HISTORICAL = {'docs/ROADMAP.md','docs/audits/2026-10-26/README.md'}

def inspect() -> dict:
    result = {'kind':'static-source-inspection-not-compilation', 'xml':[], 'events':[],
              'resources':{}, 'links':[], 'publicationItems':[], 'inventory':[], 'limits':[
              'Does not resolve C# types, framework APIs or XAML binding semantics.',
              'Does not execute PowerShell, .NET, FFmpeg, GUI or unit tests.',
              'Historical paths are retained through the Git base tree; not rewritten.']}
    for file in list(SRC.glob('*.xaml')) + list(SRC.glob('*.csproj')):
        try:
            tree = ET.parse(file)
            result['xml'].append({'path':str(file.relative_to(ROOT)), 'wellFormedXml':True})
        except ET.ParseError as ex:
            result['xml'].append({'path':str(file.relative_to(ROOT)), 'parseError':str(ex)})
            continue
        if file.suffix == '.xaml':
            code = Path(str(file) + '.cs')
            source = code.read_text(encoding='utf-8') if code.exists() else ''
            for element in tree.iter():
                for event, method in element.attrib.items():
                    if event in EVENTS:
                        result['events'].append({'file':file.name,'event':event,'method':method,
                            'declarationTextFound':bool(re.search(r'\b' + re.escape(method) + r'\s*\(', source))})
        else:
            for content in tree.iter('Content'):
                for path in content.attrib.get('Include','').split(';'):
                    origin = (file.parent / path).resolve()
                    result['publicationItems'].append({'source':str(origin.relative_to(ROOT)),
                        'existsInSourceWorkspace':origin.exists(),
                        'copyToPublish':content.findtext('CopyToPublishDirectory'),
                        'outsideSingleFile':content.findtext('ExcludeFromSingleFile')})
    resources = {e.attrib[X+'Key'] for e in ET.parse(SRC/'App.xaml').iter() if X+'Key' in e.attrib}
    references = set()
    for file in SRC.glob('*.xaml'):
        references.update(re.findall(r'\{StaticResource\s+([^}]+)\}',file.read_text(encoding='utf-8')))
    result['resources'] = {'definedKeys':sorted(resources),'missingKeys':sorted(references-resources)}
    for file in list(ROOT.glob('*.md')) + list((ROOT/'docs').glob('*.md')) + list((ROOT/'tools').glob('*.md')):
        for target in re.findall(r'\]\(([^)]+)\)', file.read_text(encoding='utf-8')):
            if '://' in target or target.startswith('#'): continue
            resolved = (file.parent / target.split('#')[0]).resolve()
            relative = str(resolved.relative_to(ROOT))
            result['links'].append({'from':str(file.relative_to(ROOT)), 'to':relative,
                'state':'present' if resolved.exists() else 'retained-baseline' if relative in HISTORICAL else 'missing'})
    for file in sorted(SRC.iterdir()):
        if file.is_file():
            raw=file.read_bytes()
            result['inventory'].append({'path':str(file.relative_to(ROOT)), 'bytes':len(raw),
                'lines':len(raw.splitlines()),'sha256':hashlib.sha256(raw).hexdigest(),
                'gitBlob':hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()})
    result['sourceRules'] = {
        'mediaElementReferences':[f.name for f in SRC.glob('*') if f.is_file() and 'MediaElement' in f.read_text(encoding='utf-8')],
        'selectedIndexReferences':[f.name for f in SRC.glob('*') if f.is_file() and 'SelectedIndex' in f.read_text(encoding='utf-8')],
        'dispatcherTimerReferences':[f.name for f in SRC.glob('*') if f.is_file() and 'DispatcherTimer' in f.read_text(encoding='utf-8')],
        'fileDeleteOwners':[f.name for f in SRC.glob('*.cs') if 'File.Delete(' in f.read_text(encoding='utf-8')],
        'processStartOwners':[f.name for f in SRC.glob('*.cs') if 'process.Start()' in f.read_text(encoding='utf-8')]
    }
    return result

if __name__ == '__main__':
    print(json.dumps(inspect(),ensure_ascii=False,indent=2))
