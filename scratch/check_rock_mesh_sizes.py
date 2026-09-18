import glob, os, re

# Let's inspect the FBX files in binary or text
# FBX files have vertices or bounding boxes
# Better yet, let's write a quick C# editor utility or inspect with python
# Since we have dotnet or python, let's see if we can parse the FBX bounding box or vertices
def parse_fbx(path):
    with open(path, 'rb') as f:
        data = f.read()
    # search for 'Vertices' or 'BBox' or 'GeometricTranslation'
    # FBX binary has node names
    # Let's see if it's binary or ASCII
    is_ascii = b"Kaydara FBX Header" in data[:100] and b"FBX" in data[:20] and not data.startswith(b"Kaydara FBX Binary")
    return len(data), is_ascii

for p in sorted(glob.glob("Assets/Arts/Environment/rocks-low-poly-starter-pack/source/*.fbx"))[:15]:
    size, ascii_mode = parse_fbx(p)
    print(os.path.basename(p), size, "ASCII" if ascii_mode else "BINARY")
