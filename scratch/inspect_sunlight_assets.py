import os, re

prefabs_dir = 'Assets/Prefabs/Environment/Sunlight Zone'
for f in os.listdir(prefabs_dir):
    if f.endswith('.prefab'):
        print(f"=== Prefab: {f} ===")
        with open(os.path.join(prefabs_dir, f), 'r', encoding='utf-8', errors='ignore') as p:
            c = p.read()
            # find materials, scale, components
            guids = re.findall(r'guid:\s*([a-f0-9]{32})', c)
            print("  Referenced GUIDs:", set(guids))

print("\n=== GLBs in Assets/Arts/Environment/Sunlight Zone ===")
arts_dir = 'Assets/Arts/Environment/Sunlight Zone'
for f in os.listdir(arts_dir):
    if f.endswith('.glb'):
        size = os.path.getsize(os.path.join(arts_dir, f))
        meta_path = os.path.join(arts_dir, f + '.meta')
        guid = "NO_META"
        if os.path.exists(meta_path):
            with open(meta_path, 'r') as mf:
                m = re.search(r'guid:\s*([a-f0-9]{32})', mf.read())
                if m: guid = m.group(1)
        print(f"  {f} ({size} bytes) -> guid: {guid}")
