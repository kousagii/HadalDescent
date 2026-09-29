import json, struct

with open('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb', 'rb') as f:
    f.seek(12)
    cl, ct = struct.unpack('<II', f.read(8))
    d = json.loads(f.read(cl).decode('utf-8'))

# Let's inspect bone hierarchy from Bone.001 to Bone.010
# Bone.001 is parent of Bone.002 and Bone.007
# Bone.002 is parent of Bone.003 -> Bone.004 -> Bone.005 -> Bone.006 -> Bone.009 -> Bone.010
for i in [12, 9, 8, 7, 6, 5, 4, 3]:
    n = d['nodes'][i]
    print(f"Node {i} ({n.get('name')}): trans={n.get('translation')}, rot={n.get('rotation')}")
