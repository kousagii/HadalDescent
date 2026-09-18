import glob, re, os
from parse_fbx import read_fbx_vertices

# For each of the 9 rock prefabs used in SunlightProps:
rock_prefabs = [
    "Cone_001.prefab",
    "Cone_002.prefab",
    "Cube_001.prefab",
    "Cube_002.prefab",
    "Cube_004.prefab",
    "Icosphere_001.prefab",
    "Icosphere_003.prefab",
    "Sphere_002.prefab",
    "bisect.prefab"
]

for rp in rock_prefabs:
    p_path = os.path.join("Assets/Prefabs/Environment/Rocks", rp)
    with open(p_path, "r", encoding="utf-8") as f:
        txt = f.read()
    
    # get BoxCollider size
    bc_size = re.search(r'm_Size:\s*\{x:\s*([-\d\.]+),\s*y:\s*([-\d\.]+),\s*z:\s*([-\d\.]+)\}', txt)
    if bc_size:
        bx, by, bz = float(bc_size.group(1)), float(bc_size.group(2)), float(bc_size.group(3))
        # Find which of bx, by, bz is longest, mid, shortest
        dims = sorted([('X', bx), ('Y', by), ('Z', bz)], key=lambda x: x[1])
        shortest = dims[0]
        mid = dims[1]
        longest = dims[2]
        print(f"{rp:20s}: Collider size: X={bx:.4f}, Y={by:.4f}, Z={bz:.4f} | Shortest={shortest[0]}({shortest[1]:.4f}), Longest={longest[0]}({longest[1]:.4f})")
