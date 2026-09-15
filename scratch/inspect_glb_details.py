import json, struct, os

def inspect_glb(path):
    with open(path, 'rb') as f:
        magic, version, length = struct.unpack('<4sII', f.read(12))
        if magic != b'glTF': return "Not glTF"
        chunk_len, chunk_type = struct.unpack('<II', f.read(8))
        json_data = json.loads(f.read(chunk_len).decode('utf-8'))
        
        # Access nodes, meshes, accessors
        accessors = json_data.get('accessors', [])
        nodes = json_data.get('nodes', [])
        materials = json_data.get('materials', [])
        
        # find min/max of position accessors
        bounds = []
        for acc in accessors:
            if 'min' in acc and 'max' in acc and len(acc['min']) == 3:
                bounds.append((acc['min'], acc['max']))
        
        mat_names = [m.get('name', 'unnamed') for m in materials]
        node_names = [n.get('name', 'unnamed') for n in nodes]
        return {
            'nodes': node_names,
            'materials': mat_names,
            'bounds': bounds[0] if bounds else None
        }

arts_dir = 'Assets/Arts/Environment/Sunlight Zone'
for f in os.listdir(arts_dir):
    if f.endswith('.glb'):
        info = inspect_glb(os.path.join(arts_dir, f))
        print(f"=== {f} ===")
        print(f"  Nodes: {info['nodes']}")
        print(f"  Materials: {info['materials']}")
        if info['bounds']:
            mins, maxs = info['bounds']
            dims = [maxs[i] - mins[i] for i in range(3)]
            print(f"  Dims (X,Y,Z): [{dims[0]:.2f}, {dims[1]:.2f}, {dims[2]:.2f}]")
