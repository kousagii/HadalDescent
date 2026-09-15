import os, re

prefabs = [
    'coral by Kelli Ray - 7Cs3rTEcpcD.prefab',
    'Orange Coral by Device Lab - 3HEc6LvqCJd.prefab',
    'prop_sun_buried_clam_shell.prefab',
    'prop_sun_seagrass_patch.prefab'
]

for p in prefabs:
    path = os.path.join('Assets/Prefabs/Environment/Sunlight Zone', p)
    meta_path = path + '.meta'
    guid = ""
    with open(meta_path, 'r', encoding='utf-8') as mf:
        m = re.search(r'guid:\s*([a-f0-9]{32})', mf.read())
        if m: guid = m.group(1)
    
    # find root GameObject fileID or PrefabInstance
    with open(path, 'r', encoding='utf-8') as pf:
        lines = pf.readlines()
        # look for root GameObject or stripped Transform
        for i, line in enumerate(lines):
            if '--- !u!1 &' in line:
                print(f"{p} -> GUID: {guid} | First GameObject: {line.strip()}")
                break
        else:
            # if only PrefabInstance, find root in target
            print(f"{p} -> GUID: {guid} | PrefabInstance: {lines[2].strip()}")
