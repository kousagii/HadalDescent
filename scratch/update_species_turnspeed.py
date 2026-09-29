import glob, os, re

species_configs = {
    "Clownfish": 120,
    "BubbleTipAnemone": 0,
    "FanCoral": 0,
    "PacificBlueSeaStar": 25,
    "GiantClam": 0,
    "BigfinReefSquid": 80,
    "BlacktipReefShark": 60,
    "BlueSpottedRibbontailRay": 50,
    "TubularBlueSponge": 0,
    "GeographyConeSnail": 30,
    "Bluebottle": 20,
    "OrnateSpinyLobster": 50,
    "BottlenoseDolphin": 75,
    "BandedSeaKrait": 55,
    "SpottedJellyfish": 40,
}

assets = glob.glob("Assets/Scripts/ScriptableObjects/Species/*.asset")
updated = 0

for path in assets:
    base = os.path.splitext(os.path.basename(path))[0]
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    speed = species_configs.get(base, 65)

    if "turnSpeed:" not in content:
        # Insert after returnThreshold or fleeSpeed
        if "  returnThreshold:" in content:
            new_content = re.sub(r"(  returnThreshold:\s*\d+(\.\d+)?)", r"\1\n  turnSpeed: " + str(speed), content)
        elif "  fleeSpeed:" in content:
            new_content = re.sub(r"(  fleeSpeed:\s*\d+(\.\d+)?)", r"\1\n  turnSpeed: " + str(speed), content)
        else:
            new_content = content + f"\n  turnSpeed: {speed}\n"

        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
        updated += 1
    else:
        # Update existing turnSpeed if configured
        if base in species_configs:
            new_content = re.sub(r"turnSpeed:\s*\d+(\.\d+)?", f"turnSpeed: {speed}", content)
            if new_content != content:
                with open(path, "w", encoding="utf-8") as f:
                    f.write(new_content)
                updated += 1

print(f"Updated {updated} species asset files with turnSpeed.")
