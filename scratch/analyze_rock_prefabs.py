import glob, re, os

# Let's inspect each rock prefab used in SunlightProps.asset
# and find out what its mesh dimensions are, what its prefab rotation is,
# and what its world/instance dimensions (dx, dy, dz) are!

from parse_fbx import read_fbx_vertices

def quat_mult(q1, q2):
    w1, x1, y1, z1 = q1
    w2, x2, y2, z2 = q2
    return (
        w1*w2 - x1*x2 - y1*y2 - z1*z2,
        w1*x2 + x1*w2 + y1*z2 - z1*y2,
        w1*y2 - x1*z2 + y1*w2 + z1*x2,
        w1*z2 + x1*y2 - y1*x2 + z1*w2
    )

def rot_vec(q, v):
    qv = (0, v[0], v[1], v[2])
    q_inv = (q[0], -q[1], -q[2], -q[3])
    res = quat_mult(quat_mult(q, qv), q_inv)
    return (res[1], res[2], res[3])

# Rock prefabs in SunlightProps:
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
    prefab_path = os.path.join("Assets/Prefabs/Environment/Rocks", rp)
    with open(prefab_path, "r", encoding="utf-8") as f:
        txt = f.read()
    
    # get source guid
    source_guid = re.search(r'm_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]{32})', txt)
    fbx_name = rp.replace(".prefab", ".fbx")
    fbx_path = os.path.join("Assets/Arts/Environment/rocks-low-poly-starter-pack/source", fbx_name)
    
    # get prefab rotation
    rx = float(re.search(r'm_LocalRotation\.x\s+value:\s+([-\d\.]+)', txt).group(1))
    ry = float(re.search(r'm_LocalRotation\.y\s+value:\s+([-\d\.]+)', txt).group(1))
    rz = float(re.search(r'm_LocalRotation\.z\s+value:\s+([-\d\.]+)', txt).group(1))
    rw = float(re.search(r'm_LocalRotation\.w\s+value:\s+([-\d\.]+)', txt).group(1))
    q = (rw, rx, ry, rz)
    
    # read mesh vertices
    # Note: FBX vertices in binary format
    bounds = read_fbx_vertices(fbx_path)
    if bounds:
        bx, by, bz = bounds
        sx = bx[1] - bx[0]
        sy = by[1] - by[0]
        sz = bz[1] - bz[0]
        
        # Test rotated bounding box or rotated axes
        ax_x = rot_vec(q, (sx, 0, 0))
        ax_y = rot_vec(q, (0, sy, 0))
        ax_z = rot_vec(q, (0, 0, sz))
        
        # Total extent along X, Y, Z after prefab rotation
        dim_x = abs(ax_x[0]) + abs(ax_y[0]) + abs(ax_z[0])
        dim_y = abs(ax_x[1]) + abs(ax_y[1]) + abs(ax_z[1])
        dim_z = abs(ax_x[2]) + abs(ax_y[2]) + abs(ax_z[2])
        
        print(f"{rp:20s}: FBX size=({sx:.2f}, {sy:.2f}, {sz:.2f}) -> Rotated size=({dim_x:.2f}, {dim_y:.2f}, {dim_z:.2f}) | Major axis = {'Y (UPRIGHT!)' if dim_y > max(dim_x, dim_z) else 'Horizontal (Flat)'}")
