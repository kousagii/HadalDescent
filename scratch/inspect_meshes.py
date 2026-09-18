import os, glob

# Let's inspect the FBX files in Assets/Arts/Environment/rocks-low-poly-starter-pack/source/
# FBX files often contain ASCII or binary vertex data. Let's see if we can parse bounding boxes or check the prefabs.

prefabs = glob.glob("Assets/Prefabs/Environment/Rocks/*.prefab")
print(f"Found {len(prefabs)} rock prefabs")
for p in prefabs[:10]:
    with open(p, "r", encoding="utf-8") as f:
        txt = f.read()
    # Check rotation modifications
    import re
    rot = re.findall(r'm_LocalRotation\.(x|y|z|w)\s+value:\s+([-\d\.]+)', txt)
    print(p, rot)
