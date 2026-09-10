#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TwilightSpeciesData
{
    public static void Generate()
    {
        string folder = "Assets/Scripts/ScriptableObjects/Species";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects", "Species");

        var createdAssets = new List<SpeciesData>();

        // 1. Albatross Coffinfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "AlbatrossCoffinfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "albatross_coffinfish_001",
            commonName             = "Albatross Coffinfish",
            scientificName         = "Chaunax albatrossae",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 1,
            habitat                = "Deep seafloor (195–702 m)",
            depthRangeText         = "195–702 m",
            explorationHint        = "Resting on the soft sediment of the continental slope, walking along on leg-like fins.",
            characteristics        = "Round body, large mouth, and fins that help it move along the bottom.",
            ecologicalRole         = "Eats small fish and other sea animals.",
            interestingFact        = "It can use its fins almost like legs to walk along the seafloor.",
            rdpReward              = 75,
            scanDifficulty         = 2,
            isStationary           = true, // Walks slowly on seafloor, scannable benthic entity
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.63f,
            instanceCount          = 3,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.90f, 0.42f, 0.35f, 1f),
            placeholderScale       = new Vector3(0.8f, 0.6f, 0.9f),
            previewScaleMultiplier = 1.0f
        }));

        // 2. Sulu Sea Ribbonfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "SuluSeaRibbonfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "sulu_ribbonfish_001",
            commonName             = "Sulu Sea Ribbonfish",
            scientificName         = "Benthodesmus suluensis",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 1,
            habitat                = "Deep water (200–500 m)",
            depthRangeText         = "200–500 m",
            explorationHint        = "Look for undulating metallic silver bands gliding vertically through open mesopelagic water.",
            characteristics        = "Very long, thin, silver-colored body.",
            ecologicalRole         = "Hunts small fish and other small animals.",
            interestingFact        = "Its ribbon-like body makes it look more like a swimming ribbon than a normal fish.",
            rdpReward              = 85,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = true,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.38f,
            instanceCount          = 4,
            wanderRadius           = 22f,
            moveSpeed              = 2.8f,
            fleeSpeed              = 5.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.78f, 0.84f, 0.90f, 1f),
            placeholderScale       = new Vector3(0.3f, 2.5f, 0.3f),
            previewScaleMultiplier = 0.9f
        }));

        // 3. Threelight Hatchetfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "ThreelightHatchetfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "threelight_hatchetfish_001",
            commonName             = "Threelight Hatchetfish",
            scientificName         = "Polyipnus triphanos",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 1,
            habitat                = "Open water (322–966 m)",
            depthRangeText         = "322–966 m",
            explorationHint        = "Watch for small glimmering photophores glowing in the twilight midwater.",
            characteristics        = "Small, shiny fish with light-producing organs.",
            ecologicalRole         = "Eats tiny sea animals and is food for larger fish.",
            interestingFact        = "It can produce its own light in the darkness.",
            rdpReward              = 70,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.15f,
            maxDepthFraction       = 0.96f,
            instanceCount          = 6,
            wanderRadius           = 14f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.70f, 0.80f, 0.90f, 1f),
            placeholderScale       = new Vector3(0.5f, 0.7f, 0.2f),
            previewScaleMultiplier = 1.0f
        }));

        // 4. Ring Triangular Batfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "RingTriangularBatfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "ring_batfish_001",
            commonName             = "Ring Triangular Batfish",
            scientificName         = "Malthopsis annulifera",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 1,
            habitat                = "Deep seafloor (90–590 m)",
            depthRangeText         = "90–590 m",
            explorationHint        = "Perched motionlessly on hard gravel and rock ledges resembling an alien carving.",
            characteristics        = "Wide, flattened body with a large head.",
            ecologicalRole         = "Finds and eats small animals on the seafloor.",
            interestingFact        = "Its unusual shape can make it look like a tiny alien creature.",
            rdpReward              = 80,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.49f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.68f, 0.58f, 0.45f, 1f),
            placeholderScale       = new Vector3(0.9f, 0.4f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 5. Deep-water Coral (Anthozoa)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepWaterCoral", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "deepwater_coral_001",
            commonName             = "Deep-water Coral",
            scientificName         = "Dendrophyllia arbuscula",
            taxonomicClass         = TaxonomicClass.Anthozoa,
            zoneIndex              = 1,
            habitat                = "Rocky areas (30–259 m)",
            depthRangeText         = "30–259 m",
            explorationHint        = "Adhering to high vertical drop-off rocks near the upper twilight threshold.",
            characteristics        = "Branching coral with a hard skeleton.",
            ecologicalRole         = "Gives small sea animals places to live and hide.",
            interestingFact        = "Unlike many shallow corals, it can live where very little sunlight reaches.",
            rdpReward              = 65,
            scanDifficulty         = 1,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Rock,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.15f,
            instanceCount          = 5,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.95f, 0.48f, 0.18f, 1f),
            placeholderScale       = new Vector3(1.2f, 1.8f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 6. Volsellate Crown Star (Asteroidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "VolsellateCrownStar", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "crown_star_001",
            commonName             = "Volsellate Crown Star",
            scientificName         = "Coronaster volsellatus",
            taxonomicClass         = TaxonomicClass.Asteroidea,
            zoneIndex              = 1,
            habitat                = "Deep seafloor (82–630 m)",
            depthRangeText         = "82–630 m",
            explorationHint        = "Sprawled along stony seabed slopes with multiple spiny arms deployed.",
            characteristics        = "Star-shaped body with many arms and spines.",
            ecologicalRole         = "Eats small animals and leftover organic material.",
            interestingFact        = "It can regrow an arm after losing one.",
            rdpReward              = 70,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.54f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.92f, 0.28f, 0.28f, 1f),
            placeholderScale       = new Vector3(1.1f, 0.25f, 1.1f),
            previewScaleMultiplier = 1.0f
        }));

        // 7. Philippine Deep-water Clam (Bivalvia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PhilippineDeepWaterClam", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "philippine_deepclam_001",
            commonName             = "Philippine Deep-water Clam",
            scientificName         = "Acesta philippinensis",
            taxonomicClass         = TaxonomicClass.Bivalvia,
            zoneIndex              = 1,
            habitat                = "Deep slopes (300–700 m)",
            depthRangeText         = "300–700 m",
            explorationHint        = "Nestled inside fissures and ledges along deep current-swept slopes.",
            characteristics        = "Thin shell with fine lines on its surface.",
            ecologicalRole         = "Filters tiny food particles from the water.",
            interestingFact        = "Its species name “philippinensis” is connected to the Philippines.",
            rdpReward              = 75,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Rock,
            minDepthFraction       = 0.125f,
            maxDepthFraction       = 0.625f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.85f, 0.80f, 0.75f, 1f),
            placeholderScale       = new Vector3(0.9f, 0.5f, 0.9f),
            previewScaleMultiplier = 1.0f
        }));

        // 8. Purpleback Flying Squid (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PurplebackFlyingSquid", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "purpleback_squid_001",
            commonName             = "Purpleback Flying Squid",
            scientificName         = "Sthenoteuthis oualaniensis",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 1,
            habitat                = "Open ocean (up to 1,000 m)",
            depthRangeText         = "up to 1,000 m",
            explorationHint        = "Darting with explosive speed across the open water column.",
            characteristics        = "Large squid with wide fins and a dark purple back.",
            ecologicalRole         = "Eats fish, shrimp, and other squid.",
            interestingFact        = "Despite its name, it does not fly in the air—it can move rapidly through the water using jet propulsion.",
            rdpReward              = 90,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = true,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 3,
            wanderRadius           = 25f,
            moveSpeed              = 3.8f,
            fleeSpeed              = 7.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.48f, 0.18f, 0.60f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.8f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 9. Arm Squid (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "ArmSquid", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "arm_squid_001",
            commonName             = "Arm Squid",
            scientificName         = "Brachioteuthis picta",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 1,
            habitat                = "Open ocean (500–1,000 m)",
            depthRangeText         = "500–1,000 m",
            explorationHint        = "Drifting in deep, dim water with arms outstretched to snag prey.",
            characteristics        = "Small squid with a soft body and long arms.",
            ecologicalRole         = "Eats tiny sea animals and becomes food for larger predators.",
            interestingFact        = "Like other squid, it can shoot water from its body to move quickly.",
            rdpReward              = 80,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.375f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 4,
            wanderRadius           = 18f,
            moveSpeed              = 3.0f,
            fleeSpeed              = 6.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.72f, 0.35f, 0.40f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.4f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 10. Silver Chimaera (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "SilverChimaera", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "silver_chimaera_001",
            commonName             = "Silver Chimaera",
            scientificName         = "Chimaera phantasma",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 1,
            habitat                = "Deep seafloor (90–540 m)",
            depthRangeText         = "90–540 m",
            explorationHint        = "Ghostly iridescent form gliding silently above muddy seabed channels.",
            characteristics        = "Silvery body, large eyes, and a long tail.",
            ecologicalRole         = "Eats worms, shrimp, and other bottom animals.",
            interestingFact        = "Chimaeras are sometimes called “ghost sharks,” although they are not true sharks.",
            rdpReward              = 95,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.43f,
            instanceCount          = 2,
            wanderRadius           = 22f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 5.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.78f, 0.82f, 0.88f, 1f),
            placeholderScale       = new Vector3(0.7f, 2.0f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 11. Deep-water Stingray (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepWaterStingray", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "deepwater_stingray_001",
            commonName             = "Deep-water Stingray",
            scientificName         = "Plesiobatis daviesi",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 1,
            habitat                = "Deep slopes (44–780 m)",
            depthRangeText         = "44–780 m",
            explorationHint        = "Skimming the sandy seabed on long continental inclines.",
            characteristics        = "Wide, rounded body with a pointed nose and long tail.",
            ecologicalRole         = "Eats fish, squid, shrimp, and other bottom animals.",
            interestingFact        = "It can grow to around 2.7 m long.",
            rdpReward              = 90,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.73f,
            instanceCount          = 2,
            wanderRadius           = 20f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.8f,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.42f, 0.38f, 0.32f, 1f),
            placeholderScale       = new Vector3(2.0f, 0.3f, 2.0f),
            previewScaleMultiplier = 0.9f
        }));

        // 12. Shorttail Lanternshark (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "ShorttailLanternshark", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "shorttail_lanternshark_001",
            commonName             = "Shorttail Lanternshark",
            scientificName         = "Etmopterus brachyurus",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 1,
            habitat                = "Deep water (100–696 m)",
            depthRangeText         = "100–696 m",
            explorationHint        = "Patrolling dark intermediate water columns, glowing faintly from ventral photophores.",
            characteristics        = "Small dark shark with tiny light-producing organs.",
            ecologicalRole         = "Hunts small fish and shrimp.",
            interestingFact        = "It can glow in the dark using special organs on its body.",
            rdpReward              = 85,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.62f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 3.2f,
            fleeSpeed              = 6.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.22f, 0.25f, 0.30f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.8f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 13. Deep-water Sponge (Demospongiae)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepWaterSponge", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "deepwater_sponge_001",
            commonName             = "Deep-water Sponge",
            scientificName         = "Corallistes masoni",
            taxonomicClass         = TaxonomicClass.Demospongiae,
            zoneIndex              = 1,
            habitat                = "Rocky seafloor (200–1,000 m)",
            depthRangeText         = "200–1,000 m",
            explorationHint        = "Anchored to exposed rock promontories filtering mineral-rich deep currents.",
            characteristics        = "Hard, cup-shaped body with a strong skeleton.",
            ecologicalRole         = "Filters food from the water and provides shelter.",
            interestingFact        = "Its skeleton contains tiny structures made of silica.",
            rdpReward              = 65,
            scanDifficulty         = 1,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Rock,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.82f, 0.78f, 0.68f, 1f),
            placeholderScale       = new Vector3(1.2f, 1.2f, 1.2f),
            previewScaleMultiplier = 1.0f
        }));

        // 14. Heart Urchin (Echinoidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "HeartUrchin", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "heart_urchin_001",
            commonName             = "Heart Urchin",
            scientificName         = "Echinocardium cordatum",
            taxonomicClass         = TaxonomicClass.Echinoidea,
            zoneIndex              = 1,
            habitat                = "Sandy or muddy seafloor (0–230 m)",
            depthRangeText         = "0–230 m",
            explorationHint        = "Semi-buried in fine sand beds near the zone entrance.",
            characteristics        = "Heart-shaped body covered with tiny spines.",
            ecologicalRole         = "Mixes the sand and mud while searching for food.",
            interestingFact        = "Its heart shape is why it is commonly called a heart urchin.",
            rdpReward              = 60,
            scanDifficulty         = 1,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.15f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.85f, 0.75f, 0.55f, 1f),
            placeholderScale       = new Vector3(0.8f, 0.6f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 15. Transparent Sea Snail (Gastropoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "TransparentSeaSnail", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "transparent_snail_001",
            commonName             = "Transparent Sea Snail",
            scientificName         = "Carinaria cristata",
            taxonomicClass         = TaxonomicClass.Gastropoda,
            zoneIndex              = 1,
            habitat                = "Open ocean (up to about 500 m)",
            depthRangeText         = "up to about 500 m",
            explorationHint        = "Drifting gracefully through open water, nearly invisible like living glass.",
            characteristics        = "Mostly transparent body with a small shell.",
            ecologicalRole         = "Hunts tiny animals floating in the water.",
            interestingFact        = "Its clear body makes it difficult for predators to see.",
            rdpReward              = 85,
            scanDifficulty         = 3,
            isStationary           = false, // Pelagic heteropod swimmer
            isShy                  = true,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.38f,
            instanceCount          = 3,
            wanderRadius           = 15f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.70f, 0.85f, 0.95f, 0.7f),
            placeholderScale       = new Vector3(0.6f, 1.2f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 16. Mino Nylon Shrimp (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "MinoNylonShrimp", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "mino_nylon_shrimp_001",
            commonName             = "Mino Nylon Shrimp",
            scientificName         = "Heterocarpus sibogae",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 1,
            habitat                = "Deep seafloor (190–950 m)",
            depthRangeText         = "190–950 m",
            explorationHint        = "Scuttling with vibrant crimson antennae over deep sediment terraces.",
            characteristics        = "Small, slender shrimp with a reddish body.",
            ecologicalRole         = "Eats small animals and organic material.",
            interestingFact        = "Its long antennae help it find food in dark water.",
            rdpReward              = 70,
            scanDifficulty         = 2,
            isStationary           = false, // Swims/scuttles along the seafloor
            isShy                  = true,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.94f,
            instanceCount          = 5,
            wanderRadius           = 12f,
            moveSpeed              = 1.6f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.95f, 0.30f, 0.22f, 1f),
            placeholderScale       = new Vector3(0.4f, 0.8f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 17. Coronate Jellyfish (Scyphozoa)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "CoronateJellyfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "coronate_jellyfish_001",
            commonName             = "Coronate Jellyfish",
            scientificName         = "Nausithoe punctata",
            taxonomicClass         = TaxonomicClass.Scyphozoa,
            zoneIndex              = 1,
            habitat                = "Open water (up to 1,000 m)",
            depthRangeText         = "up to 1,000 m",
            explorationHint        = "Pulsing slowly through dark open currents with its distinctive crown-like groove.",
            characteristics        = "Small jellyfish with a rounded, ridged body.",
            ecologicalRole         = "Eats tiny animals drifting in the water.",
            interestingFact        = "Some members of its group have a crown-like shape around the bell.",
            rdpReward              = 70,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 5,
            wanderRadius           = 16f,
            moveSpeed              = 1.2f,
            fleeSpeed              = 3.0f,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.65f, 0.25f, 0.70f, 0.85f),
            placeholderScale       = new Vector3(0.8f, 0.8f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        SpeciesDataGenerator.UpdateRegistryForZone(1, createdAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TwilightSpeciesData] Successfully generated all {createdAssets.Count} official scientific species for Twilight Zone (Zone 1)!");
    }
}
#endif
