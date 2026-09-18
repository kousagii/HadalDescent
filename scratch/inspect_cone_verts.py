import struct, zlib
from parse_fbx import read_fbx_vertices

# Let's check Cone_001.fbx and Cone_002.fbx vertices
for name in ["Cone_001.fbx", "Cone_002.fbx"]:
    p = "Assets/Arts/Environment/rocks-low-poly-starter-pack/source/" + name
    bounds = read_fbx_vertices(p)
    print(name, "bounds:", bounds)
