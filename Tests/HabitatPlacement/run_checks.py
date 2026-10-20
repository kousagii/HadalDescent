"""Exercise the production landmark search using fake terrain, without launching Unity.

Also compile the complete changed spawners against Unity and the project's last built
assembly, when available. Requires Python, .NET and Unity 6000.3.18f1.
"""
from pathlib import Path
import json
import os
import subprocess

ROOT = Path(__file__).resolve().parents[2]
TESTS = Path(__file__).parent
OUTPUT = ROOT / 'Temp/HabitatPlacementChecks'
OUTPUT.mkdir(parents=True, exist_ok=True)
EDITOR = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
DOTNET = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
reference_root = DOTNET / 'packs/Microsoft.NETCore.App.Ref'
version = max(reference_root.iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())


def compile_sources(name, sources, references, target='exe'):
    output = OUTPUT / (name + '.dll')
    args = ['/nostdlib+', '/target:' + target, '/langversion:9.0', '/out:' + str(output)]
    args += ['/reference:' + str(path) for path in references]
    args += [str(path) for path in sources]
    response = OUTPUT / (name + '.rsp')
    response.write_text('\n'.join('"' + arg + '"' for arg in args), encoding='utf-8')
    subprocess.run([str(EDITOR / 'NetCoreRuntime/dotnet.exe'),
                    str(EDITOR / 'DotNetSdkRoslyn/csc.dll'), '@' + str(response)], check=True)
    return output


source = (ROOT / 'Assets/Scripts/Managers/HabitatLandmarkSpawner.cs').read_text(encoding='utf-8-sig')
start = source.index('    private bool FindFlatBiomeLocation(')
end = source.index('    private BiomeBand GetTargetBiomeForLandmark(', start)
fields_start = source.index('    public struct LandmarkZone')
fields_end = source.index('    /// <summary>', fields_start)
generated = OUTPUT / 'ProductionSearch.cs'
prop_source = (ROOT / 'Assets/Scripts/Managers/EnvPropScatterer.cs').read_text(encoding='utf-8-sig')
prop_start = prop_source.index('    private bool TryPlacePropInstance(')
prop_end = prop_source.index('        // Rotation & Scale', prop_start)
generated.write_text('using System.Collections.Generic;\nusing UnityEngine;\n'
                     'public partial class PlacementChecks {\n' + source[fields_start:fields_end]
                     + source[start:end] + '\n}\n'
                     'public partial class PropChecks { private TerrainGenerator _terrain;\n'
                     + prop_source[prop_start:prop_end] + '\nreturn true;\n}\n}\n', encoding='utf-8')
dll = compile_sources('HabitatChecks', [generated, TESTS / 'Program.cs', TESTS / 'UnityStubs.cs'], refs.glob('*.dll'))
(OUTPUT / 'HabitatChecks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name},
    'rollForward': 'LatestPatch'}}), encoding='utf-8')
subprocess.run([str(DOTNET / 'dotnet.exe'), str(dll)], check=True)

project_assembly = ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll'
if project_assembly.exists():
    unity_refs = list((EDITOR / 'NetStandard/ref/2.1.0').glob('*.dll'))
    unity_refs += list((EDITOR / 'Managed/UnityEngine').glob('*.dll'))
    unity_refs += [project_assembly]
    compile_sources('SpawnerCompileCheck', [ROOT / ('Assets/Scripts/Managers/' + name + '.cs')
                    for name in ['HabitatLandmarkSpawner', 'EnvPropScatterer']], unity_refs, 'library')
    print('PASS: complete spawners compile against Unity assemblies.')
