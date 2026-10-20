"""Run production hazard rules and collision/effect dispatch with Unity side effects stubbed.

This does not simulate GPU rendering, particles, imported model bounds or UI layout.
"""
from pathlib import Path
import json
import os
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'Temp/HazardDodgeChecks'
out.mkdir(parents=True, exist_ok=True)
source = (root / 'Assets/Scripts/Minigames/HazardDodgeMinigame.cs').read_text(encoding='utf-8-sig')


def extract(signature, text=source):
    start = text.index(signature)
    opening = text.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


production = 'using System.Collections; using System.Collections.Generic; using UnityEngine;\npublic partial class HazardDodgeMinigame {\n'
production += extract('public enum HazardType') + '\n'
production += extract('private class ActiveHazardItem') + '\n'
for signature in ('private void UpdateActiveItems(', 'public void SetLane(',
                  'private void OnHazardHit()', 'private void FinishRun(',
                  'private void UpdateHazardFeedback(',
                  'private void ApplyCurrentPush(', 'public void ShiftLane(',
                  'private void UpdateSubmarineMotion(', 'private int NearbySolidLanes('):
    production += extract(signature) + '\n'
production += '}\n'
trigger = (root / 'Assets/Scripts/Minigames/HazardZoneTrigger.cs').read_text(encoding='utf-8-sig')
production += 'public partial class HazardZoneTrigger {\n'
for signature in ('private bool IsHazardTriggerSuppressed(', 'public void DeactivateHazard()',
                  'public void TriggerHazardMinigame()', 'private void TestTriggerHazardMinigameNow()',
                  'private void TryTriggerHazardMinigame(', 'private IEnumerator PeriodicSurgeRoutine()',
                  'private float NextSurgeInterval()'):
    production += extract(signature, trigger) + '\n'
production += '}\n'
(out / 'Production.cs').write_text(production)
atmosphere = (root / 'Assets/Scripts/Minigames/HazardDodgeAtmosphere.cs').read_text(encoding='utf-8-sig')
support_property = re.search(r'public bool SupportsScreenEffect =>.*?;', atmosphere, re.S)[0]
with (out / 'Production.cs').open('a') as file:
    file.write('public partial class HazardDodgeAtmosphere {\n' + support_property + '\n}\n')

# Check the actual Resources references, including local file IDs, rather than only filenames.
catalog = (root / 'Assets/Resources/HazardDodgeVisuals.asset').read_text()
paths = [root / p for p in (
    'Assets/Scripts/ScriptableObjects/HazardDodgeVisualAssets.cs',
    'Assets/Prefabs/Environment/Rocks/Icosphere.prefab',
    'Assets/Prefabs/Environment/Rocks/Icosphere_002.prefab',
    'Assets/Prefabs/Environment/Rocks/Cylinder_001.prefab',
    'Assets/Resources/Materials/Hazard_Particles.mat',
    'Assets/Resources/Materials/Hazard_Underwater.mat')]
for path in paths:
    guid = re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]
    assert len(guid) == 32, path
    reference = re.search(r'fileID: (\d+), guid: ' + guid, catalog)
    assert reference, f'Catalog missing {path}'
    file_id = reference[1]
    if path.suffix != '.cs':
        assert re.search(r'^--- !u!\d+ &' + file_id + r'\b', path.read_text(), re.M), path
material = (root / 'Assets/Resources/Materials/Hazard_Particles.mat').read_text()
shader = root / 'Library/PackageCache/com.unity.render-pipelines.universal@35356061dd01/Shaders/Particles/ParticlesUnlit.shader'
shader_guid = re.search(r'^guid: (\w+)', Path(str(shader) + '.meta').read_text(), re.M)[1]
assert shader_guid in material
water_shader = root / 'Assets/Shaders/HazardUnderwater.shader'
water_guid = re.search(r'^guid: (\w+)', Path(str(water_shader) + '.meta').read_text(), re.M)[1]
assert water_guid in paths[-1].read_text()
print('Resources catalog and particle shader references resolve.')

dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
sources = [out / 'Production.cs', root / 'Assets/Scripts/Minigames/HazardDodgeRules.cs',
           root / 'Assets/Scripts/Minigames/HazardDodgeConditions.cs',
           Path(__file__).with_name('Program.cs'), Path(__file__).with_name('UnityStubs.cs'),
           Path(__file__).with_name('TriggerChecks.cs'), Path(__file__).with_name('ConditionsChecks.cs')]
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(out / 'Checks.dll')]
args += ['/reference:' + str(p) for p in refs.glob('*.dll')]
args += [str(p) for p in sources]
(out / 'compile.rsp').write_text('\n'.join('"' + arg + '"' for arg in args))
data = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(data / 'NetCoreRuntime/dotnet.exe'), str(data / 'DotNetSdkRoslyn/csc.dll'), '@' + str(out / 'compile.rsp')], check=True)
(out / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(out / 'Checks.dll')], check=True)
