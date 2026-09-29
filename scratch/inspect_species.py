import json, struct, os

def inspect_glb(path):
    print("=" * 60)
    print("Inspecting:", path)
    if not os.path.exists(path):
        print("NOT FOUND")
        return
    with open(path, 'rb') as f:
        f.seek(12)
        cl, ct = struct.unpack('<II', f.read(8))
        d = json.loads(f.read(cl).decode('utf-8'))
        print("Scenes:", d.get('scenes'))
        print("Nodes (non-bone):")
        for i, n in enumerate(d.get('nodes', [])):
            name = n.get('name', '')
            if not name.startswith('Bone.') and not name.startswith('bone'):
                print(f"  [{i}] {name}: rot={n.get('rotation')}, trans={n.get('translation')}, scale={n.get('scale')}, mesh={n.get('mesh')}")

inspect_glb('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb')
inspect_glb('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin 1.glb')
