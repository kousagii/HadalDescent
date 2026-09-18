import json, struct

with open("Assets/Arts/Environment/Sunlight Zone/prop_sun_buried_clam_shell.glb", "rb") as f:
    magic = f.read(4)
    version = struct.unpack("<I", f.read(4))[0]
    length = struct.unpack("<I", f.read(4))[0]
    chunk_len = struct.unpack("<I", f.read(4))[0]
    chunk_type = f.read(4)
    json_data = json.loads(f.read(chunk_len).decode('utf-8'))

print("Nodes in clam GLB:")
for i, n in enumerate(json_data.get('nodes', [])):
    print(f"Node {i}: {n}")

print("\nMeshes in clam GLB:")
for i, m in enumerate(json_data.get('meshes', [])):
    print(f"Mesh {i}: {m.get('name')}")
    for p in m.get('primitives', []):
        pos_acc = p['attributes']['POSITION']
        acc = json_data['accessors'][pos_acc]
        print(f"  Primitive pos bounds: min={acc.get('min')}, max={acc.get('max')}")
