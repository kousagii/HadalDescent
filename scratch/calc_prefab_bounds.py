import struct, zlib, os, glob, re

# Let's inspect each prefab in SunlightProps.asset!
with open("Assets/Scripts/ScriptableObjects/Environment/SunlightProps.asset", "r") as f:
    asset_txt = f.read()

# Let's find all metas once
guid_to_path = {}
for m in glob.glob("Assets/**/*.meta", recursive=True):
    try:
        with open(m, "r", encoding="utf-8", errors="ignore") as mf:
            txt = mf.read()
            g = re.search(r'guid:\s*([a-f0-9]{32})', txt)
            if g:
                guid_to_path[g.group(1)] = m[:-5]
    except:
        pass

entries = re.findall(r'guid:\s*([a-f0-9]{32})', asset_txt)
for guid in set(entries):
    if guid in guid_to_path:
        print(f"GUID {guid} -> {guid_to_path[guid]}")

