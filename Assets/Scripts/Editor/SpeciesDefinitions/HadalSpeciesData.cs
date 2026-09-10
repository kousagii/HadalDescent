#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HadalSpeciesData
{
    public static void Generate()
    {
        string folder = "Assets/Scripts/ScriptableObjects/Species";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects", "Species");

        var createdAssets = new List<SpeciesData>();

        // 1. Mariana Snailfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "MarianaSnailfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "mariana_snailfish_001",
            commonName             = "Mariana Snailfish",
            scientificName         = "Pseudoliparis swirei",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,198–8,178 m)",
            depthRangeText         = "6,198–8,178 m",
            explorationHint        = "Translucent pinkish tadpole shape fluttering broad pectoral fins in extreme trench depressions.",
            characteristics        = "Small, soft body, rounded head, and broad fins.",
            ecologicalRole         = "Eats small crustaceans and other tiny animals.",
            interestingFact        = "Females produce unusually large eggs for such a small fish.",
            rdpReward              = 150,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.04f,
            maxDepthFraction       = 0.44f,
            instanceCount          = 4,
            wanderRadius           = 16f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.95f, 0.75f, 0.80f, 0.85f), // translucent pink
            placeholderScale       = new Vector3(0.5f, 1.2f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 2. Northwest Pacific Snailfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "NorthwestPacificSnailfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "nw_pacific_snailfish_001",
            commonName             = "Northwest Pacific Snailfish",
            scientificName         = "Pseudoliparis amblystomopsis",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,156–7,587 m)",
            depthRangeText         = "6,156–7,587 m",
            explorationHint        = "Pale gelatinous swimmer darting above trench floor amphipod clusters.",
            characteristics        = "Soft body, rounded head, and broad pectoral fins.",
            ecologicalRole         = "Feeds on small animals near the trench floor.",
            interestingFact        = "A soft, flexible body helps the fish survive extreme water pressure.",
            rdpReward              = 145,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.03f,
            maxDepthFraction       = 0.32f,
            instanceCount          = 3,
            wanderRadius           = 16f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.92f, 0.82f, 0.85f, 0.85f),
            placeholderScale       = new Vector3(0.5f, 1.2f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 3. Belyaev's Snailfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "BelyaevsSnailfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "belyaev_snailfish_001",
            commonName             = "Belyaev's Snailfish",
            scientificName         = "Pseudoliparis belyaevi",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,380–8,336 m)",
            depthRangeText         = "6,380–8,336 m",
            explorationHint        = "Soft scaleless fish cruising slowly along fault scarps.",
            characteristics        = "Small, soft body with broad fins and a rounded head.",
            ecologicalRole         = "Eats small animals near the trench floor.",
            interestingFact        = "The body stays flexible instead of being heavily protected by hard bones.",
            rdpReward              = 150,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.08f,
            maxDepthFraction       = 0.47f,
            instanceCount          = 3,
            wanderRadius           = 16f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.2f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.90f, 0.78f, 0.82f, 0.85f),
            placeholderScale       = new Vector3(0.5f, 1.3f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 4. Galathea Cusk-eel (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "GalatheaCuskeel", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "galathea_cuskeel_001",
            commonName             = "Galathea Cusk-eel",
            scientificName         = "Abyssobrotula galatheae",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 4,
            habitat                = "Deep ocean (3,110–7,965 m)",
            depthRangeText         = "3,110–7,965 m",
            explorationHint        = "Ghostly pale fish with swollen bulbous snout and vestigial eyes.",
            characteristics        = "Small body, short head, swollen snout, and tiny eyes.",
            ecologicalRole         = "Hunts small animals in deep water.",
            interestingFact        = "The tiny eyes reflect how little useful light reaches the deep water.",
            rdpReward              = 160,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.39f,
            instanceCount          = 2,
            wanderRadius           = 18f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 3.8f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.85f, 0.80f, 0.78f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.8f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 5. Palau Trench Sea Lily (Crinoidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PalauTrenchSeaLily", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "palau_sea_lily_001",
            commonName             = "Palau Trench Sea Lily",
            scientificName         = "Bathycrinus kirilli",
            taxonomicClass         = TaxonomicClass.Crinoidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,970–9,735 m)",
            depthRangeText         = "7,970–9,735 m",
            explorationHint        = "Anchored to rocky slabs on ultra-deep trench walls, feather arms open like a flower.",
            characteristics        = "Long stalk with feathery arms for collecting food.",
            ecologicalRole         = "Filters tiny food particles from the water.",
            interestingFact        = "The long stalk raises the feeding arms above the seafloor.",
            rdpReward              = 165,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Rock,
            minDepthFraction       = 0.39f,
            maxDepthFraction       = 0.75f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.92f, 0.88f, 0.78f, 1f),
            placeholderScale       = new Vector3(0.5f, 2.5f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 6. Long-stemmed Sea Lily (Crinoidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LongstemmedSeaLily", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "longstemmed_sea_lily_001",
            commonName             = "Long-stemmed Sea Lily",
            scientificName         = "Bathycrinus longicolumnalis",
            taxonomicClass         = TaxonomicClass.Crinoidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,445–7,170 m)",
            depthRangeText         = "6,445–7,170 m",
            explorationHint        = "Erect stalked crinoid waving segmented column in hadal current.",
            characteristics        = "Long stalk and feathery feeding arms.",
            ecologicalRole         = "Filters tiny particles from the surrounding water.",
            interestingFact        = "The stalk is made of many small sections that work together like a long underwater stem.",
            rdpReward              = 155,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Rock,
            minDepthFraction       = 0.09f,
            maxDepthFraction       = 0.23f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.88f, 0.85f, 0.75f, 1f),
            placeholderScale       = new Vector3(0.5f, 2.2f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 7. Palau Trench Starfish (Asteroidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PalauTrenchStarfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "palau_starfish_001",
            commonName             = "Palau Trench Starfish",
            scientificName         = "Porcellanaster ivanovi",
            taxonomicClass         = TaxonomicClass.Asteroidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,000–8,720 m)",
            depthRangeText         = "7,000–8,720 m",
            explorationHint        = "Flat porcelain-white sea star pressing evenly onto hadal mud.",
            characteristics        = "Flat body with short arms around the center.",
            ecologicalRole         = "Processes organic material in deep sediment.",
            interestingFact        = "The flat body helps it move across soft mud without sinking deeply.",
            rdpReward              = 150,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.20f,
            maxDepthFraction       = 0.54f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.92f, 0.90f, 0.85f, 1f),
            placeholderScale       = new Vector3(1.0f, 0.15f, 1.0f),
            previewScaleMultiplier = 1.0f
        }));

        // 8. Hadal Heart Urchin (Echinoidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "HadalHeartUrchin", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "hadal_urchin_001",
            commonName             = "Hadal Heart Urchin",
            scientificName         = "Pourtalesia heptneri",
            taxonomicClass         = TaxonomicClass.Echinoidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,335–7,340 m)",
            depthRangeText         = "7,335–7,340 m",
            explorationHint        = "Elongated bottle-shaped urchin test plowing through fine trench mud.",
            characteristics        = "Small, narrow body covered with fine spines.",
            ecologicalRole         = "Feeds on organic material in deep sediment.",
            interestingFact        = "The unusual narrow shape makes it look very different from common round sea urchins.",
            rdpReward              = 160,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.25f,
            maxDepthFraction       = 0.30f,
            instanceCount          = 3,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.72f, 0.68f, 0.60f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.0f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 9. Philippine Trench Sea Cucumber (Holothuroidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PhilippineTrenchSeaCucumber", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "scotoplanes_galatheae_001",
            commonName             = "Philippine Trench Sea Cucumber",
            scientificName         = "Scotoplanes galatheae",
            taxonomicClass         = TaxonomicClass.Holothuroidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (9,790 m record)",
            depthRangeText         = "9,790 m record",
            explorationHint        = "Chubby translucent sea pig with walking tube feet navigating trench valleys.",
            characteristics        = "Soft gray body with tube feet and feeding tentacles.",
            ecologicalRole         = "Collects food particles from the trench floor.",
            interestingFact        = "Tube feet help it crawl across the soft trench floor.",
            rdpReward              = 175,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.60f,
            maxDepthFraction       = 0.80f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.85f, 0.80f, 0.85f, 0.85f),
            placeholderScale       = new Vector3(0.9f, 0.6f, 1.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 10. Long-bodied Trench Sea Cucumber (Holothuroidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LongbodiedTrenchSeaCucumber", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "myriotrochus_001",
            commonName             = "Long-bodied Trench Sea Cucumber",
            scientificName         = "Myriotrochus longissimus",
            taxonomicClass         = TaxonomicClass.Holothuroidea,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,475–7,370 m)",
            depthRangeText         = "6,475–7,370 m",
            explorationHint        = "Worm-like translucent cylinder slowly ingesting organic sediment.",
            characteristics        = "Very long, narrow body with small tube feet.",
            ecologicalRole         = "Feeds on organic material in deep sediment.",
            interestingFact        = "The long body can bend easily while moving through soft sediment.",
            rdpReward              = 145,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.09f,
            maxDepthFraction       = 0.27f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.80f, 0.75f, 0.78f, 0.85f),
            placeholderScale       = new Vector3(0.4f, 2.2f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 11. Deep Trench Clam (Bivalvia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepTrenchClam", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "vesicomya_sergeevi_001",
            commonName             = "Deep Trench Clam",
            scientificName         = "Vesicomya sergeevi",
            taxonomicClass         = TaxonomicClass.Bivalvia,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,600–9,530 m)",
            depthRangeText         = "7,600–9,530 m",
            explorationHint        = "Thick rounded valves partially submerged in reducing sediment channels.",
            characteristics        = "Small shell with two rounded halves.",
            ecologicalRole         = "Gets nutrients with help from bacteria living in its tissues.",
            interestingFact        = "Helpful bacteria provide nutrients when ordinary food is scarce.",
            rdpReward              = 160,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.32f,
            maxDepthFraction       = 0.71f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.85f, 0.82f, 0.75f, 1f),
            placeholderScale       = new Vector3(0.8f, 0.4f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 12. Ultraabyssal Clam (Bivalvia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "UltraabyssalClam", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "parayoldiella_ultraabyssalis_001",
            commonName             = "Ultraabyssal Clam",
            scientificName         = "Parayoldiella ultraabyssalis",
            taxonomicClass         = TaxonomicClass.Bivalvia,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (about 8,000–9,583 m)",
            depthRangeText         = "about 8,000–9,583 m",
            explorationHint        = "Delicate shell valves embedded in the finest hadal mud.",
            characteristics        = "Very small, thin shell with two matching halves.",
            ecologicalRole         = "Processes tiny food particles in deep sediment.",
            interestingFact        = "The small shell fits well in the fine mud found deep inside trenches.",
            rdpReward              = 170,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.40f,
            maxDepthFraction       = 0.72f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.80f, 0.80f, 0.76f, 1f),
            placeholderScale       = new Vector3(0.7f, 0.35f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 13. Median Hadal Clam (Bivalvia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "MedianHadalClam", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "parayoldiella_mediana_001",
            commonName             = "Median Hadal Clam",
            scientificName         = "Parayoldiella mediana",
            taxonomicClass         = TaxonomicClass.Bivalvia,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (up to about 8,740 m)",
            depthRangeText         = "up to about 8,740 m",
            explorationHint        = "Smooth paired valves resting in hadal sediment terraces.",
            characteristics        = "Small, smooth shell with two hinged halves.",
            ecologicalRole         = "Feeds on tiny particles and organic material.",
            interestingFact        = "The two shell halves can close tightly around the soft body.",
            rdpReward              = 155,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.10f,
            maxDepthFraction       = 0.55f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.82f, 0.82f, 0.78f, 1f),
            placeholderScale       = new Vector3(0.7f, 0.35f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 14. Deep Sea Amphipod (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaAmphipod", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "bathyschraderia_001",
            commonName             = "Deep Sea Amphipod",
            scientificName         = "Bathyschraderia fragilis",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,000 to 9,900 meters)",
            depthRangeText         = "7,000 to 9,900 meters",
            explorationHint        = "Delicate shrimp-like scavenger crawling across organic falls.",
            characteristics        = "Small shrimp-like body with many legs.",
            ecologicalRole         = "Scavenges organic material on the trench floor.",
            interestingFact        = "The body looks delicate despite living under enormous pressure.",
            rdpReward              = 150,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.20f,
            maxDepthFraction       = 0.78f,
            instanceCount          = 6,
            wanderRadius           = 14f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.85f, 0.82f, 0.80f, 1f),
            placeholderScale       = new Vector3(0.35f, 0.8f, 0.35f),
            previewScaleMultiplier = 1.0f
        }));

        // 15. Giant Hadal Amphipod (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "GiantHadalAmphipod", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "hirondellea_gigas_001",
            commonName             = "Giant Hadal Amphipod",
            scientificName         = "Hirondellea gigas",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (10,897–10,929 m records)",
            depthRangeText         = "10,897–10,929 m records",
            explorationHint        = "Massive swarms swarming Challenger Deep carrion falls in the deepest trench on Earth.",
            characteristics        = "Shrimp-like body with many legs and a hard outer covering.",
            ecologicalRole         = "Scavenges dead animals and plant material.",
            interestingFact        = "Special digestive enzymes help break down tough plant material such as wood.",
            rdpReward              = 200,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.80f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 6,
            wanderRadius           = 18f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 5.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.95f, 0.85f, 0.75f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.4f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 16. Small-eyed Hadal Amphipod (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "SmallEyedHadalAmphipod", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "paralicella_microps_001",
            commonName             = "Small-eyed Hadal Amphipod",
            scientificName         = "Paralicella microps",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trenches (6,580–8,480 m)",
            depthRangeText         = "6,580–8,480 m",
            explorationHint        = "Long sensory antennae feeling ahead through pitch-black hadal canyons.",
            characteristics        = "Small, flattened body with many legs and long antennae.",
            ecologicalRole         = "Scavenges food that reaches the trench floor.",
            interestingFact        = "Very small eyes are enough because almost no sunlight reaches these depths.",
            rdpReward              = 145,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.12f,
            maxDepthFraction       = 0.50f,
            instanceCount          = 5,
            wanderRadius           = 14f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.88f, 0.80f, 0.72f, 1f),
            placeholderScale       = new Vector3(0.35f, 0.9f, 0.35f),
            previewScaleMultiplier = 1.0f
        }));

        // 17. Abyssal Trench Amphipod (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "AbyssalTrenchAmphipod", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "princaxelia_abyssalis_001",
            commonName             = "Abyssal Trench Amphipod",
            scientificName         = "Princaxelia abyssalis",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep Western Pacific trenches (6,380–9,530 m)",
            depthRangeText         = "6,380–9,530 m",
            explorationHint        = "Active predatory amphipod hunting smaller crustaceans along trench slopes.",
            characteristics        = "Slender shrimp-like body with many legs and antennae.",
            ecologicalRole         = "Hunts small crustaceans and scavenges food.",
            interestingFact        = "Long antennae help detect nearby food in complete darkness.",
            rdpReward              = 155,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.08f,
            maxDepthFraction       = 0.71f,
            instanceCount          = 4,
            wanderRadius           = 16f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.90f, 0.75f, 0.70f, 1f),
            placeholderScale       = new Vector3(0.4f, 1.1f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 18. Jamieson's Amphipod (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "JamiesonsAmphipod", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "princaxelia_jamiesoni_001",
            commonName             = "Jamieson's Amphipod",
            scientificName         = "Princaxelia jamiesoni",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trenches (7,703–9,316 m)",
            depthRangeText         = "7,703–9,316 m",
            explorationHint        = "Rapid darting movements propelled by elongate legs over hadal mud.",
            characteristics        = "Small body with long legs and antennae.",
            ecologicalRole         = "Hunts small crustaceans and other prey.",
            interestingFact        = "Strong legs help it move quickly while searching for food.",
            rdpReward              = 165,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.34f,
            maxDepthFraction       = 0.66f,
            instanceCount          = 4,
            wanderRadius           = 16f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 4.8f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.92f, 0.78f, 0.72f, 1f),
            placeholderScale       = new Vector3(0.4f, 1.1f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 19. Large Princaxelia (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LargePrincaxelia", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "princaxelia_magna_001",
            commonName             = "Large Princaxelia",
            scientificName         = "Princaxelia magna",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trenches (7,190–8,942 m)",
            depthRangeText         = "7,190–8,942 m",
            explorationHint        = "Robust predatory crustacean roaming deep fault lines.",
            characteristics        = "Compact body with many legs and long antennae.",
            ecologicalRole         = "Feeds on small animals and dead material.",
            interestingFact        = "A larger body gives more room for strong muscles and feeding parts.",
            rdpReward              = 160,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.24f,
            maxDepthFraction       = 0.59f,
            instanceCount          = 4,
            wanderRadius           = 18f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.88f, 0.72f, 0.68f, 1f),
            placeholderScale       = new Vector3(0.45f, 1.3f, 0.45f),
            previewScaleMultiplier = 1.0f
        }));

        // 20. Ultra-abyssal Lepechinella (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "UltraAbyssalLepechinella", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "lepechinella_ultra_001",
            commonName             = "Ultra-abyssal Lepechinella",
            scientificName         = "Lepechinella ultraabyssalis",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (6,475–8,015 m)",
            depthRangeText         = "6,475–8,015 m",
            explorationHint        = "Armored crustacean scuttling over rocky trench rubble.",
            characteristics        = "Small, flattened body with protective plates.",
            ecologicalRole         = "Feeds on small animals and organic material.",
            interestingFact        = "Hard body plates provide extra protection while moving over the seafloor.",
            rdpReward              = 150,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.10f,
            maxDepthFraction       = 0.40f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.82f, 0.75f, 0.65f, 1f),
            placeholderScale       = new Vector3(0.5f, 0.3f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 21. Glass-like Lepechinella (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "GlassLikeLepechinella", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "lepechinella_vitrea_001",
            commonName             = "Glass-like Lepechinella",
            scientificName         = "Lepechinella vitrea",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 4,
            habitat                = "Deep ocean trench (7,190–7,250 m)",
            depthRangeText         = "7,190–7,250 m",
            explorationHint        = "Delicate vitreous crustacean crawling near hadal fault scarps.",
            characteristics        = "Small, delicate body with many legs and long antennae.",
            ecologicalRole         = "Scavenges small food particles.",
            interestingFact        = "The delicate-looking body can still survive extreme water pressure.",
            rdpReward              = 160,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.24f,
            maxDepthFraction       = 0.27f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.85f, 0.90f, 0.95f, 0.8f),
            placeholderScale       = new Vector3(0.45f, 0.25f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        SpeciesDataGenerator.UpdateRegistryForZone(4, createdAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HadalSpeciesData] Successfully generated all {createdAssets.Count} official scientific species for Hadal Zone (Zone 4)!");
    }
}
#endif
