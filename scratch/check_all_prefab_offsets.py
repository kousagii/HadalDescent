import glob, os, re

prefabs = glob.glob("Assets/Prefabs/Creatures/**/*.prefab", recursive=True)
for p in prefabs:
    with open(p, "r", encoding="utf-8") as f:
        content = f.read()
    center_match = re.search(r"m_Center:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^}]+)\}", content)
    size_match = re.search(r"m_Size:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^}]+)\}", content)
    c_str = center_match.groups() if center_match else ("N/A", "N/A", "N/A")
    s_str = size_match.groups() if size_match else ("N/A", "N/A", "N/A")
    print(f"{os.path.basename(p):<35} | Center: ({float(c_str[0]):.2f}, {float(c_str[1]):.2f}, {float(c_str[2]):.2f}) | Size: ({float(s_str[0]):.2f}, {float(s_str[1]):.2f}, {float(s_str[2]):.2f})" if center_match else f"{os.path.basename(p):<35} | No BoxCollider")
