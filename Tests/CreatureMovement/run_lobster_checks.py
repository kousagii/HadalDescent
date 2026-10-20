"""Lobster-only regression: internal animation motion lock and authored contact envelope."""
from pathlib import Path
import json
import os
import re
import struct
import subprocess

root = Path(__file__).resolve().parents[2]
output = root / 'Temp/LobsterGroundingChecks'
output.mkdir(parents=True, exist_ok=True)
source = (root / 'Assets/Scripts/Creatures/CreatureLocomotion.cs').read_text(encoding='utf-8-sig')


def method(signature):
    start = source.index(signature)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


asset = (root / 'Assets/Scripts/ScriptableObjects/Species/Sunlight Zone/OrnateSpinyLobster.asset').read_text()
section = asset.split('  benthicSupportPoints:', 1)[1].split('  minCruiseSpeedFraction:', 1)[0]
points = re.findall(r'\{x: ([\d.-]+), y: ([\d.-]+), z: ([\d.-]+)\}', section)
assert len(points) >= 5 and min(float(y) for x, y, z in points) <= -1.04698
generator = (root / 'Assets/Scripts/Editor/SpeciesDataGenerator.cs').read_text(encoding='utf-8-sig')
block = generator.split('speciesId              = "spiny_lobster_001"', 1)[1].split('}));', 1)[0]
block = block.split('benthicSupportPoints', 1)[1].split('            },', 1)[0]
authored = re.findall(r'new Vector3\(([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\)', block)
assert points == authored, 'Regenerating the lobster data would lose its contact profile.'

# The supplied generic clip moves/turns Bone below the GameObject root.
# applyRootMotion=false alone cannot remove these skeletal animation curves.
raw = (root / 'Assets/Arts/Creatures/Sunlight Zone/OrnateSpinyLobster.glb').read_bytes()
length = struct.unpack_from('<I', raw, 12)[0]
gltf = json.loads(raw[20:20+length])
bone = next(i for i, node in enumerate(gltf['nodes']) if node.get('name') == 'Bone')
paths = {channel['target']['path'] for animation in gltf['animations']
         for channel in animation['channels'] if channel['target']['node'] == bone}
assert {'translation', 'rotation'} <= paths

# Reuse numeric stand-ins, while compiling the actual lock and clearance methods.
stubs = (Path(__file__).with_name('Program.cs')).read_text()
start = stubs.index('namespace UnityEngine')
end = stubs.index('    public static class Time', start)
generated = 'using System;\nusing UnityEngine;\n' + stubs[start:end] + '''
    public class Transform {
        public string name; public Vector3 localPosition, position;
        public Quaternion localRotation = Quaternion.identity;
    }
    public class SkinnedMeshRenderer { public Transform[] bones; }
}
public class SpeciesData { public string speciesId; }
public class LobsterHost {
    private SpeciesData _data;
    private Transform _lobsterMotionBone;
    private Vector3 _lobsterMotionPosition;
    private Quaternion _lobsterMotionRotation;
    private SkinnedMeshRenderer[] skins;
    private T[] GetComponentsInChildren<T>() => (T[])(object)skins;
    public void Setup(string id, SkinnedMeshRenderer[] renderers) {
        _data = new SpeciesData { speciesId=id }; skins=renderers; CacheLobsterMotionBone();
    }
    public void Frame() => LateUpdate();
'''
generated += method('    private void CacheLobsterMotionBone()') + '\n' + method('    private void LateUpdate()')
generated += '''
}
public static class Checks {
    private static int count;
    private static void Require(bool value,string reason) { count++;if(!value)throw new Exception(reason); }
    public static void Main() {
        var root=new Transform { name="Bone",localPosition=new Vector3(.1f,0,.2f),localRotation=Quaternion.Euler(0,15,0) };
        var leg=new Transform { name="Bone.004" };
        var skins=new[] {new SkinnedMeshRenderer {bones=new[] {leg,root}}};
        var host=new LobsterHost();host.Setup("spiny_lobster_001",skins);
        for(int frame=0;frame<190;frame++) {
            root.localPosition=new Vector3(frame*.01f,0,frame*.02f);
            root.localRotation=Quaternion.Euler(0,frame*3,0);
            leg.localPosition=new Vector3(0,frame*.001f,0);
            host.Frame();
            Require(Vector3.Distance(root.localPosition,new Vector3(.1f,0,.2f))<.0001f,"Lobster animation still drifts away from its ground footprint.");
            Require(Quaternion.Angle(root.localRotation,Quaternion.Euler(0,15,0))<.01f,"Skeletal yaw still rotates the visible lobster away from its locomotion heading.");
            Require(MathF.Abs(leg.localPosition.y-frame*.001f)<.0001f,"The fix overwrote leg animation.");
        }
        host.Setup("mino_nylon_shrimp_001",skins);
        root.localPosition=new Vector3(10,20,30);root.localRotation=Quaternion.Euler(0,120,0);
        host.Frame();
        Require(Vector3.Distance(root.localPosition,new Vector3(10,20,30))<.0001f,"Lobster fix modified another species.");
        Require(Quaternion.Angle(root.localRotation,Quaternion.Euler(0,120,0))<.01f,"Lobster fix modified another species' rotation.");
        host.Setup("spiny_lobster_001",new[] {new SkinnedMeshRenderer {bones=new[] {leg}}});host.Frame();
        var points = new Vector3[] { POINTS };
        foreach(float scale in new[] {.75f,1f,1.25f})
        for(int yaw=0;yaw<360;yaw+=30)
        foreach(float pitch in new[] {-30f,0f,30f}) {
            var scaled=Array.ConvertAll(points,p=>p*(.45f*scale));
            var rotation=Quaternion.Euler(pitch,yaw,0);
            Func<Vector3,float> floor=p=>-15+.2f*p.x-.15f*p.z;
            float height=BenthicClearance.MinimumRootY(Vector3.zero,rotation,new Bounds(),.01f*scale,floor,scaled);
            foreach(var p in scaled) {
                var foot=new Vector3(0,height,0)+rotation*p;
                Require(foot.y>=floor(foot)+.01f*scale-.0001f,"Authored animated foot envelope penetrates a slope.");
            }
        }
        Console.WriteLine($"PASS: {count} lobster motion-lock, preserved gait, species-isolation and contact-envelope checks.");
    }
}
'''.replace('POINTS', ','.join(f'new Vector3({x}f,{y}f,{z}f)' for x, y, z in points))
(output / 'Checks.cs').write_text(generated)
dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(output / 'Checks.dll')]
args += ['/reference:' + str(p) for p in refs.glob('*.dll')]
args += [str(output / 'Checks.cs'), str(root / 'Assets/Scripts/Creatures/BenthicClearance.cs')]
(output / 'compile.rsp').write_text('\n'.join('"' + arg + '"' for arg in args))
editor = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(editor / 'NetCoreRuntime/dotnet.exe'), str(editor / 'DotNetSdkRoslyn/csc.dll'), '@' + str(output / 'compile.rsp')], check=True)
(output / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(output / 'Checks.dll')], check=True)
print('PASS: lobster data/generator parity and supplied skeleton animation-track checks.')
