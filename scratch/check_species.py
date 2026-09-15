import re

with open("Assets/Scripts/Editor/SpeciesDataGenerator.cs", "r", encoding="utf-8") as f:
    c = f.read()

matches = re.findall(r'CreateOrUpdateSpecies\(folder,\s*"([^"]+)"', c)
for m in matches:
    print(m)
