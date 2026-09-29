import glob, os, json, struct

for p in sorted(glob.glob('Assets/Arts/Creatures/Sunlight Zone/*.glb')):
    with open(p, 'rb') as f:
        f.seek(12)
        cl, ct = struct.unpack('<II', f.read(8))
        d = json.loads(f.read(cl).decode('utf-8'))
        f.seek(12 + 8 + cl)
        b_len, b_type = struct.unpack('<II', f.read(8))
        bin_data = f.read(b_len)

    def get_accessor_data(acc_idx):
        acc = d['accessors'][acc_idx]
        bv = d['bufferViews'][acc['bufferView']]
        offset = bv.get('byteOffset', 0) + acc.get('byteOffset', 0)
        count = acc['count']
        fmt_map = {'SCALAR': 'f', 'VEC2': '2f', 'VEC3': '3f', 'VEC4': '4f'}
        fmt = fmt_map[acc['type']]
        stride = struct.calcsize(fmt)
        data = []
        for i in range(count):
            val = struct.unpack_from('<' + fmt, bin_data, offset + i * stride)
            data.append(val if len(val) > 1 else val[0])
        return data

    anims = d.get('animations', [])
    if anims:
        print('=== ' + os.path.basename(p))
        for a in anims:
            times = []
            for ch in a.get('channels', []):
                s = a['samplers'][ch['sampler']]
                t = get_accessor_data(s['input'])
                times.extend(t)
            duration = max(times) if times else 0
            channels_count = len(a.get('channels', []))
            print(f'  Anim "{a.get("name")}": duration={duration:.2f}s, channels={channels_count}')
