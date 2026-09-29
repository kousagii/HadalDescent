import json, struct

with open('Assets/Arts/Creatures/Sunlight Zone/bottlenose dolphin.glb', 'rb') as f:
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

for a in d.get('animations', []):
    print("================== ANIMATION:", a.get('name'))
    for ch in a.get('channels', []):
        target = ch.get('target', {})
        node = d['nodes'][target.get('node')].get('name')
        path = target.get('path')
        if 'translation' in path or 'rotation' in path:
            sampler = a['samplers'][ch['sampler']]
            times = get_accessor_data(sampler['input'])
            vals = get_accessor_data(sampler['output'])
            if 'translation' in path:
                print(f"  {node}.translation: {vals[0]} -> {vals[len(vals)//2]} -> {vals[-1]}")
            elif node in ['Bone', 'Armature', 'Icosphere']:
                print(f"  {node}.rotation: {vals[0]} -> {vals[-1]}")
