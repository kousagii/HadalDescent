import re

with open("Assets/Scripts/Editor/SpeciesDataGenerator.cs", "r", encoding="utf-8") as f:
    c = f.read()

pattern = r'CreateOrUpdateSpecies\(folder,\s*"([^"]+)",\s*new SpeciesDataConfig\s*\{([^}]+)\}'
for name, body in re.findall(pattern, c, re.DOTALL):
    m = re.search(r'preferredBiome\s*=\s*BiomeBand\.(\w+)', body)
    b = m.group(1) if m else "UNKNOWN"
    print(f"{name:<26} -> {b}")
