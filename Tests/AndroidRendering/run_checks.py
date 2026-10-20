"""Exercise production cancellation/render-request control flow without a licensed Unity process.

Only Unity side effects are substituted. GPU rendering and actual HUD hierarchy need device/Play Mode tests.
"""
from pathlib import Path
import json
import os
import subprocess

root = Path(__file__).resolve().parents[2]
out = root / 'Temp/AndroidRenderingChecks'
out.mkdir(parents=True, exist_ok=True)

def method(relative, signature):
    source = (root / relative).read_text(encoding='utf-8-sig')
    start = source.index(signature)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

overlay = 'Assets/Scripts/UI/MinigameInstructionOverlay.cs'
manager = 'Assets/Scripts/Minigames/MinigameManager.cs'
generated = '''using System;
using System.Collections.Generic;
using UnityEngine;
public partial class MinigameInstructionOverlay {
 public bool _isOpen, _isDragging;
 public Action _onFinishedCallback;
 public Widget overlayRoot = new Widget(), _panelRect = new Widget();
 public Group _canvasGroup = new Group();
 public bool IsOpen => _isOpen || overlayRoot.activeSelf || _panelRect.gameObject.activeSelf;
 public void CompleteForTest() => CloseOverlay();
 public void CancelSlideshow() => HideInstant();
'''
generated += method(overlay, 'private void CloseOverlay()') + '\n' + method(overlay, 'public void HideInstant()') + '\n}\n'
generated += '''public partial class MinigameManager {
 public bool _minigameActive, _cancelling;
 public int _activityVersion;
 public bool IsMinigameActive => _minigameActive || _cancelling;
 public Activity _mg1 = new Activity(), _mg2 = new Activity(), _mg3 = new Activity(), _mg4 = new Activity();
 public bool ControlsEnabled;
 private void EnableControls() { ControlsEnabled = true; Events.Values.Add("controls"); }
 private static T FindFirstObjectByType<T>(FindObjectsInactive mode) where T: class => Globals.Overlay as T;
 public bool CurrentForTest(int version) => IsCurrentActivity(version);
'''
generated += method(manager, 'public void CancelActiveMinigame()') + '\n'
source = (root / manager).read_text()
generated += next(line for line in source.splitlines() if 'private bool IsCurrentActivity(' in line) + '\n}\n'
for name, field, path in [
    ('CleanupCameraHost', '_minigameCam', 'Assets/Scripts/Minigames/EnvironmentalCleanupMinigame.cs'),
    ('HazardCameraHost', '_stageCamera', 'Assets/Scripts/Minigames/HazardDodgeMinigame.cs')]:
    generated += f'''public class {name} {{
 public Camera {field};
 public List<Camera> _disabledSceneCams = new List<Camera>();
 public void RestoreForTest() => RestoreSceneCamera();
'''
    generated += method(path, 'private void RestoreSceneCamera()') + '\n}\n'
(out / 'ProductionMethods.cs').write_text(generated)

dotnet = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'dotnet'
version = max((dotnet / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
refs = next((version / 'ref').iterdir())
sources = [out / 'ProductionMethods.cs', Path(__file__).with_name('Program.cs'),
           root / 'Assets/Scripts/UI/PreviewRenderUtility.cs']
args = ['/nostdlib+', '/target:exe', '/langversion:9.0', '/out:' + str(out / 'Checks.dll')]
args += ['/reference:' + str(p) for p in refs.glob('*.dll')]
args += [str(p) for p in sources]
(out / 'compile.rsp').write_text('\n'.join('"' + arg + '"' for arg in args))
data = Path(r'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data')
subprocess.run([str(data / 'NetCoreRuntime/dotnet.exe'), str(data / 'DotNetSdkRoslyn/csc.dll'), '@' + str(out / 'compile.rsp')], check=True)
(out / 'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': refs.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': version.name}, 'rollForward': 'LatestPatch'}}))
subprocess.run([str(dotnet / 'dotnet.exe'), str(out / 'Checks.dll')], check=True)
