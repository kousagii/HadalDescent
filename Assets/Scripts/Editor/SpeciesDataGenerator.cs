#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to generate and register all scientific species assets
/// for Hadal Descent based on official marine biological data.
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

        // 1. Clownfish
        createdAssets.Add(CreateOrUpdateSpecies(folder, "Clownfish", new SpeciesDataConfig
        {
            speciesId        = "clownfish_001",
            commonName       = "Clownfish",
            scientificName   = "Amphiprion ocellaris",
            taxonomicClass   = TaxonomicClass.Actinopterygii,
            zoneIndex        = 0,
            habitat          = "Shallow Indo-Pacific coral reefs (1–15 m)",
            characteristics  = "Bright orange body with three black-bordered white vertical bands.",
            ecologicalRole   = "Lives mutualistically with sea anemones, protecting them from predators.",
            interestingFact  = "The largest male can change into a breeding female if the dominant female dies.",
            rdpReward        = 80,
            scanDifficulty   = 1, // Easy
            isStationary     = false,
            isShy            = false,
            preferredBiome   = BiomeBand.Hard, // Coral
            minDepthFraction = 0.005f, // 1m
            maxDepthFraction = 0.08f,  // 15m
            instanceCount    = 5,
            wanderRadius     = 8f,
            moveSpeed        = 2.2f,
            fleeSpeed        = 5.0f,
            placeholderShape = PlaceholderShape.Sphere,
            placeholderColor = new Color(1.0f, 0.45f, 0.10f, 1f),
            placeholderScale = new Vector3(0.5f, 0.5f, 0.5f)
        }));

        // 2. Bubble-tip Sea Anemone
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BubbleTipAnemone", new SpeciesDataConfig
        {
            speciesId        = "anemone_001",
            commonName       = "Bubble-tip Sea Anemone",
            scientificName   = "Entacmaea quadricolor",
            taxonomicClass   = TaxonomicClass.Anthozoa,
            zoneIndex        = 0,
            habitat          = "Shallow coral reefs (0–200 m)",
            characteristics  = "Numerous tentacles with rounded, bubble-like bulbous tips.",
            ecologicalRole   = "Provides shelter for clownfish and small reef invertebrates.",
            interestingFact  = "Its tentacles can morph dynamically from bulbous bubbles to flowing streamers.",
            rdpReward        = 90,
            scanDifficulty   = 1,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Hard,
            minDepthFraction = 0.01f,
            maxDepthFraction = 0.40f,
            instanceCount    = 4,
            placeholderShape = PlaceholderShape.Cylinder,
            placeholderColor = new Color(0.95f, 0.35f, 0.65f, 1f),
            placeholderScale = new Vector3(1.2f, 0.8f, 1.2f)
        }));

        // 3. Fan Coral
        createdAssets.Add(CreateOrUpdateSpecies(folder, "FanCoral", new SpeciesDataConfig
        {
            speciesId        = "fan_coral_001",
            commonName       = "Fan Coral",
            scientificName   = "Melithaea philippinensis",
            taxonomicClass   = TaxonomicClass.Anthozoa,
            zoneIndex        = 0,
            habitat          = "Shallow coral reefs and reef drop-offs",
            characteristics  = "Branching, fan-shaped octocoral with a semi-rigid calcified skeleton.",
            ecologicalRole   = "Provides 3D structural shelter for small organisms and filter-feeds plankton.",
            interestingFact  = "Grows oriented perpendicular to water currents to maximize particle capture.",
            rdpReward        = 85,
            scanDifficulty   = 1,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Hard,
            minDepthFraction = 0.05f,
            maxDepthFraction = 0.50f,
            instanceCount    = 6,
            placeholderShape = PlaceholderShape.Cube,
            placeholderColor = new Color(0.92f, 0.25f, 0.35f, 1f),
            placeholderScale = new Vector3(1.5f, 2.0f, 0.3f)
        }));

        // 4. Pacific Blue Sea Star
        createdAssets.Add(CreateOrUpdateSpecies(folder, "PacificBlueSeaStar", new SpeciesDataConfig
        {
            speciesId        = "blue_seastar_001",
            commonName       = "Pacific Blue Sea Star",
            scientificName   = "Linckia laevigata",
            taxonomicClass   = TaxonomicClass.Asteroidea,
            zoneIndex        = 0,
            habitat          = "Shallow sunlit coral reefs (0–60 m)",
            characteristics  = "Vivid cobalt-blue body with five long, cylindrical slender arms.",
            ecologicalRole   = "Scavenges and breaks down detritus and organic material on the seabed.",
            interestingFact  = "Can regenerate a complete new sea star from a single severed arm ('comet').",
            rdpReward        = 80,
            scanDifficulty   = 1,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Hard,
            minDepthFraction = 0.01f,
            maxDepthFraction = 0.30f,
            instanceCount    = 4,
            placeholderShape = PlaceholderShape.Sphere,
            placeholderColor = new Color(0.12f, 0.45f, 0.95f, 1f),
            placeholderScale = new Vector3(1.0f, 0.3f, 1.0f)
        }));

        // 5. Giant Clam
        createdAssets.Add(CreateOrUpdateSpecies(folder, "GiantClam", new SpeciesDataConfig
        {
            speciesId        = "giant_clam_001",
            commonName       = "Giant Clam",
            scientificName   = "Tridacna gigas",
            taxonomicClass   = TaxonomicClass.Bivalvia,
            zoneIndex        = 0,
            habitat          = "Shallow sunlit coral reef lagoons (0–35 m)",
            characteristics  = "Enormous fluted shell with iridescent blue, green, and gold mantle tissue.",
            ecologicalRole   = "Hosts photosynthetic zooxanthellae and actively filters large volumes of seawater.",
            interestingFact  = "The largest living bivalve mollusc, capable of weighing over 200 kilograms.",
            rdpReward        = 110,
            scanDifficulty   = 2,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Hard,
            minDepthFraction = 0.01f,
            maxDepthFraction = 0.18f,
            instanceCount    = 3,
            placeholderShape = PlaceholderShape.Cube,
            placeholderColor = new Color(0.10f, 0.85f, 0.75f, 1f),
            placeholderScale = new Vector3(2.0f, 1.2f, 1.5f)
        }));

        // 6. Bigfin Reef Squid
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BigfinReefSquid", new SpeciesDataConfig
        {
            speciesId        = "reef_squid_001",
            commonName       = "Bigfin Reef Squid",
            scientificName   = "Sepioteuthis lessoniana",
            taxonomicClass   = TaxonomicClass.Cephalopoda,
            zoneIndex        = 0,
            habitat          = "Shallow coastal reefs and seagrass beds (0–100 m)",
            characteristics  = "Translucent oval mantle with wide undulating fins running full body length.",
            ecologicalRole   = "Agile midwater predator consuming small fish and crustaceans.",
            interestingFact  = "Possesses chromatophores allowing instantaneous full-body color and pattern shifts.",
            rdpReward        = 95,
            scanDifficulty   = 2,
            isStationary     = false,
            isShy            = true,
            preferredBiome   = BiomeBand.Soft, // Seagrass
            minDepthFraction = 0.05f,
            maxDepthFraction = 0.45f,
            instanceCount    = 3,
            wanderRadius     = 18f,
            moveSpeed        = 3.2f,
            fleeSpeed        = 6.5f,
            placeholderShape = PlaceholderShape.Capsule,
            placeholderColor = new Color(0.85f, 0.95f, 1.0f, 1f),
            placeholderScale = new Vector3(0.8f, 1.8f, 0.8f)
        }));

        // 7. Blacktip Reef Shark
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BlacktipReefShark", new SpeciesDataConfig
        {
            speciesId        = "blacktip_shark_001",
            commonName       = "Blacktip Reef Shark",
            scientificName   = "Carcharhinus melanopterus",
            taxonomicClass   = TaxonomicClass.Chondrichthyes,
            zoneIndex        = 0,
            habitat          = "Shallow coral reef flats and drop-offs (0–75 m)",
            characteristics  = "Streamlined bronze-gray body with prominent black tips on all fins.",
            ecologicalRole   = "Apex reef predator maintaining equilibrium among fish and cephalopod populations.",
            interestingFact  = "Must remain in continuous forward motion to ventilate water over its gills.",
            rdpReward        = 120,
            scanDifficulty   = 3,
            isStationary     = false,
            isShy            = false,
            preferredBiome   = BiomeBand.OpenWater,
            minDepthFraction = 0.05f,
            maxDepthFraction = 0.38f,
            instanceCount    = 2,
            wanderRadius     = 30f,
            moveSpeed        = 4.5f,
            fleeSpeed        = 7.0f,
            placeholderShape = PlaceholderShape.Capsule,
            placeholderColor = new Color(0.40f, 0.45f, 0.50f, 1f),
            placeholderScale = new Vector3(1.2f, 3.5f, 1.2f)
        }));

        // 8. Blue-spotted Ribbontail Ray
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BlueSpottedRibbontailRay", new SpeciesDataConfig
        {
            speciesId        = "ribbontail_ray_001",
            commonName       = "Blue-spotted Ribbontail Ray",
            scientificName   = "Taeniura lymma",
            taxonomicClass   = TaxonomicClass.Chondrichthyes,
            zoneIndex        = 0,
            habitat          = "Shallow reef sand flats and lagoons (0–20 m)",
            characteristics  = "Distinctive oval disc patterned with electric-blue circular spots.",
            ecologicalRole   = "Benthic carnivore hunting molluscs, worms, and small crabs buried in sand.",
            interestingFact  = "Armed with two venomous serrated tail spines used exclusively in self-defense.",
            rdpReward        = 100,
            scanDifficulty   = 2,
            isStationary     = false,
            isShy            = true,
            preferredBiome   = BiomeBand.Soft, // Sand / Seagrass
            minDepthFraction = 0.02f,
            maxDepthFraction = 0.12f,
            instanceCount    = 3,
            wanderRadius     = 14f,
            moveSpeed        = 2.6f,
            fleeSpeed        = 5.5f,
            placeholderShape = PlaceholderShape.Cylinder,
            placeholderColor = new Color(0.20f, 0.65f, 0.90f, 1f),
            placeholderScale = new Vector3(1.8f, 0.2f, 1.8f)
        }));

        // 9. Purple Sea Urchin
        createdAssets.Add(CreateOrUpdateSpecies(folder, "PurpleSeaUrchin", new SpeciesDataConfig
        {
            speciesId        = "purple_urchin_001",
            commonName       = "Purple Sea Urchin",
            scientificName   = "Strongylocentrotus purpuratus",
            taxonomicClass   = TaxonomicClass.Echinoidea,
            zoneIndex        = 0,
            habitat          = "Rocky coastal areas and boulder fields (0–160 m)",
            characteristics  = "Globular test covered in dense, sharp vivid violet-purple spines.",
            ecologicalRole   = "Herbivorous grazer regulating macroalgae and kelp forest canopy density.",
            interestingFact  = "Key model organism in developmental genetics with a genome closely related to humans.",
            rdpReward        = 75,
            scanDifficulty   = 1,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Rock,
            minDepthFraction = 0.05f,
            maxDepthFraction = 0.75f,
            instanceCount    = 6,
            placeholderShape = PlaceholderShape.Sphere,
            placeholderColor = new Color(0.65f, 0.15f, 0.85f, 1f),
            placeholderScale = new Vector3(0.6f, 0.6f, 0.6f)
        }));

        // 10. Dotted Sea Hare
        createdAssets.Add(CreateOrUpdateSpecies(folder, "DottedSeaHare", new SpeciesDataConfig
        {
            speciesId        = "sea_hare_001",
            commonName       = "Dotted Sea Hare",
            scientificName   = "Aplysia punctata",
            taxonomicClass   = TaxonomicClass.Gastropoda,
            zoneIndex        = 0,
            habitat          = "Shallow seaweed beds and rocky shores (0–80 m)",
            characteristics  = "Mottled olive-brown soft body with prominent ear-like sensory rhinophores.",
            ecologicalRole   = "Feeds on brown and red macroalgae, cycling nutrients across the seabed.",
            interestingFact  = "Expels an intense violet defensive ink cloud derived from digested algae.",
            rdpReward        = 80,
            scanDifficulty   = 1,
            isStationary     = true,
            isShy            = false,
            preferredBiome   = BiomeBand.Soft,
            minDepthFraction = 0.02f,
            maxDepthFraction = 0.40f,
            instanceCount    = 4,
            placeholderShape = PlaceholderShape.Capsule,
            placeholderColor = new Color(0.45f, 0.55f, 0.20f, 1f),
            placeholderScale = new Vector3(0.6f, 1.0f, 0.6f)
        }));

        // 11. Bluebottle
        createdAssets.Add(CreateOrUpdateSpecies(folder, "Bluebottle", new SpeciesDataConfig
        {
            speciesId        = "bluebottle_001",
            commonName       = "Bluebottle",
            scientificName   = "Physalia utriculus",
            taxonomicClass   = TaxonomicClass.Hydrozoa,
            zoneIndex        = 0,
            habitat          = "Sunlit open ocean surface waters (0–3 m)",
            characteristics  = "Translucent blue gas-filled pneumatophore float trailing long stinging dactylozooids.",
            ecologicalRole   = "Surface predator snaring larval fish and pelagic zooplankton with nematocysts.",
            interestingFact  = "Not a single jellyfish, but a colonial organism composed of specialized zooids.",
            rdpReward        = 105,
            scanDifficulty   = 2,
            isStationary     = false,
            isShy            = false,
            preferredBiome   = BiomeBand.OpenWater,
            minDepthFraction = 0.00f,
            maxDepthFraction = 0.02f, // 0 to 4m
            instanceCount    = 3,
            wanderRadius     = 20f,
            moveSpeed        = 1.2f,
            fleeSpeed        = 2.0f,
            placeholderShape = PlaceholderShape.Sphere,
            placeholderColor = new Color(0.15f, 0.60f, 1.0f, 0.8f),
            placeholderScale = new Vector3(0.8f, 0.8f, 0.8f)
        }));

        // 12. Ornate Spiny Lobster
        createdAssets.Add(CreateOrUpdateSpecies(folder, "OrnateSpinyLobster", new SpeciesDataConfig
        {
            speciesId        = "spiny_lobster_001",
            commonName       = "Ornate Spiny Lobster",
            scientificName   = "Panulirus ornatus",
            taxonomicClass   = TaxonomicClass.Malacostraca,
            zoneIndex        = 0,
            habitat          = "Shallow coral crevices and shelf sediment (1–50 m)",
            characteristics  = "Intricately patterned carapace with long spiny antennae and banded legs.",
            ecologicalRole   = "Nocturnal scavenger and benthic predator consuming bivalves and sea urchins.",
            interestingFact  = "Undergoes massive annual breeding migrations marching single-file across the seafloor.",
            rdpReward        = 115,
            scanDifficulty   = 2,
            isStationary     = true,
            isShy            = true,
            preferredBiome   = BiomeBand.Rock,
            minDepthFraction = 0.02f,
            maxDepthFraction = 0.25f,
            instanceCount    = 3,
            placeholderShape = PlaceholderShape.Cube,
            placeholderColor = new Color(0.85f, 0.40f, 0.15f, 1f),
            placeholderScale = new Vector3(1.2f, 0.5f, 1.5f)
        }));

        // 13. Banded Sea Krait
        createdAssets.Add(CreateOrUpdateSpecies(folder, "BandedSeaKrait", new SpeciesDataConfig
        {
            speciesId        = "sea_krait_001",
            commonName       = "Banded Sea Krait",
            scientificName   = "Laticauda colubrina",
            taxonomicClass   = TaxonomicClass.Reptilia,
            zoneIndex        = 0,
            habitat          = "Shallow coral reefs and coastal shallows (0–10 m)",
            characteristics  = "Striking black-and-silver banded body with a bright yellow snout and paddle tail.",
            ecologicalRole   = "Specialized predator hunting moray eels in tight reef crevices.",
            interestingFact  = "Amphibious marine reptile that lays eggs on land and hunts cooperatively with trevally fish.",
            rdpReward        = 125,
            scanDifficulty   = 3,
            isStationary     = false,
            isShy            = true,
            preferredBiome   = BiomeBand.Hard,
            minDepthFraction = 0.005f,
            maxDepthFraction = 0.06f, // 1 to 12m
            instanceCount    = 2,
            wanderRadius     = 16f,
            moveSpeed        = 3.0f,
            fleeSpeed        = 6.0f,
            placeholderShape = PlaceholderShape.Capsule,
            placeholderColor = new Color(0.95f, 0.85f, 0.10f, 1f),
            placeholderScale = new Vector3(0.4f, 2.5f, 0.4f)
        }));

        // 14. Spotted Jellyfish
        createdAssets.Add(CreateOrUpdateSpecies(folder, "SpottedJellyfish", new SpeciesDataConfig
        {
            speciesId        = "spotted_jellyfish_001",
            commonName       = "Spotted Jellyfish",
            scientificName   = "Mastigias papua",
            taxonomicClass   = TaxonomicClass.Scyphozoa,
            zoneIndex        = 0,
            habitat          = "Sunlit shallow lagoons and marine lakes (1–20 m)",
            characteristics  = "Golden-brown hemispherical bell speckled with uniform white crystalline spots.",
            ecologicalRole   = "Harbors symbiotic algae that generate solar-powered nutrients in clear water.",
            interestingFact  = "Performs daily horizontal and vertical migrations following the angle of the sun.",
            rdpReward        = 90,
            scanDifficulty   = 1,
            isStationary     = false,
            isShy            = false,
            preferredBiome   = BiomeBand.OpenWater,
            minDepthFraction = 0.01f,
            maxDepthFraction = 0.12f,
            instanceCount    = 4,
            wanderRadius     = 12f,
            moveSpeed        = 1.5f,
            fleeSpeed        = 3.0f,
            placeholderShape = PlaceholderShape.Sphere,
            placeholderColor = new Color(0.90f, 0.70f, 0.35f, 0.9f),
            placeholderScale = new Vector3(1.2f, 1.2f, 1.2f)
        }));

        // Update SpeciesRegistry.asset
        UpdateRegistry(createdAssets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SpeciesDataGenerator] Successfully generated {createdAssets.Count} official scientific species for Sunlight Zone and updated SpeciesRegistry!");
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

        asset.speciesId        = config.speciesId;
        asset.commonName       = config.commonName;
        asset.scientificName   = config.scientificName;
        asset.taxonomicClass   = config.taxonomicClass;
        asset.zoneIndex        = config.zoneIndex;
        asset.habitat          = config.habitat;
        asset.characteristics  = config.characteristics;
        asset.ecologicalRole   = config.ecologicalRole;
        asset.interestingFact  = config.interestingFact;
        asset.rdpReward        = config.rdpReward;
        asset.scanDifficulty   = config.scanDifficulty;
        asset.isStationary     = config.isStationary;
        asset.isShy            = config.isShy;
        asset.preferredBiome   = config.preferredBiome;
        asset.minDepthFraction = config.minDepthFraction;
        asset.maxDepthFraction = config.maxDepthFraction;
        asset.instanceCount    = config.instanceCount;
        asset.wanderRadius     = config.wanderRadius;
        asset.moveSpeed        = config.moveSpeed;
        asset.fleeSpeed        = config.fleeSpeed;
        asset.placeholderShape = config.placeholderShape;
        asset.placeholderColor = config.placeholderColor;
        asset.placeholderScale = config.placeholderScale;

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

        // Collect existing species from other zones
        var existingList = new List<SpeciesData>();
        for (int i = 0; i < allSpeciesProp.arraySize; i++)
        {
            var item = allSpeciesProp.GetArrayElementAtIndex(i).objectReferenceValue as SpeciesData;
            if (item != null && item.zoneIndex != 0)
                existingList.Add(item);
        }

        // Add all newly created sunlight species
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
    }
}
#endif
