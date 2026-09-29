import json, struct

with open('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb', 'rb') as f:
    f.seek(12)
    cl, ct = struct.unpack('<II', f.read(8))
    d = json.loads(f.read(cl).decode('utf-8'))

for i, n in enumerate(d.get('nodes', [])):
    name = n.get('name', '')
    if 'Bone' in name:
        print(f"Node {i} ({name}): trans={n.get('translation')}, rot={n.get('rotation')}")
