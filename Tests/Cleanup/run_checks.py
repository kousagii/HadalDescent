"""Compile and exercise the actual cleanup generation/scoring rules without launching Unity."""
from pathlib import Path
import json
import os
import re
import subprocess

root = Path(__file__).resolve().parents[2]
output = root / 'Temp/CleanupChecks'
output.mkdir(parents=True, exist_ok=True)
host = (root / 'Assets/Scripts/Minigames/EnvironmentalCleanupMinigame.cs').read_text(encoding='utf-8')
enums = re.findall(r'    public enum (?:DebrisCategory|DebrisItemType)\s*\{[^}]+\}', host)
assert len(enums) == 2
(output / 'HostEnums.cs').write_text('public class EnvironmentalCleanupMinigame {\n' + '\n'.join(enums) + '\n}')
dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(output / 'CleanupChecks.dll')]
args += ['/reference:' + str(path) for path in refs.glob('*.dll')]
args += [str(output / 'HostEnums.cs'), str(root / 'Assets/Scripts/Minigames/CleanupRoundRules.cs'), str(Path(__file__).with_name('Program.cs'))]
rsp = output / 'compile.rsp'
rsp.write_text('\n'.join('"' + arg + '"' for arg in args))
editor = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(editor / 'NetCoreRuntime/dotnet.exe'), str(editor / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)], check=True)
(output / 'CleanupChecks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(output / 'CleanupChecks.dll')], check=True)
