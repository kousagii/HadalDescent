#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to generate and register all scientific species assets
/// for Hadal Descent with accurate biological depth ranges and hints.
///
/// Run via Unity Editor Menu: HadalDescent → Populate All Species (Sunlight Zone)
/// </summary>
public static class SpeciesDataGenerator
{
    [MenuItem("HadalDescent/Populate All Species (Sunlight Zone)")]
    public static void GenerateSunlightSpecies()
    {
        string folder = "Assets/Scripts/ScriptableObjects/Species";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects", "Species");
        }

        var createdAssets = new List<SpeciesData>();

        // 1. Clownfish (Actinopterygii)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "Clownfish", new SpeciesDataConfig
        {
            speciesId             = "clownfish_001",
            commonName            = "Clownfish",
            scientificName        = "Amphiprion ocellaris",
            taxonomicClass        = TaxonomicClass.Actinopterygii,
            zoneIndex             = 0,
            habitat               = "Shallow Indo-Pacific coral reefs (1–15 m)",
            depthRangeText        = "1–15 m",
            explorationHint       = "Search around sunny, shallow coral reef summits nestled within sea anemone patches.",
            characteristics       = "Bright orange body with three white bands outlined in black.",
            ecologicalRole        = "Lives with sea anemones and helps protect them from threats.",
            interestingFact       = "The largest male can change into a female if the female dies.",
            rdpReward             = 80,
            scanDifficulty        = 1,
            isStationary          = false,
            isShy                 = false,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.075f,
            instanceCount         = 5,
            wanderRadius          = 8f,
            moveSpeed             = 2.2f,
            fleeSpeed             = 5.0f,
            placeholderShape      = PlaceholderShape.Sphere,
            placeholderColor      = new Color(1.0f, 0.45f, 0.10f, 1f),
            placeholderScale      = new Vector3(0.5f, 0.5f, 0.5f),
            previewScaleMultiplier= 1.0f
        }));

        // 2. Bubble-tip Sea Anemone (Anthozoa)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BubbleTipAnemone", new SpeciesDataConfig
        {
            speciesId             = "anemone_001",
            commonName            = "Bubble-tip Sea Anemone",
            scientificName        = "Entacmaea quadricolor",
            taxonomicClass        = TaxonomicClass.Anthozoa,
            zoneIndex             = 0,
            habitat               = "Shallow coral reefs (0–200 m)",
            depthRangeText        = "0–200 m",
            explorationHint       = "Attached firmly to the upper surfaces of sunlit coral boulders.",
            characteristics       = "Many tentacles with rounded, bubble-like tips.",
            ecologicalRole        = "Provides shelter for clownfish and other small reef animals.",
            interestingFact       = "Its tentacles can change from bubble-like to long and flowing.",
            rdpReward             = 90,
            scanDifficulty        = 1,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.175f,
            instanceCount         = 4,
            placeholderShape      = PlaceholderShape.Cylinder,
            placeholderColor      = new Color(0.95f, 0.35f, 0.65f, 1f),
            placeholderScale      = new Vector3(1.2f, 0.8f, 1.2f),
            previewScaleMultiplier= 1.0f
        }));

        // 3. Fan Coral (Anthozoa)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "FanCoral", new SpeciesDataConfig
        {
            speciesId             = "fan_coral_001",
            commonName            = "Fan Coral",
            scientificName        = "Melithaea philippinensis",
            taxonomicClass        = TaxonomicClass.Anthozoa,
            zoneIndex             = 0,
            habitat               = "Shallow coral reefs and reef drop-offs (10–60 m)",
            depthRangeText        = "10–60 m",
            explorationHint       = "Look along sloping reef edges and drop-off rock walls exposed to steady currents.",
            characteristics       = "Branching, fan-shaped octocoral with a semi-rigid skeleton; forms upright, branching colonies.",
            ecologicalRole        = "Provides three-dimensional habitat and shelter for small reef organisms and contributes to coral-reef biodiversity.",
            interestingFact       = "Fan-shaped colonies grow across water currents to maximize food capture; their branches also provide shelter and habitat for small reef organisms.",
            rdpReward             = 85,
            scanDifficulty        = 1,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.05f,
            maxDepthFraction      = 0.30f,
            instanceCount         = 6,
            placeholderShape      = PlaceholderShape.Cube,
            placeholderColor      = new Color(0.92f, 0.25f, 0.35f, 1f),
            placeholderScale      = new Vector3(1.5f, 2.0f, 0.3f),
            previewScaleMultiplier= 1.0f
        }));

        // 4. Pacific Blue Sea Star (Asteroidea)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "PacificBlueSeaStar", new SpeciesDataConfig
        {
            speciesId             = "blue_seastar_001",
            commonName            = "Pacific Blue Sea Star",
            scientificName        = "Linckia laevigata",
            taxonomicClass        = TaxonomicClass.Asteroidea,
            zoneIndex             = 0,
            habitat               = "Shallow coral reefs (0–60 m)",
            depthRangeText        = "0–60 m",
            explorationHint       = "Resting atop sunny reef platforms and sandy coral rubble.",
            characteristics       = "Bright blue body with five long, slender arms.",
            ecologicalRole        = "Helps break down organic material on the seafloor.",
            interestingFact       = "It can regrow an arm after losing one.",
            rdpReward             = 80,
            scanDifficulty        = 1,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.30f,
            instanceCount         = 4,
            placeholderShape      = PlaceholderShape.Sphere,
            placeholderColor      = new Color(0.12f, 0.45f, 0.95f, 1f),
            placeholderScale      = new Vector3(1.0f, 0.3f, 1.0f),
            previewScaleMultiplier= 1.0f
        }));

        // 5. Giant Clam (Bivalvia)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "GiantClam", new SpeciesDataConfig
        {
            speciesId             = "giant_clam_001",
            commonName            = "Giant Clam",
            scientificName        = "Tridacna gigas",
            taxonomicClass        = TaxonomicClass.Bivalvia,
            zoneIndex             = 0,
            habitat               = "Shallow sunlit coral reef lagoons (0–35 m)",
            depthRangeText        = "0–35 m",
            explorationHint       = "Found embedded in sandy coral lagoons where bright sunlight penetrates.",
            characteristics       = "Huge shell with colorful blue, green, or brown tissue.",
            ecologicalRole        = "Supports coral reef ecosystems and filters seawater.",
            interestingFact       = "It is the largest living bivalve.",
            rdpReward             = 110,
            scanDifficulty        = 2,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.175f,
            instanceCount         = 3,
            placeholderShape      = PlaceholderShape.Cube,
            placeholderColor      = new Color(0.10f, 0.85f, 0.75f, 1f),
            placeholderScale      = new Vector3(2.0f, 1.2f, 1.5f),
            previewScaleMultiplier= 1.0f
        }));

        // 6. Bigfin Reef Squid (Cephalopoda)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BigfinReefSquid", new SpeciesDataConfig
        {
            speciesId             = "reef_squid_001",
            commonName            = "Bigfin Reef Squid",
            scientificName        = "Sepioteuthis lessoniana",
            taxonomicClass        = TaxonomicClass.Cephalopoda,
            zoneIndex             = 0,
            habitat               = "Shallow coastal reefs and seagrass beds (0–100 m)",
            depthRangeText        = "0–100 m",
            explorationHint       = "Cruises gracefully over seagrass beds and midwater channels; shy to approach.",
            characteristics       = "Long oval body with wide fins along its sides.",
            ecologicalRole        = "Hunts small fish, crustaceans, and other marine animals.",
            interestingFact       = "It can rapidly change its body color.",
            rdpReward             = 95,
            scanDifficulty        = 2,
            isStationary          = false,
            isShy                 = true,
            preferredBiome        = BiomeBand.Soft,
            minDepthFraction      = 0.02f,
            maxDepthFraction      = 0.50f,
            instanceCount         = 3,
            wanderRadius          = 18f,
            moveSpeed             = 3.2f,
            fleeSpeed             = 6.5f,
            placeholderShape      = PlaceholderShape.Capsule,
            placeholderColor      = new Color(0.85f, 0.95f, 1.0f, 1f),
            placeholderScale      = new Vector3(0.8f, 1.8f, 0.8f),
            previewScaleMultiplier= 1.0f
        }));

        // 7. Blacktip Reef Shark (Chondrichthyes)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BlacktipReefShark", new SpeciesDataConfig
        {
            speciesId             = "blacktip_shark_001",
            commonName            = "Blacktip Reef Shark",
            scientificName        = "Carcharhinus melanopterus",
            taxonomicClass        = TaxonomicClass.Chondrichthyes,
            zoneIndex             = 0,
            habitat               = "Shallow coral reef flats and drop-offs (0–75 m)",
            depthRangeText        = "0–75 m",
            explorationHint       = "Patrols along reef edges and open drop-offs in continuous forward motion.",
            characteristics       = "Gray-brown body with black-tipped fins.",
            ecologicalRole        = "Helps control reef fish and cephalopod populations.",
            interestingFact       = "It can keep swimming to move water over its gills.",
            rdpReward             = 120,
            scanDifficulty        = 3,
            isStationary          = false,
            isShy                 = false,
            preferredBiome        = BiomeBand.OpenWater,
            minDepthFraction      = 0.025f,
            maxDepthFraction      = 0.375f,
            instanceCount         = 2,
            wanderRadius          = 30f,
            moveSpeed             = 4.5f,
            fleeSpeed             = 7.0f,
            placeholderShape      = PlaceholderShape.Capsule,
            placeholderColor      = new Color(0.40f, 0.45f, 0.50f, 1f),
            placeholderScale      = new Vector3(1.2f, 3.5f, 1.2f),
            previewScaleMultiplier= 0.8f
        }));

        // 8. Blue-spotted Ribbontail Ray (Chondrichthyes)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BlueSpottedRibbontailRay", new SpeciesDataConfig
        {
            speciesId             = "ribbontail_ray_001",
            commonName            = "Blue-spotted Ribbontail Ray",
            scientificName        = "Taeniura lymma",
            taxonomicClass        = TaxonomicClass.Chondrichthyes,
            zoneIndex             = 0,
            habitat               = "Shallow reef sand flats and lagoons (0–20 m)",
            depthRangeText        = "0–20 m",
            explorationHint       = "Glides over shallow sandy channels between coral formations.",
            characteristics       = "Flat oval body covered with bright blue spots.",
            ecologicalRole        = "Hunts small fish and invertebrates on the seafloor.",
            interestingFact       = "Its tail has venomous spines for defense.",
            rdpReward             = 100,
            scanDifficulty        = 2,
            isStationary          = false,
            isShy                 = true,
            preferredBiome        = BiomeBand.Soft,
            minDepthFraction      = 0.01f,
            maxDepthFraction      = 0.10f,
            instanceCount         = 3,
            wanderRadius          = 14f,
            moveSpeed             = 2.6f,
            fleeSpeed             = 5.5f,
            placeholderShape      = PlaceholderShape.Cylinder,
            placeholderColor      = new Color(0.20f, 0.65f, 0.90f, 1f),
            placeholderScale      = new Vector3(1.8f, 0.2f, 1.8f),
            previewScaleMultiplier= 1.0f
        }));

        // 9. Purple Sea Urchin (Echinoidea)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "PurpleSeaUrchin", new SpeciesDataConfig
        {
            speciesId             = "purple_urchin_001",
            commonName            = "Purple Sea Urchin",
            scientificName        = "Strongylocentrotus purpuratus",
            taxonomicClass        = TaxonomicClass.Echinoidea,
            zoneIndex             = 0,
            habitat               = "Rocky coastal areas (0–161 m)",
            depthRangeText        = "0–161 m",
            explorationHint       = "Clustered in groups on rocky boulders and vertical trench walls.",
            characteristics       = "Round body covered with sharp purple spines.",
            ecologicalRole        = "Grazes on algae and helps control plant growth.",
            interestingFact       = "It is widely used in biological research.",
            rdpReward             = 75,
            scanDifficulty        = 1,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Rock,
            minDepthFraction      = 0.05f,
            maxDepthFraction      = 0.80f,
            instanceCount         = 6,
            placeholderShape      = PlaceholderShape.Sphere,
            placeholderColor      = new Color(0.65f, 0.15f, 0.85f, 1f),
            placeholderScale      = new Vector3(0.6f, 0.6f, 0.6f),
            previewScaleMultiplier= 1.0f
        }));

        // 10. Dotted Sea Hare (Gastropoda)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "DottedSeaHare", new SpeciesDataConfig
        {
            speciesId             = "sea_hare_001",
            commonName            = "Dotted Sea Hare",
            scientificName        = "Aplysia punctata",
            taxonomicClass        = TaxonomicClass.Gastropoda,
            zoneIndex             = 0,
            habitat               = "Shallow seaweed beds and rocky shores (0–83 m)",
            depthRangeText        = "0–83 m",
            explorationHint       = "Crawls slowly over soft sediment and weed-covered rocks.",
            characteristics       = "Soft body with dark spots and a small internal shell.",
            ecologicalRole        = "Eats algae and helps control seaweed growth.",
            interestingFact       = "It can release purple ink when threatened.",
            rdpReward             = 80,
            scanDifficulty        = 1,
            isStationary          = true,
            isShy                 = false,
            preferredBiome        = BiomeBand.Soft,
            minDepthFraction      = 0.01f,
            maxDepthFraction      = 0.40f,
            instanceCount         = 4,
            placeholderShape      = PlaceholderShape.Capsule,
            placeholderColor      = new Color(0.45f, 0.55f, 0.20f, 1f),
            placeholderScale      = new Vector3(0.6f, 1.0f, 0.6f),
            previewScaleMultiplier= 1.0f
        }));

        // 11. Bluebottle (Hydrozoa)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "Bluebottle", new SpeciesDataConfig
        {
            speciesId             = "bluebottle_001",
            commonName            = "Bluebottle",
            scientificName        = "Physalia utriculus",
            taxonomicClass        = TaxonomicClass.Hydrozoa,
            zoneIndex             = 0,
            habitat               = "Ocean surface (0–3 m)",
            depthRangeText        = "0–3 m",
            explorationHint       = "Drifts right beneath the glistening water surface near Depth 0m.",
            characteristics       = "Gas-filled float with long stinging structures.",
            ecologicalRole        = "Catches small fish and plankton.",
            interestingFact       = "It is a colony made of many specialized parts working together.",
            rdpReward             = 105,
            scanDifficulty        = 2,
            isStationary          = false,
            isShy                 = false,
            preferredBiome        = BiomeBand.OpenWater,
            minDepthFraction      = 0.00f,
            maxDepthFraction      = 0.015f,
            instanceCount         = 3,
            wanderRadius          = 20f,
            moveSpeed             = 1.2f,
            fleeSpeed             = 2.0f,
            placeholderShape      = PlaceholderShape.Sphere,
            placeholderColor      = new Color(0.15f, 0.60f, 1.0f, 0.8f),
            placeholderScale      = new Vector3(0.8f, 0.8f, 0.8f),
            previewScaleMultiplier= 1.0f
        }));

        // 12. Ornate Spiny Lobster (Malacostraca)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "OrnateSpinyLobster", new SpeciesDataConfig
        {
            speciesId             = "spiny_lobster_001",
            commonName            = "Ornate Spiny Lobster",
            scientificName        = "Panulirus ornatus",
            taxonomicClass        = TaxonomicClass.Malacostraca,
            zoneIndex             = 0,
            habitat               = "Shallow coral reefs and shelf sediments (1–50 m)",
            depthRangeText        = "1–50 m",
            explorationHint       = "Hides under rocky overhangs and deep reef crevices.",
            characteristics       = "Colorful patterned body with long spiny antennae.",
            ecologicalRole        = "Predator and scavenger that feeds on marine animals.",
            interestingFact       = "Its young spend a long time drifting in the open ocean.",
            rdpReward             = 115,
            scanDifficulty        = 2,
            isStationary          = true,
            isShy                 = true,
            preferredBiome        = BiomeBand.Rock,
            minDepthFraction      = 0.01f,
            maxDepthFraction      = 0.25f,
            instanceCount         = 3,
            placeholderShape      = PlaceholderShape.Cube,
            placeholderColor      = new Color(0.85f, 0.40f, 0.15f, 1f),
            placeholderScale      = new Vector3(1.2f, 0.5f, 1.5f),
            previewScaleMultiplier= 1.0f
        }));

        // 13. Banded Sea Krait (Reptilia)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BandedSeaKrait", new SpeciesDataConfig
        {
            speciesId             = "sea_krait_001",
            commonName            = "Banded Sea Krait",
            scientificName        = "Laticauda colubrina",
            taxonomicClass        = TaxonomicClass.Reptilia,
            zoneIndex             = 0,
            habitat               = "Shallow coral reefs, mangroves, and rocky shores (0–10 m)",
            depthRangeText        = "0–10 m",
            explorationHint       = "Swims gracefully through shallow coral branches near the surface.",
            characteristics       = "Black stripes and a yellow snout, with a paddle-like tail for use in swimming.",
            ecologicalRole        = "Predator that helps control fish and eel populations.",
            interestingFact       = "It can hunt together with other fish.",
            rdpReward             = 125,
            scanDifficulty        = 3,
            isStationary          = false,
            isShy                 = true,
            preferredBiome        = BiomeBand.Hard,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.05f,
            instanceCount         = 2,
            wanderRadius          = 16f,
            moveSpeed             = 3.0f,
            fleeSpeed             = 6.0f,
            placeholderShape      = PlaceholderShape.Capsule,
            placeholderColor      = new Color(0.95f, 0.85f, 0.10f, 1f),
            placeholderScale      = new Vector3(0.4f, 2.5f, 0.4f),
            previewScaleMultiplier= 0.9f
        }));

        // 14. Spotted Jellyfish (Scyphozoa)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "SpottedJellyfish", new SpeciesDataConfig
        {
            speciesId             = "spotted_jellyfish_001",
            commonName            = "Spotted Jellyfish",
            scientificName        = "Mastigias papua",
            taxonomicClass        = TaxonomicClass.Scyphozoa,
            zoneIndex             = 0,
            habitat               = "Shallow sunlit lagoons and coves (1–20 m)",
            depthRangeText        = "1–20 m",
            explorationHint       = "Drifts in open sunlit water where solar rays illuminate the lagoon.",
            characteristics       = "Golden-brown bell with white spots.",
            ecologicalRole        = "Feeds on small organisms and benefits from its photosynthetic partners.",
            interestingFact       = "It moves during the day to get more sunlight.",
            rdpReward             = 90,
            scanDifficulty        = 1,
            isStationary          = false,
            isShy                 = false,
            preferredBiome        = BiomeBand.OpenWater,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.10f,
            instanceCount         = 4,
            wanderRadius          = 12f,
            moveSpeed             = 1.5f,
            fleeSpeed             = 3.0f,
            placeholderShape      = PlaceholderShape.Sphere,
            placeholderColor      = new Color(0.90f, 0.70f, 0.35f, 0.9f),
            placeholderScale      = new Vector3(1.2f, 1.2f, 1.2f),
            previewScaleMultiplier= 1.0f
        }));

        // 15. Indo-Pacific Bottlenose Dolphin (Mammalia)
        createdAssets.Add(CreateOrUpdateSpecies(folder, "IndoPacificBottlenoseDolphin", new SpeciesDataConfig
        {
            speciesId             = "dolphin_001",
            commonName            = "Indo-Pacific Bottlenose Dolphin",
            scientificName        = "Tursiops aduncus",
            taxonomicClass        = TaxonomicClass.Mammalia,
            zoneIndex             = 0,
            habitat               = "Shallow coastal, estuarine, and reef-associated tropical waters (0 - 2000 m, usually 1 - 50 m)",
            depthRangeText        = "1–50 m",
            explorationHint       = "Swims in social pods across coastal and reef-associated surface waters.",
            characteristics       = "Slender body, long dark rostrum, and distinctive dark spots on the belly/underside as adults",
            ecologicalRole        = "Coastal mesopredator keeping reef fish, cephalopod, and crustacean populations in check",
            interestingFact       = "Forms resident pods in Philippine coastal areas and frequently leaps out of water while social foraging",
            rdpReward             = 140,
            scanDifficulty        = 3,
            isStationary          = false,
            isShy                 = false,
            preferredBiome        = BiomeBand.OpenWater,
            minDepthFraction      = 0.005f,
            maxDepthFraction      = 0.25f,
            instanceCount         = 3,
            wanderRadius          = 35f,
            moveSpeed             = 5.0f,
            fleeSpeed             = 8.0f,
            placeholderShape      = PlaceholderShape.Capsule,
            placeholderColor      = new Color(0.30f, 0.60f, 0.85f, 1f),
            placeholderScale      = new Vector3(1.0f, 3.2f, 1.0f),
            previewScaleMultiplier= 0.75f
        }));

        UpdateRegistry(createdAssets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SpeciesDataGenerator] Successfully generated and aligned all {createdAssets.Count} official scientific species with Bestiary hints and depth data!");
    }

    private static SpeciesData CreateOrUpdateSpecies(string folder, string assetName, SpeciesDataConfig config)
    {
        string path = $"{folder}/{assetName}.asset";
        SpeciesData asset = AssetDatabase.LoadAssetAtPath<SpeciesData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SpeciesData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.speciesId             = config.speciesId;
        asset.commonName            = config.commonName;
        asset.scientificName        = config.scientificName;
        asset.taxonomicClass        = config.taxonomicClass;
        asset.zoneIndex             = config.zoneIndex;
        asset.habitat               = config.habitat;
        asset.depthRangeText        = config.depthRangeText;
        asset.explorationHint       = config.explorationHint;
        asset.characteristics       = config.characteristics;
        asset.ecologicalRole        = config.ecologicalRole;
        asset.interestingFact       = config.interestingFact;
        asset.rdpReward             = config.rdpReward;
        asset.scanDifficulty        = config.scanDifficulty;
        asset.isStationary          = config.isStationary;
        asset.isShy                 = config.isShy;
        asset.preferredBiome        = config.preferredBiome;
        asset.minDepthFraction      = config.minDepthFraction;
        asset.maxDepthFraction      = config.maxDepthFraction;
        asset.instanceCount         = config.instanceCount;
        asset.wanderRadius          = config.wanderRadius;
        asset.moveSpeed             = config.moveSpeed;
        asset.fleeSpeed             = config.fleeSpeed;
        asset.placeholderShape      = config.placeholderShape;
        asset.placeholderColor      = config.placeholderColor;
        asset.placeholderScale      = config.placeholderScale;
        asset.previewScaleMultiplier= config.previewScaleMultiplier;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void UpdateRegistry(List<SpeciesData> speciesList)
    {
        string registryPath = "Assets/Resources/SpeciesRegistry.asset";
        var registry = AssetDatabase.LoadAssetAtPath<SpeciesRegistry>(registryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<SpeciesRegistry>();
            AssetDatabase.CreateAsset(registry, registryPath);
        }

        var serializedObject = new SerializedObject(registry);
        var allSpeciesProp = serializedObject.FindProperty("allSpecies");

        var existingList = new List<SpeciesData>();
        for (int i = 0; i < allSpeciesProp.arraySize; i++)
        {
            var item = allSpeciesProp.GetArrayElementAtIndex(i).objectReferenceValue as SpeciesData;
            if (item != null && item.zoneIndex != 0)
                existingList.Add(item);
        }

        existingList.AddRange(speciesList);

        allSpeciesProp.ClearArray();
        for (int i = 0; i < existingList.Count; i++)
        {
            allSpeciesProp.InsertArrayElementAtIndex(i);
            allSpeciesProp.GetArrayElementAtIndex(i).objectReferenceValue = existingList[i];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);
    }

    private struct SpeciesDataConfig
    {
        public string speciesId;
        public string commonName;
        public string scientificName;
        public TaxonomicClass taxonomicClass;
        public int zoneIndex;
        public string habitat;
        public string depthRangeText;
        public string explorationHint;
        public string characteristics;
        public string ecologicalRole;
        public string interestingFact;
        public int rdpReward;
        public int scanDifficulty;
        public bool isStationary;
        public bool isShy;
        public BiomeBand preferredBiome;
        public float minDepthFraction;
        public float maxDepthFraction;
        public int instanceCount;
        public float wanderRadius;
        public float moveSpeed;
        public float fleeSpeed;
        public PlaceholderShape placeholderShape;
        public Color placeholderColor;
        public Vector3 placeholderScale;
        public float previewScaleMultiplier;
    }
}
#endif