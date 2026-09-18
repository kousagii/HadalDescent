import math, os, re
from parse_fbx import read_fbx_vertices

# Let's test OrientRockFlat logic on all 9 rock prefabs
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

def from_to_rotation(v_from, v_to):
    # Normalize
    f_len = math.sqrt(sum(x*x for x in v_from))
    t_len = math.sqrt(sum(x*x for x in v_to))
    u_f = [x/f_len for x in v_from]
    u_t = [x/t_len for x in v_to]
    
    dot = sum(a*b for a, b in zip(u_f, u_t))
    if dot >= 0.999999:
        return (1.0, 0.0, 0.0, 0.0) # Identity
    if dot <= -0.999999:
        # 180 degree rotation around any orthogonal axis
        ortho = (1, 0, 0) if abs(u_f[0]) < 0.9 else (0, 1, 0)
        cross = (u_f[1]*ortho[2] - u_f[2]*ortho[1],
                 u_f[2]*ortho[0] - u_f[0]*ortho[2],
                 u_f[0]*ortho[1] - u_f[1]*ortho[0])
        c_len = math.sqrt(sum(x*x for x in cross))
        return (0.0, cross[0]/c_len, cross[1]/c_len, cross[2]/c_len)
    
    cross = (u_f[1]*u_t[2] - u_f[2]*u_t[1],
             u_f[2]*u_t[0] - u_f[0]*u_t[2],
             u_f[0]*u_t[1] - u_f[1]*u_t[0])
    w = 1.0 + dot
    q = (w, cross[0], cross[1], cross[2])
    q_len = math.sqrt(sum(x*x for x in q))
    return (q[0]/q_len, q[1]/q_len, q[2]/q_len, q[3]/q_len)

normal = (0.0, 1.0, 0.0) # Vertical seabed normal

for rp in rock_prefabs:
    p_path = os.path.join("Assets/Prefabs/Environment/Rocks", rp)
    with open(p_path, "r", encoding="utf-8") as f:
        txt = f.read()
    
    # get BoxCollider size
    bc_size = re.search(r'm_Size:\s*\{x:\s*([-\d\.]+),\s*y:\s*([-\d\.]+),\s*z:\s*([-\d\.]+)\}', txt)
    bx, by, bz = float(bc_size.group(1)), float(bc_size.group(2)), float(bc_size.group(3))
    
    # get prefab rotation
    rx = float(re.search(r'm_LocalRotation\.x\s+value:\s+([-\d\.]+)', txt).group(1))
    ry = float(re.search(r'm_LocalRotation\.y\s+value:\s+([-\d\.]+)', txt).group(1))
    rz = float(re.search(r'm_LocalRotation\.z\s+value:\s+([-\d\.]+)', txt).group(1))
    rw = float(re.search(r'm_LocalRotation\.w\s+value:\s+([-\d\.]+)', txt).group(1))
    prefab_rot = (rw, rx, ry, rz)
    
    # World space directions of local axes after prefab rotation
    ax_x = rot_vec(prefab_rot, (1, 0, 0))
    ax_y = rot_vec(prefab_rot, (0, 1, 0))
    ax_z = rot_vec(prefab_rot, (0, 0, 1))
    
    # Dimension along each local axis
    len_x = bx
    len_y = by
    len_z = bz
    
    if len_x <= len_y and len_x <= len_z:
        shortest_dir = ax_x
    elif len_y <= len_x and len_y <= len_z:
        shortest_dir = ax_y
    else:
        shortest_dir = ax_z
    
    # Make sure shortest_dir points in normal direction
    dot = sum(a*b for a,b in zip(shortest_dir, normal))
    if dot < 0:
        shortest_dir = (-shortest_dir[0], -shortest_dir[1], -shortest_dir[2])
    
    align_flat = from_to_rotation(shortest_dir, normal)
    final_rot = quat_mult(align_flat, prefab_rot)
    
    # Check transformed dimensions
    v_x = rot_vec(final_rot, (bx, 0, 0))
    v_y = rot_vec(final_rot, (0, by, 0))
    v_z = rot_vec(final_rot, (0, 0, bz))
    
    ext_x = abs(v_x[0]) + abs(v_y[0]) + abs(v_z[0])
    ext_y = abs(v_x[1]) + abs(v_y[1]) + abs(v_z[1])
    ext_z = abs(v_x[2]) + abs(v_y[2]) + abs(v_z[2])
    
    print(f"{rp:20s}: Final World Bounds: Width={ext_x:.4f}, Height={ext_y:.4f}, Length={ext_z:.4f}")
    assert ext_y <= max(ext_x, ext_z) + 1e-4, f"Failed on {rp}: Height={ext_y} > Max({ext_x}, {ext_z})"
    print(f"   -> OK: Longer width laying flat! (Height {ext_y:.4f} <= Max({ext_x:.4f}, {ext_z:.4f}))")
