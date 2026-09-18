import os, re, math

# Rock prefabs
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
    bx, by, bz = float(bc_size.group(1)), float(bc_size.group(2)), float(bc_size.group(3))
    
    # In prefab coordinate space:
    # Which axis is shortest?
    dims = {'X': bx, 'Y': by, 'Z': bz}
    shortest_axis = min(dims, key=dims.get)
    longest_axis = max(dims, key=dims.get)
    
    # If shortest axis is aligned with UP (0, 1, 0):
    # Then vertical height = shortest_axis length
    # horizontal footprint = other two axes lengths
    h = dims[shortest_axis]
    other_dims = [v for k, v in dims.items() if k != shortest_axis]
    w1, w2 = other_dims[0], other_dims[1]
    
    print(f"{rp:20s}: Original (X={bx:.3f}, Y={by:.3f}, Z={bz:.3f})")
    print(f"   -> Shortest={shortest_axis} ({h:.3f}), Longest={longest_axis} ({dims[longest_axis]:.3f})")
    print(f"   -> Laid flat: Height={h:.3f}, Footprint={max(w1,w2):.3f} x {min(w1,w2):.3f}")
    print(f"   -> Height/MaxFootprint ratio = {h/max(w1,w2):.2f} (COMPLETELY FLAT!)")
