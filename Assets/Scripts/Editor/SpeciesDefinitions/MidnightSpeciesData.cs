#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MidnightSpeciesData
{
    public static void Generate()
    {
        string folder = "Assets/Scripts/ScriptableObjects/Species";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects", "Species");

        var createdAssets = new List<SpeciesData>();

        // 1. Indo-Pacific Snaggletooth (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "IndoPacificSnaggletooth", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "snaggletooth_001",
            commonName             = "Indo-Pacific Snaggletooth",
            scientificName         = "Astronesthes indopacificus",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Deep ocean, 100–3,178 m",
            depthRangeText         = "100–3,178 m",
            explorationHint        = "Stalking in absolute darkness, flashing chin barbels to lure small fish.",
            characteristics        = "Small, dark fish with large teeth and glowing organs.",
            ecologicalRole         = "Hunts small fish and shrimp.",
            interestingFact        = "It can produce light from its body in the darkness.",
            rdpReward              = 95,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.73f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 2.8f,
            fleeSpeed              = 5.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.18f, 0.15f, 0.22f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.4f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 2. Longnose Lancetfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LongnoseLancetfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "lancetfish_001",
            commonName             = "Longnose Lancetfish",
            scientificName         = "Alepisaurus ferox",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Open ocean, 0–1,830 m",
            depthRangeText         = "0–1,830 m",
            explorationHint        = "Watch for a tall sail-like dorsal fin cutting through upper bathypelagic waters.",
            characteristics        = "Long, silver body with a very large mouth and tall fin.",
            ecologicalRole         = "Hunts fish, squid, and shrimp.",
            interestingFact        = "It sometimes eats other lancetfish.",
            rdpReward              = 100,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.28f,
            instanceCount          = 2,
            wanderRadius           = 25f,
            moveSpeed              = 3.2f,
            fleeSpeed              = 6.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.72f, 0.78f, 0.85f, 1f),
            placeholderScale       = new Vector3(0.6f, 2.8f, 0.6f),
            previewScaleMultiplier = 0.9f
        }));

        // 3. Tribute Spiderfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "TributeSpiderfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "spiderfish_001",
            commonName             = "Tribute Spiderfish",
            scientificName         = "Bathypterois guentheri",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Deep seafloor, 800–1,500 m",
            depthRangeText         = "800–1,500 m",
            explorationHint        = "Standing upright like a tripod on elongate fin rays facing into gentle seabed currents.",
            characteristics        = "Has very long fins that support its body above the seafloor.",
            ecologicalRole         = "Waits for small animals drifting past.",
            interestingFact        = "Its fins make it look like it is standing on stilts.",
            rdpReward              = 110,
            scanDifficulty         = 3,
            isStationary           = true, // Tripod bottom-stander
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.17f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.60f, 0.55f, 0.50f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.6f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 4. Rudis Rattail (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "RudisRattail", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "rudis_rattail_001",
            commonName             = "Rudis Rattail",
            scientificName         = "Coryphaenoides rudis",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Deep ocean and seafloor, 600–2,380 m",
            depthRangeText         = "600–2,380 m",
            explorationHint        = "Hovering head-down above the mud sniffing for falling organic detritus.",
            characteristics        = "Has a large head and a long, thin tail.",
            ecologicalRole         = "Eats small animals and food that falls to the seafloor.",
            interestingFact        = "It has been recorded from deep waters near the Philippines.",
            rdpReward              = 90,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.46f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.40f, 0.38f, 0.35f, 1f),
            placeholderScale       = new Vector3(0.7f, 1.8f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 5. Doublespine Seadevil (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DoublespineSeadevil", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "doublespine_seadevil_001",
            commonName             = "Doublespine Seadevil",
            scientificName         = "Diceratias bispinosus",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Deep ocean, 533–2,306 m",
            depthRangeText         = "533–2,306 m",
            explorationHint        = "Drifting silently with a glowing bioluminescent esca hovering above needle teeth.",
            characteristics        = "Small, dark anglerfish with a glowing lure.",
            ecologicalRole         = "Uses its lure to attract and catch prey.",
            interestingFact        = "It has been recorded in Philippine and Indo-Pacific waters.",
            rdpReward              = 115,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.44f,
            instanceCount          = 3,
            wanderRadius           = 15f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.12f, 0.10f, 0.15f, 1f),
            placeholderScale       = new Vector3(0.9f, 0.8f, 0.9f),
            previewScaleMultiplier = 1.0f
        }));

        // 6. Diaphanous Hatchetfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DiaphanousHatchetfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "diaphanous_hatchetfish_001",
            commonName             = "Diaphanous Hatchetfish",
            scientificName         = "Sternoptyx diaphana",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 2,
            habitat                = "Deep ocean, 400–3,676 m",
            depthRangeText         = "400–3,676 m",
            explorationHint        = "Schooling in deep midwater, counterilluminating with belly photophores.",
            characteristics        = "Small, thin fish with a shiny, hatchet-shaped body.",
            ecologicalRole         = "Eats tiny drifting animals.",
            interestingFact        = "Its thin body helps it blend into the dark water.",
            rdpReward              = 85,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.89f,
            instanceCount          = 5,
            wanderRadius           = 16f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.68f, 0.74f, 0.80f, 1f),
            placeholderScale       = new Vector3(0.4f, 0.6f, 0.15f),
            previewScaleMultiplier = 1.0f
        }));

        // 7. Deep-water Clam (Bivalvia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepWaterClam", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "elliptiolucina_clam_001",
            commonName             = "Deep-water Clam",
            scientificName         = "Elliptiolucina labeyriei",
            taxonomicClass         = TaxonomicClass.Bivalvia,
            zoneIndex              = 2,
            habitat                = "Deep seafloor, up to 2,570 m",
            depthRangeText         = "up to 2,570 m",
            explorationHint        = "Buried partially in sulfide-rich sediments near reducing bathyal mounds.",
            characteristics        = "Small clam that lives in deep seafloor sediments.",
            ecologicalRole         = "Gets nutrients with help from bacteria living inside it.",
            interestingFact        = "It was recorded at 2,570 m near the Philippines.",
            rdpReward              = 90,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.52f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.82f, 0.80f, 0.74f, 1f),
            placeholderScale       = new Vector3(0.8f, 0.4f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 8. Vampire Squid (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "VampireSquid", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "vampire_squid_001",
            commonName             = "Vampire Squid",
            scientificName         = "Vampyroteuthis infernalis",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 2,
            habitat                = "Deep ocean, 300–3,000 m",
            depthRangeText         = "300–3,000 m",
            explorationHint        = "Inverted dark cloak drifting in oxygen minimum layers, deploying delicate bioluminescent filaments.",
            characteristics        = "Dark animal with webbed arms and glowing organs.",
            ecologicalRole         = "Eats drifting organic material and tiny animals.",
            interestingFact        = "It can release glowing particles when threatened.",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = true,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.67f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 5.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.35f, 0.12f, 0.15f, 1f), // deep reddish black
            placeholderScale       = new Vector3(0.7f, 1.6f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 9. Diamondback Squid (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DiamondbackSquid", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "diamondback_squid_001",
            commonName             = "Diamondback Squid",
            scientificName         = "Thysanoteuthis rhombus",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 2,
            habitat                = "Open ocean, surface–about 2,200 m",
            depthRangeText         = "surface–about 2,200 m",
            explorationHint        = "Cruising in mated pairs through deep oceanic waters with expansive diamond fins.",
            characteristics        = "Large squid with broad, diamond-shaped fins.",
            ecologicalRole         = "Hunts fish, squid, and shrimp.",
            interestingFact        = "Adults are often seen swimming in pairs.",
            rdpReward              = 105,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.40f,
            instanceCount          = 2,
            wanderRadius           = 25f,
            moveSpeed              = 3.5f,
            fleeSpeed              = 6.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.75f, 0.40f, 0.35f, 1f),
            placeholderScale       = new Vector3(0.9f, 2.2f, 0.9f),
            previewScaleMultiplier = 1.0f
        }));

        // 10. Blackbelly Lanternshark (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "BlackbellyLanternshark", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "blackbelly_shark_001",
            commonName             = "Blackbelly Lanternshark",
            scientificName         = "Etmopterus lucifer",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 2,
            habitat                = "Deep slopes, 150–1,357 m",
            depthRangeText         = "150–1,357 m",
            explorationHint        = "Patrolling upper bathyal slopes, glowing brightly underneath.",
            characteristics        = "Small dark shark with glowing areas underneath its body.",
            ecologicalRole         = "Hunts shrimp, squid, and small fish.",
            interestingFact        = "Its belly can glow in the dark.",
            rdpReward              = 95,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.12f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 3.0f,
            fleeSpeed              = 5.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.20f, 0.22f, 0.25f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.7f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 11. Portuguese Dogfish (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PortugueseDogfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "portuguese_dogfish_001",
            commonName             = "Portuguese Dogfish",
            scientificName         = "Centroscymnus coelolepis",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 2,
            habitat                = "Deep underwater slopes, 128–3,675 m",
            depthRangeText         = "128–3,675 m",
            explorationHint        = "Heavy dark shark prowling low along abyssal margins and deep underwater inclines.",
            characteristics        = "Small, dark shark with large eyes.",
            ecologicalRole         = "Hunts and scavenges for deep-sea animals.",
            interestingFact        = "It is one of the deepest-living sharks known.",
            rdpReward              = 110,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.89f,
            instanceCount          = 2,
            wanderRadius           = 22f,
            moveSpeed              = 2.6f,
            fleeSpeed              = 5.2f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.18f, 0.18f, 0.20f, 1f),
            placeholderScale       = new Vector3(0.7f, 2.0f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 12. Philippine Spurdog (Chondrichthyes)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PhilippineSpurdog", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "philippine_spurdog_001",
            commonName             = "Philippine Spurdog",
            scientificName         = "Squalus montalbani",
            taxonomicClass         = TaxonomicClass.Chondrichthyes,
            zoneIndex              = 2,
            habitat                = "Deep underwater slopes, 154–1,370 m",
            depthRangeText         = "154–1,370 m",
            explorationHint        = "Streamlined gray form hunting near upper bathyal rock drops.",
            characteristics        = "Gray-brown shark with a long body.",
            ecologicalRole         = "Eats small fish, squid, and shrimp.",
            interestingFact        = "Its original scientific description was based on a specimen from the Philippines.",
            rdpReward              = 95,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Hard,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.12f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 3.0f,
            fleeSpeed              = 5.8f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.45f, 0.42f, 0.40f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.8f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 13. Harp Sponge (Demospongiae)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "HarpSponge", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "harp_sponge_001",
            commonName             = "Harp Sponge",
            scientificName         = "Chondrocladia lyra",
            taxonomicClass         = TaxonomicClass.Demospongiae,
            zoneIndex              = 2,
            habitat                = "Deep seafloor, 3,316–3,399 m",
            depthRangeText         = "3,316–3,399 m",
            explorationHint        = "Anchored in deep bathyal mud, its multi-vaned lyre vanes reaching upward into the gloom.",
            characteristics        = "Has long branches shaped somewhat like a harp.",
            ecologicalRole         = "Captures tiny animals instead of only filtering food from water.",
            interestingFact        = "It looks like a harp growing from the seafloor.",
            rdpReward              = 125,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.77f,
            maxDepthFraction       = 0.80f,
            instanceCount          = 3,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.88f, 0.88f, 0.82f, 1f),
            placeholderScale       = new Vector3(1.6f, 1.8f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 14. Scaly-foot Snail (Gastropoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "ScalyFootSnail", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "scaly_foot_snail_001",
            commonName             = "Scaly-foot Snail",
            scientificName         = "Chrysomallon squamiferum",
            taxonomicClass         = TaxonomicClass.Gastropoda,
            zoneIndex              = 2,
            habitat                = "Hydrothermal vents, 2,400–2,900 m",
            depthRangeText         = "2,400–2,900 m",
            explorationHint        = "Clustered around black smoker chimneys with iron-sulfide armored scales.",
            characteristics        = "Has a shell and armor-like scales on its foot.",
            ecologicalRole         = "Lives near hot seafloor vents with helpful bacteria.",
            interestingFact        = "Its scales can contain iron minerals.",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Special, // Hydrothermal vents
            minDepthFraction       = 0.47f,
            maxDepthFraction       = 0.63f,
            instanceCount          = 5,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.20f, 0.20f, 0.20f, 1f), // iron black
            placeholderScale       = new Vector3(0.6f, 0.4f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 15. Giant Siphonophore (Hydrozoa)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "GiantSiphonophore", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "giant_siphonophore_001",
            commonName             = "Giant Siphonophore",
            scientificName         = "Marrus orthocanna",
            taxonomicClass         = TaxonomicClass.Hydrozoa,
            zoneIndex              = 2,
            habitat                = "Deep ocean, 200–2,000 m",
            depthRangeText         = "200–2,000 m",
            explorationHint        = "Enormous chain of translucent swimming bells and red digestive zooids drifting like a living rope.",
            characteristics        = "Long, transparent body made of many connected parts.",
            ecologicalRole         = "Uses stinging tentacles to catch small animals.",
            interestingFact        = "It looks like one animal but is actually a colony of many specialized parts.",
            rdpReward              = 115,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.33f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 1.5f,
            fleeSpeed              = 3.0f,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.85f, 0.30f, 0.30f, 0.75f),
            placeholderScale       = new Vector3(0.4f, 4.0f, 0.4f),
            previewScaleMultiplier = 0.8f
        }));

        // 16. Deep-sea Squat Lobster (Malacostraca)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaSquatLobster", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "squat_lobster_001",
            commonName             = "Deep-sea Squat Lobster",
            scientificName         = "Munidopsis lauensis",
            taxonomicClass         = TaxonomicClass.Malacostraca,
            zoneIndex              = 2,
            habitat                = "Deep hydrothermal vents, 1,136–2,000 m",
            depthRangeText         = "1,136–2,000 m",
            explorationHint        = "Scrambling across mineral chimney mounds, grazing on bacterial mats.",
            characteristics        = "Small lobster-like animal with long legs.",
            ecologicalRole         = "Feeds on food around deep-sea vents.",
            interestingFact        = "It lives in some of the most extreme environments in the ocean.",
            rdpReward              = 105,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Special,
            minDepthFraction       = 0.05f,
            maxDepthFraction       = 0.33f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.90f, 0.85f, 0.80f, 1f), // pale vent crab
            placeholderScale       = new Vector3(0.7f, 0.3f, 0.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 17. Sperm Whale (Mammalia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "SpermWhale", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "sperm_whale_001",
            commonName             = "Sperm Whale",
            scientificName         = "Physeter macrocephalus",
            taxonomicClass         = TaxonomicClass.Mammalia,
            zoneIndex              = 2,
            habitat                = "Deep ocean; dives to about 2,000 m or more",
            depthRangeText         = "dives to about 2,000 m or more",
            explorationHint        = "Enormous titan cruising into the abyss clicking powerful sonar pulses to hunt giant squid.",
            characteristics        = "Huge whale with a very large, square-shaped head.",
            ecologicalRole         = "Hunts large squid and other deep-sea animals.",
            interestingFact        = "It is one of the deepest-diving whales.",
            rdpReward              = 150,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.33f,
            instanceCount          = 1,
            wanderRadius           = 45f,
            moveSpeed              = 4.5f,
            fleeSpeed              = 6.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.28f, 0.30f, 0.34f, 1f),
            placeholderScale       = new Vector3(2.5f, 7.0f, 2.5f),
            previewScaleMultiplier = 0.5f
        }));

        // 18. Leatherback Sea Turtle (Reptilia)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LeatherbackSeaTurtle", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "leatherback_turtle_001",
            commonName             = "Leatherback Sea Turtle",
            scientificName         = "Dermochelys coriacea",
            taxonomicClass         = TaxonomicClass.Reptilia,
            zoneIndex              = 2,
            habitat                = "Open ocean; dives to about 1,280 m",
            depthRangeText         = "dives to about 1,280 m",
            explorationHint        = "Gliding with slow, powerful flipper strokes through the upper bathypelagic chill.",
            characteristics        = "Largest sea turtle with a soft, leathery shell.",
            ecologicalRole         = "Eats jellyfish and helps control their numbers.",
            interestingFact        = "It is the largest living sea turtle.",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.10f,
            instanceCount          = 2,
            wanderRadius           = 25f,
            moveSpeed              = 2.8f,
            fleeSpeed              = 5.0f,
            placeholderShape       = PlaceholderShape.Cylinder,
            placeholderColor       = new Color(0.22f, 0.26f, 0.28f, 1f),
            placeholderScale       = new Vector3(1.8f, 0.5f, 1.8f),
            previewScaleMultiplier = 0.8f
        }));

        // 19. Deep-sea Jellyfish (Scyphozoa)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaJellyfishAtolla", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "atolla_jellyfish_001",
            commonName             = "Deep-sea Jellyfish",
            scientificName         = "Atolla wyvillei",
            taxonomicClass         = TaxonomicClass.Scyphozoa,
            zoneIndex              = 2,
            habitat                = "Deep ocean, about 1,000–4,000 m",
            depthRangeText         = "1,000–4,000 m",
            explorationHint        = "Crimson coronal bell pulsing through the midnight gloom, emitting circular pinwheel flashes.",
            characteristics        = "Red jellyfish with a round bell and long tentacles.",
            ecologicalRole         = "Catches tiny animals drifting through the water.",
            interestingFact        = "It can produce flashing light when disturbed.",
            rdpReward              = 95,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 5,
            wanderRadius           = 16f,
            moveSpeed              = 1.4f,
            fleeSpeed              = 3.5f,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.70f, 0.12f, 0.15f, 0.9f),
            placeholderScale       = new Vector3(0.9f, 0.9f, 0.9f),
            previewScaleMultiplier = 1.0f
        }));

        SpeciesDataGenerator.UpdateRegistryForZone(2, createdAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MidnightSpeciesData] Successfully generated all {createdAssets.Count} official scientific species for Midnight Zone (Zone 2)!");
    }
}
#endif
