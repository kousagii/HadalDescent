import glob, os, re

controllers = glob.glob("Assets/Animations/*.controller")

for c in controllers:
    print("=" * 60)
    print("Controller:", os.path.basename(c))
    with open(c, "r", encoding="utf-8") as f:
        text = f.read()
    states = re.findall(r"m_Name:\s*([^\n]+)\s*\n\s*m_Speed:\s*([^\n]+).*?m_Motion:\s*\{fileID:\s*([^,]+),\s*guid:\s*([^,]+)", text, re.DOTALL)
    for s in states:
        print(f"  State: {s[0]}, speed: {s[1]}, motion fileID: {s[2]}, guid: {s[3]}")
    default_state = re.search(r"m_DefaultState:\s*\{fileID:\s*([^}]+)\}", text)
    if default_state:
        print(f"  DefaultState: {default_state.group(1)}")
