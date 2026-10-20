"""Compile the production ground-contact component with stand-ins for Unity scene queries."""
from pathlib import Path
import json
import os
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'Temp/CreatureGroundingChecks'
out.mkdir(parents=True, exist_ok=True)
dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
sources = [Path(__file__).with_name('GroundingChecks.cs'), root / 'Assets/Scripts/Creatures/CreatureGroundContact.cs']
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(out / 'Checks.dll')]
args += ['/reference:' + str(p) for p in refs.glob('*.dll')]
args += [str(p) for p in sources]
(out / 'compile.rsp').write_text('\n'.join('"' + arg + '"' for arg in args))
data = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(data / 'NetCoreRuntime/dotnet.exe'), str(data / 'DotNetSdkRoslyn/csc.dll'), '@' + str(out / 'compile.rsp')], check=True)
(out / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(out / 'Checks.dll')], check=True)

# Check variant wiring without rewriting/importing any assets.
prefab = (root / 'Assets/Prefabs/Creatures/Twilight Zone/Deep-water Coral.prefab').read_text()
docs = dict((match.group(1), match.group(2)) for match in re.finditer(r'^--- !u!\d+ &(-?\d+)[^\n]*\n(.*?)(?=^--- |\Z)', prefab, re.M | re.S))
component = next(doc for doc in docs.values() if 'Assembly-CSharp::CreatureGroundContact' in doc)
anchor_id = re.search(r'contactPoint: \{fileID: (\d+)\}', component).group(1)
anchor = docs[anchor_id]
game_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', anchor).group(1)
parent_id = re.search(r'm_Father: \{fileID: (\d+)\}', anchor).group(1)
assert 'm_Name: GroundContact' in docs[game_id]
assert 'stripped' in prefab.split('&'+parent_id,1)[1].splitlines()[0]
assert 'embedDepth: 0.05' in component
assert 'addedObject: {fileID: '+anchor_id+'}' in prefab
assert prefab.count('BoxCollider:') == 1
assert 'm_Size: {x: 1.7158829, y: 1.3165206, z: 1.7026262}' in prefab
print('PASS: coral anchor references, variant child override, and original Box Collider settings.')
