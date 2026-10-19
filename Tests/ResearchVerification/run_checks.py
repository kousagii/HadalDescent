"""Run real question selection/scoring code without starting a licensed Unity Editor.
Requires Python, the .NET SDK, and this project's Unity 6000.3.18f1 compiler.
Only engine data types are substituted; no production logic is copied into the test.
"""
from pathlib import Path
import json
import os
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Temp/ResearchChecks'
OUTPUT.mkdir(parents=True, exist_ok=True)


def scalar(raw, key):
    match = re.search(r'^  ' + re.escape(key) + r': ?(.*(?:\n    .*)*)', raw, re.M)
    if match is None:
        raise ValueError(key)
    text = match[1].strip()
    if text.startswith('"'):
        text = re.sub(r'\\U([0-9a-fA-F]{8})', lambda m: chr(int(m[1], 16)), text)
        text = re.sub(r'\\x([0-9a-fA-F]{2})', lambda m: chr(int(m[1], 16)), text)
        return json.loads(re.sub(r'\s*\n\s*', ' ', text))
    if text.startswith("'"):
        return text[1:-1].replace("''", "'")
    return ' '.join(text.split())


species = []
for path in sorted((ROOT / 'Assets/Scripts/ScriptableObjects/Species').rglob('*.asset')):
    raw = path.read_text(encoding='utf-8-sig')
    if 'guid: e81f7d5a231807847b19fbb00eb4b3b2' not in raw:
        continue
    entry = {key: scalar(raw, key) for key in ['speciesId', 'commonName', 'zoneIndex', 'habitat',
             'characteristics', 'ecologicalRole', 'interestingFact']}
    entry['zoneIndex'] = int(entry['zoneIndex'])
    entry['guid'] = re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]
    for key in ['photo', 'modelPrefab']:
        entry[key] = bool(re.search(r'^  ' + key + r': \{fileID: (?!0\})', raw, re.M))
    species.append(entry)

banks = []
for zone in range(5):
    raw = (ROOT / f'Assets/Resources/Research/Zone{zone}Questions.asset').read_text(encoding='utf-8')
    questions = []
    for block in raw.split('  - questionId: ')[1:]:
        lines = block.splitlines()
        entry = {'questionId': lines[0], 'choices': []}
        for line in lines[1:]:
            if line.startswith('    - '):
                entry['choices'].append(json.loads(line[6:]))
                continue
            match = re.match(r'    (\w+): (.*)', line)
            if not match:
                continue
            key, value = match.groups()
            if key == 'species': entry[key] = re.search(r'guid: (\w+)', value)[1]
            elif key == 'choices': continue
            elif key in ['format', 'sourceField', 'correctChoice']: entry[key] = int(value)
            else: entry[key] = json.loads(value)
        questions.append(entry)
    banks.append({'zoneIndex': zone, 'questions': questions})
input_path = OUTPUT / 'input.json'
input_path.write_text(json.dumps({'species': species, 'banks': banks}), encoding='utf-8')

dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
reference_root = dotnet / 'packs/Microsoft.NETCore.App.Ref'
version = max(reference_root.iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
sources = [ROOT / 'Assets/Scripts/Minigames/ResearchQuestionSelector.cs',
           ROOT / 'Assets/Scripts/ScriptableObjects/ResearchQuestionBank.cs',
           Path(__file__).with_name('Program.cs'), Path(__file__).with_name('UnityStubs.cs')]
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(OUTPUT / 'ResearchChecks.dll')]
args += ['/reference:' + str(path) for path in refs.glob('*.dll')]
args += [str(path) for path in sources]
rsp = OUTPUT / 'compile.rsp'
rsp.write_text('\n'.join('"' + a + '"' for a in args), encoding='utf-8')
editor = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(editor / 'NetCoreRuntime/dotnet.exe'), str(editor / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)], check=True)
(OUTPUT / 'ResearchChecks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name},
    'rollForward': 'LatestPatch'}}), encoding='utf-8')
subprocess.run([str(dotnet / 'dotnet.exe'), str(OUTPUT / 'ResearchChecks.dll'), str(input_path)], check=True)
