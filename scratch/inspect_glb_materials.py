import json, struct, os

def inspect_textures(path):
    with open(path, 'rb') as f:
        magic, version, length = struct.unpack('<4sII', f.read(12))
        if magic != b'glTF': return "Not glTF"
        chunk_len, chunk_type = struct.unpack('<II', f.read(8))
        json_data = json.loads(f.read(chunk_len).decode('utf-8'))
        
        images = json_data.get('images', [])
        materials = json_data.get('materials', [])
        return {
            'images_count': len(images),
            'materials': materials
        }

arts_dir = 'Assets/Arts/Environment/Sunlight Zone'
for f in os.listdir(arts_dir):
    if f.endswith('.glb'):
        info = inspect_textures(os.path.join(arts_dir, f))
        print(f"=== {f} ===")
        print(f"  Images: {info['images_count']}")
        for m in info['materials']:
            pbr = m.get('pbrMetallicRoughness', {})
            col = pbr.get('baseColorFactor', [1,1,1,1])
            tex = pbr.get('baseColorTexture', None)
            print(f"    Mat: {m.get('name')} | BaseColor: {[round(x,2) for x in col]} | HasTex: {tex is not None}")
