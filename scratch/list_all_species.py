import re, glob

for path in glob.glob('Assets/Scripts/Editor/SpeciesDefinitions/*.cs'):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    matches = re.findall(r'commonName\s*=\s*"([^"]+)".*?scientificName\s*=\s*"([^"]+)"', content, re.DOTALL)
    print(f'=== {path} ({len(matches)} species) ===')
    for m in matches:
        print(f'  {m[0]} ({m[1]})')
