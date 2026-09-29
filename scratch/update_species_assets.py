import glob, os, re

# Map common species names to locomotion attributes
# LocomotionArchetype:
# 0 = PelagicCruiser
# 1 = HoverBurst
# 2 = PulsatileJetter
# 3 = BenthicFollower
# 4 = SurfaceDrifter
# 5 = Serpentine
# 6 = Sessile

species_configs = {
    "Clownfish": { "locomotionArchetype": 1, "pulseFrequency": 1.2, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 3.5, "undulationAmplitude": 15, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "BubbleTipAnemone": { "locomotionArchetype": 6, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "FanCoral": { "locomotionArchetype": 6, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "PacificBlueSeaStar": { "locomotionArchetype": 3, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 1.0, "undulationAmplitude": 5, "benthicSurfaceOffset": 0.15, "minCruiseSpeedFraction": 0.0 },
    "GiantClam": { "locomotionArchetype": 6, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "BigfinReefSquid": { "locomotionArchetype": 2, "pulseFrequency": 1.1, "pulseDutyCycle": 0.35, "bankingAngle": 15, "undulationFrequency": 2.5, "undulationAmplitude": 10, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.1 },
    "BlacktipReefShark": { "locomotionArchetype": 0, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 25, "undulationFrequency": 1.6, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.35 },
    "BlueSpottedRibbontailRay": { "locomotionArchetype": 3, "pulseFrequency": 1.0, "pulseDutyCycle": 0.35, "bankingAngle": 10, "undulationFrequency": 1.8, "undulationAmplitude": 8, "benthicSurfaceOffset": 0.45, "minCruiseSpeedFraction": 0.0 },
    "TubularBlueSponge": { "locomotionArchetype": 6, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "GeographyConeSnail": { "locomotionArchetype": 3, "pulseFrequency": 0.5, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 0.5, "undulationAmplitude": 2, "benthicSurfaceOffset": 0.15, "minCruiseSpeedFraction": 0.0 },
    "Bluebottle": { "locomotionArchetype": 4, "pulseFrequency": 0.5, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 1.0, "undulationAmplitude": 5, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "OrnateSpinyLobster": { "locomotionArchetype": 3, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 1.5, "undulationAmplitude": 6, "benthicSurfaceOffset": 0.30, "minCruiseSpeedFraction": 0.0 },
    "BottlenoseDolphin": { "locomotionArchetype": 0, "pulseFrequency": 1.0, "pulseDutyCycle": 0.35, "bankingAngle": 35, "undulationFrequency": 2.2, "undulationAmplitude": 16, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.45 },
    "BandedSeaKrait": { "locomotionArchetype": 5, "pulseFrequency": 1.0, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.2, "undulationAmplitude": 24, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
    "SpottedJellyfish": { "locomotionArchetype": 2, "pulseFrequency": 0.65, "pulseDutyCycle": 0.40, "bankingAngle": 0, "undulationFrequency": 1.0, "undulationAmplitude": 5, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 },
}

assets = glob.glob("Assets/Scripts/ScriptableObjects/Species/*.asset")
updated = 0

for path in assets:
    base = os.path.splitext(os.path.basename(path))[0]
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Determine config
    cfg = species_configs.get(base)
    if not cfg:
        # Fallback based on stationary
        is_stat = re.search(r"isStationary:\s*(\d+)", content)
        is_stat = int(is_stat.group(1)) if is_stat else 0
        if is_stat == 1:
            cfg = { "locomotionArchetype": 6, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 }
        elif "Squid" in base or "Jellyfish" in base:
            cfg = { "locomotionArchetype": 2, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 10, "undulationFrequency": 2.0, "undulationAmplitude": 10, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 }
        elif "Ray" in base or "Shrimp" in base or "Urchin" in base or "Star" in base or "Snail" in base:
            cfg = { "locomotionArchetype": 3, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 1.5, "undulationAmplitude": 8, "benthicSurfaceOffset": 0.35, "minCruiseSpeedFraction": 0.0 }
        elif "Ribbonfish" in base:
            cfg = { "locomotionArchetype": 5, "pulseFrequency": 1.0, "pulseDutyCycle": 0.35, "bankingAngle": 0, "undulationFrequency": 2.2, "undulationAmplitude": 20, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.0 }
        else:
            cfg = { "locomotionArchetype": 0, "pulseFrequency": 0.8, "pulseDutyCycle": 0.35, "bankingAngle": 20, "undulationFrequency": 2.0, "undulationAmplitude": 12, "benthicSurfaceOffset": 0.4, "minCruiseSpeedFraction": 0.25 }

    # Check if locomotionArchetype already exists in content
    if "locomotionArchetype:" not in content:
        # Insert before modelPrefab
        loco_block = f"""  locomotionArchetype: {cfg['locomotionArchetype']}
  pulseFrequency: {cfg['pulseFrequency']}
  pulseDutyCycle: {cfg['pulseDutyCycle']}
  bankingAngle: {cfg['bankingAngle']}
  undulationFrequency: {cfg['undulationFrequency']}
  undulationAmplitude: {cfg['undulationAmplitude']}
  benthicSurfaceOffset: {cfg['benthicSurfaceOffset']}
  minCruiseSpeedFraction: {cfg['minCruiseSpeedFraction']}
"""
        if "  modelPrefab:" in content:
            new_content = content.replace("  modelPrefab:", loco_block + "  modelPrefab:")
        else:
            new_content = content + "\n" + loco_block

        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
        updated += 1

print(f"Updated {updated} species asset files.")
