with open('Assets/Prefabs/Creatures/Sunlight Zone/Bottlenose Dolphin.prefab', 'r', encoding='utf-8') as f:
    text = f.read()

import re
print("--- Modifications in Bottlenose Dolphin.prefab ---")
for m in re.findall(r"target: \{fileID: ([^,]+).*?\n\s*propertyPath: ([^\n]+)\n\s*value: ([^\n]+)", text):
    print(f"  fileID: {m[0]}, prop: {m[1]}, val: {m[2]}")

print("--- Components in Bottlenose Dolphin.prefab ---")
for c in re.findall(r"--- !u!(\d+) &(\d+)\n([A-Za-z0-9_]+):", text):
    print(f"  Type {c[0]} (&{c[1]}): {c[2]}")
