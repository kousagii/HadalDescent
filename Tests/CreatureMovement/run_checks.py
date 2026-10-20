"""Execute actual jet propulsion + depth enforcement with deferred-body stand-ins.

Native imported meshes are covered separately by SpeciesMovementValidation in Unity.
"""
from pathlib import Path
import json
import os
import re
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'Temp/CreatureMovementChecks'
out.mkdir(parents=True, exist_ok=True)
source = (root / 'Assets/Scripts/Creatures/CreatureLocomotion.cs').read_text(encoding='utf-8-sig')


def method(signature):
    start = source.index(signature)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


generated = 'using UnityEngine;\npublic partial class MovementHost {\n'
generated += '\n'.join(method(signature) for signature in [
    'private void FixedUpdate()', 'private void UpdatePulsatileJetter(', 'private void UpdatePelagicCruiser(',
    'private void EnforcePlayableAndDepthBounds()', 'private void FinalizeBenthicStep()',
    'private void ConstrainBenthicFloor(', 'private bool UsesBackwardEscape()', 'private static Vector3 BenthicTravelDirection('])
generated += '\n' + next(line for line in source.splitlines() if 'private Vector3 MovementForward =>' in line)

# Run the actual escape selection and body-heading sections of the benthic update.
benthic = method('private void UpdateBenthicFollower(')
start = benthic.index('        float yawOffset')
end = benthic.index('        float scaleFactorY')
generated += '\npublic (Vector3 direction,bool backward,float speed) EscapeSelection(Vector3 steerDir,float targetSpeed) {\n' + benthic[start:end] + '\nreturn (rawTangent, backwardEscape, targetSpeed); }\n'
start = benthic.index('        Vector3 hHeading;')
end = benthic.index('        var tg =', start)
generated += '\npublic Vector3 EscapeBodyHeading(Vector3 surfaceTangent, Vector3 flatFacing, bool backwardEscape) {\n' + benthic[start:end] + '\nreturn hHeading; }\n'
generated += '\n}\n'
ray_asset = (root / 'Assets/Scripts/ScriptableObjects/Species/Twilight Zone/DeepWaterStingray.asset').read_text()
points_section = ray_asset.split('  benthicSupportPoints:', 1)[1].split('  minCruiseSpeedFraction:', 1)[0]
points = re.findall(r'\{x: ([\d.-]+), y: ([\d.-]+), z: ([\d.-]+)\}', points_section)
generated += '\npublic static class RaySupportProfile { public static readonly Vector3[] Points = new[] { '
generated += ','.join(f'new Vector3({x}f,{y}f,{z}f)' for x,y,z in points)
generated += ' }; }\n'
placement = (root / 'Assets/Scripts/Creatures/BenthicPlacement.cs').read_text()
start = placement.index('    public static bool IsBlocked(')
end = placement.index('    public static float SampleFloor(', start)
generated += '\npublic static class BenthicPlacement {\n' + placement[start:end] + '\npublic static float SampleFloor(TerrainGenerator t, Vector3 p, float fallback) => t != null ? t.SampleHeight(p.x,p.z) : fallback;\n}\n'
spawner = (root / 'Assets/Scripts/Managers/SpeciesSpawner.cs').read_text(encoding='utf-8-sig')
start = spawner.index('    private bool TryPlaceBenthicInstance(')
end = spawner.index('    /// <summary>', start)
generated += '\npublic partial class SpawnPlacementHost {\n' + spawner[start:end] + '\n}\n'

locomotion_source = source
source = (root / 'Assets/Scripts/Creatures/SpeciesAI.cs').read_text(encoding='utf-8-sig')
generated += '\npublic partial class SpeciesAI {\n'
generated += '\n'.join(line for line in source.splitlines() if 'private const float MaxFleeDuration' in line or 'private const float FleeRecoveryDuration' in line or 'public bool IsFleeing' in line or 'public float FleeElapsed' in line) + '\n'
generated += '\n'.join(method(signature) for signature in [
    'public void Flee(', 'private void Update()', 'private void UpdateFlee()',
    'private void UpdateReturn()', 'private void SetFleeGoalAwayFrom(',
    'private void EnterReturning()', 'private void EnterWandering()', 'public void TurnAwayFromBoundary('])
generated += '\n}\n'
source = locomotion_source
(out / 'ProductionMethods.cs').write_text(generated)

dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
sources = [out / 'ProductionMethods.cs', Path(__file__).with_name('Program.cs'), Path(__file__).with_name('BenthicChecks.cs'), Path(__file__).with_name('FleeChecks.cs'),
           root / 'Assets/Scripts/Creatures/CreatureMotionBounds.cs',
           root / 'Assets/Scripts/Creatures/CreatureFacing.cs',
           root / 'Assets/Scripts/Creatures/BenthicClearance.cs']
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(out / 'Checks.dll')]
args += ['/reference:' + str(p) for p in refs.glob('*.dll')]
args += [str(p) for p in sources]
(out / 'compile.rsp').write_text('\n'.join('"' + arg + '"' for arg in args))
data = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(data / 'NetCoreRuntime/dotnet.exe'), str(data / 'DotNetSdkRoslyn/csc.dll'), '@' + str(out / 'compile.rsp')], check=True)
(out / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(out / 'Checks.dll')], check=True)

# Inspect only the two requested asset profiles. Existing art references are kept.
for name in ['RingTriangularBatfish', 'AlbatrossCoffinfish']:
    text = (root / f'Assets/Scripts/ScriptableObjects/Species/Twilight Zone/{name}.asset').read_text()
    fields = dict(line.split(': ', 1) for line in (line.strip() for line in text.splitlines()) if ': ' in line)
    assert fields['isStationary'] == '0' and fields['locomotionArchetype'] == '3'
    # Speed is an Inspector tuning choice; the regression is accidentally making
    # either species stationary or leaving it without a usable movement profile.
    assert float(fields['moveSpeed']) > 0 and float(fields['wanderRadius']) > 0
    print(name + ': mobile seabed profile passed.')

for name, expected in [('AlbatrossCoffinfish', -90), ('ThreelightHatchetfish', 90),
                       ('ShorttailLanternshark', 90), ('SuluSeaRibbonfish', -90)]:
    text = (root / f'Assets/Scripts/ScriptableObjects/Species/Twilight Zone/{name}.asset').read_text()
    fields = dict(line.split(': ', 1) for line in (line.strip() for line in text.splitlines()) if ': ' in line)
    assert float(fields['modelYawOffset']) == expected
    print(f'{name}: facing correction {expected} degrees passed.')
text = (root / 'Assets/Scripts/ScriptableObjects/Species/Twilight Zone/DeepWaterStingray.asset').read_text()
assert text.count('y: -1.32') == 5 and 'benthicSurfaceOffset: 0.4' in text
print('DeepWaterStingray: animated underside envelope and clearance profile passed.')
