# Let's inspect the exact node hierarchy inside bottlenose dolphin.glb
import json, struct

with open('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb', 'rb') as f:
    f.seek(12)
    cl, ct = struct.unpack('<II', f.read(8))
    d = json.loads(f.read(cl).decode('utf-8'))

print("All nodes in bottlenose dolphin.glb:")
for i, n in enumerate(d['nodes']):
    print(f"Node {i}: name={n.get('name')}, children={n.get('children')}, mesh={n.get('mesh')}, trans={n.get('translation')}, rot={n.get('rotation')}, scale={n.get('scale')}")
