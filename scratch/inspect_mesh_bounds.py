import json, struct

with open('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb', 'rb') as f:
    f.seek(12)
    cl, ct = struct.unpack('<II', f.read(8))
    d = json.loads(f.read(cl).decode('utf-8'))
    f.seek(12 + 8 + cl)
    b_len, b_type = struct.unpack('<II', f.read(8))
    bin_data = f.read(b_len)

for i, mesh in enumerate(d.get('meshes', [])):
    print(f"Mesh {i}: {mesh.get('name')}")
    for prim in mesh.get('primitives', []):
        pos_acc_idx = prim['attributes'].get('POSITION')
        acc = d['accessors'][pos_acc_idx]
        print(f"  POSITION min: {acc.get('min')}, max: {acc.get('max')}")
