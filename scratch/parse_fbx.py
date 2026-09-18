import struct, zlib, os, glob

def read_fbx_vertices(filepath):
    with open(filepath, 'rb') as f:
        content = f.read()
    
    # search for 'Vertices'
    idx = 0
    while True:
        idx = content.find(b'Vertices', idx)
        if idx == -1:
            break
        # FBX property header comes after name
        # In FBX binary format:
        # After 'Vertices', there might be property type 'd' (array of double) or 'f' (array of float)
        # Let's search around idx for array header
        chunk = content[idx:idx+100]
        # Let's search for 'd' type array
        d_idx = content.find(b'd', idx)
        if d_idx != -1 and d_idx - idx < 20:
            # Array length (4 bytes), encoding (4 bytes), compressed length (4 bytes)
            arr_len, enc, comp_len = struct.unpack('<III', content[d_idx+1:d_idx+13])
            # print(f"Found 'd' array: arr_len={arr_len}, enc={enc}, comp_len={comp_len}")
            raw_data = content[d_idx+13:d_idx+13+comp_len] if enc == 1 else content[d_idx+13:d_idx+13+arr_len*8]
            if enc == 1:
                try:
                    raw_data = zlib.decompress(raw_data)
                except Exception as e:
                    idx += 8
                    continue
            coords = struct.unpack(f'<{arr_len}d', raw_data)
            xs = coords[0::3]
            ys = coords[1::3]
            zs = coords[2::3]
            return (min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))
        idx += 8
    return None

for p in sorted(glob.glob("Assets/Arts/Environment/rocks-low-poly-starter-pack/source/*.fbx")):
    res = read_fbx_vertices(p)
    if res:
        x, y, z = res
        dx = x[1] - x[0]
        dy = y[1] - y[0]
        dz = z[1] - z[0]
        print(f"{os.path.basename(p):20s} size: X={dx:.3f}, Y={dy:.3f}, Z={dz:.3f}")
