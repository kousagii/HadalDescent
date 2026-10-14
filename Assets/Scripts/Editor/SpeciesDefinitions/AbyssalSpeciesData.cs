#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AbyssalSpeciesData
{
    public static void Generate()
    {
        string folder = "Assets/Scripts/ScriptableObjects/Species/Abyssal Zone";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObjects/Species", "Abyssal Zone");

        var createdAssets = new List<SpeciesData>();

        // 1. Abyssal Grenadier (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "AbyssalGrenadier", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "abyssal_grenadier_001",
            commonName             = "Abyssal Grenadier",
            scientificName         = "Coryphaenoides armatus",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep seafloor (282-5,180 m)",
            depthRangeText         = "282–5,180 m",
            explorationHint        = "Long tapering body and broad head scouring the deep seafloor.",
            characteristics        = "Long body, large head, tapering tail",
            ecologicalRole         = "Scavenges food on the seafloor",
            interestingFact        = "Uses sensitive sensory structures to locate food in darkness",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.65f,
            instanceCount          = 3,
            wanderRadius           = 22f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.40f, 0.38f, 0.35f, 1f),
            placeholderScale       = new Vector3(0.7f, 2.2f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 2. Rough Abyssal Grenadier (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "RoughAbyssalGrenadier", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "rough_grenadier_001",
            commonName             = "Rough Abyssal Grenadier",
            scientificName         = "Coryphaenoides yaquinae",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep seafloor (3,400–5,800 m)",
            depthRangeText         = "3,400–5,800 m",
            explorationHint        = "Hovering over vast abyssal mud plains, undulating its long rat-like tail.",
            characteristics        = "Long body, large head, and narrow pointed tail.",
            ecologicalRole         = "Searches the seafloor for small prey.",
            interestingFact        = "The name honors the research vessel Yaquina.",
            rdpReward              = 115,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.90f,
            instanceCount          = 3,
            wanderRadius           = 22f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.35f, 0.32f, 0.30f, 1f),
            placeholderScale       = new Vector3(0.7f, 2.2f, 0.7f),
            previewScaleMultiplier = 1.0f
        }));

        // 3. Threadfin grenadier (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "ThreadfinGrenadier", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "threadfin_grenadier_001",
            commonName             = "Threadfin grenadier",
            scientificName         = "Coryphaenoides filicauda",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep water( 2500 - 5000 m)",
            depthRangeText         = "2,500–5,000 m",
            explorationHint        = "Watch for a delicate whip-like filament trailing behind its slender tail.",
            characteristics        = "Elongated body, pointed snout, and long, tapering tail.",
            ecologicalRole         = "Deep-sea predator that helps maintain the food web.",
            interestingFact        = "Can grow up to 40 cm long and lives at abyssal depths.",
            rdpReward              = 110,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.75f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.2f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.42f, 0.40f, 0.38f, 1f),
            placeholderScale       = new Vector3(0.5f, 2.0f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 4. Abyssal Spiderfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "AbyssalSpiderfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "abyssal_spiderfish_001",
            commonName             = "Abyssal Spiderfish",
            scientificName         = "Bathypterois longipes",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep ocean floor (2,615–5,610 m)",
            depthRangeText         = "2,615–5,610 m",
            explorationHint        = "Standing rigid atop extended pectoral and caudal rays against abyssal bottom currents.",
            characteristics        = "Small body with extremely long fins extending downward like legs.",
            ecologicalRole         = "Waits for small animals carried by currents.",
            interestingFact        = "The long fins make the fish look like it is standing.",
            rdpReward              = 130,
            scanDifficulty         = 3,
            isStationary           = true, // Tripod bottom-stander
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.81f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.55f, 0.52f, 0.48f, 1f),
            placeholderScale       = new Vector3(0.4f, 1.8f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 5. Highfin Lizardfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "HighfinLizardfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "highfin_lizardfish_001",
            commonName             = "Highfin Lizardfish",
            scientificName         = "Bathysaurus mollis",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep ocean floor (1,550–4,903 m)",
            depthRangeText         = "1,550–4,903 m",
            explorationHint        = "Lying flat in the ooze, tilting its sharp-toothed jaw upward in ambush posture.",
            characteristics        = "Long body, large mouth, and sharp teeth.",
            ecologicalRole         = "Hunts fish and other animals near the bottom.",
            interestingFact        = "One animal can produce both eggs and sperm.",
            rdpReward              = 125,
            scanDifficulty         = 4,
            isStationary           = true, // Ambush benthic predator
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.45f,
            instanceCount          = 3,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.38f, 0.35f, 0.30f, 1f),
            placeholderScale       = new Vector3(0.6f, 2.2f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 6. Pudgy Cusk-eel (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PudgyCuskeel", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "pudgy_cuskeel_001",
            commonName             = "Pudgy Cusk-eel",
            scientificName         = "Spectrunculus grandis",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep ocean floor (800–4,300 m)",
            depthRangeText         = "800–4,300 m",
            explorationHint        = "Pale, bulky body meandering lazily near the upper boundary of the abyss.",
            characteristics        = "Long body, large head, and wide mouth.",
            ecologicalRole         = "Eats worms, snails, shrimp, and other small animals.",
            interestingFact        = "The body can grow to about 1.3 m long.",
            rdpReward              = 110,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.15f,
            instanceCount          = 2,
            wanderRadius           = 18f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.2f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.70f, 0.65f, 0.60f, 1f),
            placeholderScale       = new Vector3(0.8f, 2.4f, 0.8f),
            previewScaleMultiplier = 0.9f
        }));

        // 7. Philippine Cutthroat Eel (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "PhilippineCutthroatEel", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "cutthroat_eel_001",
            commonName             = "Philippine Cutthroat Eel",
            scientificName         = "Ilyophis robinsae",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep water off the Philippines (up to 4,800 m)",
            depthRangeText         = "up to 4,800 m",
            explorationHint        = "Sinusoidal black ribbon slithering through dense mud fissures.",
            characteristics        = "Long, narrow eel-like body with a small head.",
            ecologicalRole         = "Feeds on small deep-water animals.",
            interestingFact        = "FishBase specifically records the species off the Philippines.",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = true,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.40f,
            instanceCount          = 3,
            wanderRadius           = 16f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 5.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.15f, 0.14f, 0.18f, 1f),
            placeholderScale       = new Vector3(0.35f, 2.2f, 0.35f),
            previewScaleMultiplier = 1.0f
        }));

        // 8. Deep-water Arrowtooth Eel (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepWaterArrowtoothEel", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "arrowtooth_eel_001",
            commonName             = "Deep-water Arrowtooth Eel",
            scientificName         = "Histiobranchus bathybius",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep slopes and abyssal plains (295–5,440 m)",
            depthRangeText         = "295–5,440 m",
            explorationHint        = "Sleek dark eel actively patrolling abyssal sediment basins.",
            characteristics        = "Long dark body with a narrow head and small fins.",
            ecologicalRole         = "Hunts fish, shrimp, and squid.",
            interestingFact        = "The same species occurs in both the Pacific and Indo-Pacific.",
            rdpReward              = 115,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.72f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 2.6f,
            fleeSpeed              = 5.2f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.22f, 0.20f, 0.22f, 1f),
            placeholderScale       = new Vector3(0.4f, 2.0f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 9. Headlight Fish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "HeadlightFish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "headlight_fish_001",
            commonName             = "Headlight Fish",
            scientificName         = "Diaphus effulgens",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep open ocean (up to 6,000 m)",
            depthRangeText         = "up to 6,000 m",
            explorationHint        = "Paired headlights beaming brightly forward from prominent snout photophores.",
            characteristics        = "Small body with several light-producing organs.",
            ecologicalRole         = "Eats tiny drifting animals and supports the deep-ocean food web.",
            interestingFact        = "Different light organs can produce different patterns of light.",
            rdpReward              = 100,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 1.00f,
            instanceCount          = 5,
            wanderRadius           = 16f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.5f,
            placeholderShape       = PlaceholderShape.Cube,
            placeholderColor       = new Color(0.45f, 0.60f, 0.75f, 1f),
            placeholderScale       = new Vector3(0.4f, 0.8f, 0.3f),
            previewScaleMultiplier = 1.0f
        }));

        // 10. Flabby Whalefish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "FlabbyWhalefish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "flabby_whalefish_001",
            commonName             = "Flabby Whalefish",
            scientificName         = "Cetichthys parini",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep open ocean (0–5,000 m)",
            depthRangeText         = "0–5,000 m",
            explorationHint        = "Orange-red velvet body drifting with soft, loosely-constructed fins.",
            characteristics        = "Elongated body with a soft-looking shape and small fins.",
            ecologicalRole         = "Eats tiny animals in deep water.",
            interestingFact        = "FishBase describes it as apparently the deepest-living whalefish.",
            rdpReward              = 130,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.50f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 2.0f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.85f, 0.30f, 0.20f, 1f), // intense crimson/orange
            placeholderScale       = new Vector3(0.6f, 1.6f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 11. Oceanic Lightfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "OceanicLightfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "oceanic_lightfish_001",
            commonName             = "Oceanic Lightfish",
            scientificName         = "Vinciguerria nimbaria",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Open ocean (20–5,000 m)",
            depthRangeText         = "20–5,000 m",
            explorationHint        = "Small silver fish with rows of ventral pearls sparkling in the abyss.",
            characteristics        = "Small silver-sided body with several glowing organs.",
            ecologicalRole         = "Eats tiny drifting animals.",
            interestingFact        = "Light organs appear at several points on the body.",
            rdpReward              = 95,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.50f,
            instanceCount          = 5,
            wanderRadius           = 15f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.8f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.70f, 0.75f, 0.82f, 1f),
            placeholderScale       = new Vector3(0.35f, 1.0f, 0.35f),
            previewScaleMultiplier = 1.0f
        }));

        // 12. Blind Cirrate Octopus (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "BlindCirrateOctopus", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "blind_cirrate_octopus_001",
            commonName             = "Blind Cirrate Octopus",
            scientificName         = "Cirrothauma murrayi",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 3,
            habitat                = "Below the ocean’s surface (1,500 - 4,500 m)",
            depthRangeText         = "1,500–4,500 m",
            explorationHint        = "Gelatinous body with paddle-like fins drifting gently in midwater abyssal currents.",
            characteristics        = "Gelatinous body, large ear-like fins, webbed arms, and reduced eyes.",
            ecologicalRole         = "Deep-sea predator that likely feeds on small crustaceans and other invertebrates.",
            interestingFact        = "Its eyes lack lenses and cannot form images, but they can still detect light.",
            rdpReward              = 135,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            locomotionArchetype    = LocomotionArchetype.PulsatileJetter,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.35f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 3.8f,
            pulseFrequency         = 0.6f,
            pulseDutyCycle         = 0.35f,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.95f, 0.85f, 0.90f, 0.75f),
            placeholderScale       = new Vector3(1.0f, 1.0f, 1.0f),
            previewScaleMultiplier = 1.0f
        }));

        // 13. Emperor Dumbo Octopus (Cephalopoda)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "EmperorDumboOctopus", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "emperor_dumbo_octopus_001",
            commonName             = "Emperor Dumbo Octopus",
            scientificName         = "Grimpoteuthis imperator",
            taxonomicClass         = TaxonomicClass.Cephalopoda,
            zoneIndex              = 3,
            habitat                = "Deep-water (3900–4400m)",
            depthRangeText         = "3,900–4,400 m",
            explorationHint        = "Flapping ear-like fins gracefully navigating just above the abyssal plain.",
            characteristics        = "Soft, gelatinous body, ear-like fins, and eight webbed arms with small suckers.",
            ecologicalRole         = "Predator that likely feeds on small deep-sea invertebrates.",
            interestingFact        = "Scientists described the species in 2021 using MRI and CT scanning to preserve its rare specimen.",
            rdpReward              = 140,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            locomotionArchetype    = LocomotionArchetype.PulsatileJetter,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.25f,
            instanceCount          = 3,
            wanderRadius           = 16f,
            moveSpeed              = 1.6f,
            fleeSpeed              = 3.5f,
            pulseFrequency         = 0.7f,
            pulseDutyCycle         = 0.35f,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.85f, 0.50f, 0.65f, 0.85f),
            placeholderScale       = new Vector3(1.1f, 1.1f, 1.1f),
            previewScaleMultiplier = 1.0f
        }));

        // 14. Bony-eared Assfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "BonyEaredAssfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "bony_eared_assfish_001",
            commonName             = "Bony-eared Assfish",
            scientificName         = "Acanthonus armatus",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep-water (1171 - 4415 m)",
            depthRangeText         = "1,171–4,415 m",
            explorationHint        = "Enormous bulbous spiny head tapering to a tiny tail hovering above sediment.",
            characteristics        = "Large head, tapering body, small eyes, and prominent head spines.",
            ecologicalRole         = "Deep-sea consumer that contributes to the marine food web.",
            interestingFact        = "Has one of the smallest brain-to-body size ratios among bony fishes.",
            rdpReward              = 120,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            locomotionArchetype    = LocomotionArchetype.PelagicCruiser,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.25f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 3.8f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.60f, 0.55f, 0.50f, 1f),
            placeholderScale       = new Vector3(0.6f, 1.8f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 15. Tripodfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "Tripodfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "tripodfish_001",
            commonName             = "Tripodfish",
            scientificName         = "Bathypterois grallator",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep- water (878 - 4720 m)",
            depthRangeText         = "878–4,720 m",
            explorationHint        = "Perched motionless on elongated tripod stilts facing incoming bottom currents.",
            characteristics        = "Slender body with extremely long pelvic and tail fin rays.",
            ecologicalRole         = "Feeds on copepods and other small planktonic crustaceans.",
            interestingFact        = "Uses its elongated fins to stand above the seafloor while facing the current.",
            rdpReward              = 130,
            scanDifficulty         = 3,
            isStationary           = true, // Tripod bottom-stander
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            locomotionArchetype    = LocomotionArchetype.BenthicFollower,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.40f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.70f, 0.65f, 0.60f, 1f),
            placeholderScale       = new Vector3(0.4f, 1.8f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 16. Kaup's Arrowtooth Eel (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "KaupsArrowtoothEel", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "kaups_arrowtooth_eel_001",
            commonName             = "Kaup's Arrowtooth Eel",
            scientificName         = "Synaphobranchus kaupii",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep-water (120 - 4800 m)",
            depthRangeText         = "120–4,800 m",
            explorationHint        = "Dark purplish-gray eel undulating sinuously over abyssal slopes.",
            characteristics        = "Long, eel-like body with dark purplish-gray coloration.",
            ecologicalRole         = "Predator of crustaceans, fishes, and cephalopods.",
            interestingFact        = "Larger individuals primarily eat fish, while smaller ones prefer crustaceans.",
            rdpReward              = 115,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            locomotionArchetype    = LocomotionArchetype.Serpentine,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.45f,
            instanceCount          = 3,
            wanderRadius           = 20f,
            moveSpeed              = 2.4f,
            fleeSpeed              = 4.8f,
            undulationFrequency    = 1.6f,
            undulationAmplitude    = 14f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.28f, 0.22f, 0.32f, 1f),
            placeholderScale       = new Vector3(0.4f, 2.2f, 0.4f),
            previewScaleMultiplier = 1.0f
        }));

        // 17. Barbeled Dragonfish (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "BarbeledDragonfish", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "barbeled_dragonfish_001",
            commonName             = "Barbeled Dragonfish",
            scientificName         = "Leptostomias gladiator",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep open ocean (reaches about 5,000 m)",
            depthRangeText         = "reaches about 5,000 m",
            explorationHint        = "Jet-black elongated predator trailing an iridescent luminous chin barbel.",
            characteristics        = "Long dark body, large teeth, and a glowing barbel.",
            ecologicalRole         = "Hunts smaller deep-sea animals.",
            interestingFact        = "The light-producing barbel can help attract prey.",
            rdpReward              = 125,
            scanDifficulty         = 4,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.OpenWater,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.50f,
            instanceCount          = 2,
            wanderRadius           = 22f,
            moveSpeed              = 3.2f,
            fleeSpeed              = 6.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.10f, 0.10f, 0.12f, 1f),
            placeholderScale       = new Vector3(0.5f, 2.6f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 18. Longnose Grenadier (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "LongnoseGrenadier", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "longnose_grenadier_001",
            commonName             = "Longnose Grenadier",
            scientificName         = "Paracetonurus flagellicauda",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep ocean floor (2,085–4,500 m)",
            depthRangeText         = "2,085–4,500 m",
            explorationHint        = "Pointed snout rooting into soft pelagic sediment.",
            characteristics        = "Slender body with a very long, thin tail.",
            ecologicalRole         = "Searches the bottom for small prey.",
            interestingFact        = "The tail gives the fish a distinctive flag-like shape.",
            rdpReward              = 110,
            scanDifficulty         = 3,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.25f,
            instanceCount          = 3,
            wanderRadius           = 18f,
            moveSpeed              = 2.2f,
            fleeSpeed              = 4.6f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.48f, 0.44f, 0.40f, 1f),
            placeholderScale       = new Vector3(0.6f, 2.0f, 0.6f),
            previewScaleMultiplier = 1.0f
        }));

        // 19. Deep-sea Eelpout (Actinopterygii)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaEelpout", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "deepsea_eelpout_001",
            commonName             = "Deep-sea Eelpout",
            scientificName         = "Pachycara priedei",
            taxonomicClass         = TaxonomicClass.Actinopterygii,
            zoneIndex              = 3,
            habitat                = "Deep seafloor (reaches about 4,275 m)",
            depthRangeText         = "reaches about 4,275 m",
            explorationHint        = "Pale serpentine body resting motionless across abyssal sediment pockets.",
            characteristics        = "Long, pale body with smooth-looking skin.",
            ecologicalRole         = "Feeds on small animals near the bottom.",
            interestingFact        = "The species was named after deep-sea researcher Imants Priede.",
            rdpReward              = 105,
            scanDifficulty         = 2,
            isStationary           = false,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.14f,
            instanceCount          = 3,
            wanderRadius           = 14f,
            moveSpeed              = 1.8f,
            fleeSpeed              = 4.0f,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.75f, 0.70f, 0.68f, 1f),
            placeholderScale       = new Vector3(0.5f, 1.8f, 0.5f),
            previewScaleMultiplier = 1.0f
        }));

        // 20. Deep-sea Sea Cucumber (Holothuroidea)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaSeaCucumber", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "psychropotes_cucumber_001",
            commonName             = "Deep-sea Sea Cucumber",
            scientificName         = "Psychropotes longicauda",
            taxonomicClass         = TaxonomicClass.Holothuroidea,
            zoneIndex              = 3,
            habitat                = "Deep seafloor (1,100–5,173 m)",
            depthRangeText         = "1,100–5,173 m",
            explorationHint        = "Vibrant purple gelatinous sea cucumber with a massive sail-like dorsal appendage crawling over mud.",
            characteristics        = "Soft body with a long tail-like extension.",
            ecologicalRole         = "Eats food particles that settle on the seafloor.",
            interestingFact        = "The unusual shape gives it an almost alien appearance.",
            rdpReward              = 120,
            scanDifficulty         = 2,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.59f,
            instanceCount          = 4,
            placeholderShape       = PlaceholderShape.Capsule,
            placeholderColor       = new Color(0.55f, 0.15f, 0.60f, 1f), // deep purple "gummy squirrel"
            placeholderScale       = new Vector3(1.0f, 0.5f, 1.8f),
            previewScaleMultiplier = 1.0f
        }));

        // 21. Deep-sea Sea Spider (Pycnogonida)
        createdAssets.Add(SpeciesDataGenerator.CreateOrUpdateSpecies(folder, "DeepSeaSeaSpider", new SpeciesDataGenerator.SpeciesDataConfig
        {
            speciesId              = "colossendeis_spider_001",
            commonName             = "Deep-sea Sea Spider",
            scientificName         = "Colossendeis megalonyx",
            taxonomicClass         = TaxonomicClass.Pycnogonida,
            zoneIndex              = 3,
            habitat                = "Deep seafloor (3–5,000 m)",
            depthRangeText         = "3–5,000 m",
            explorationHint        = "Giant spindly orange legs spanning wide across abyssal silt.",
            characteristics        = "Tiny body with very long legs and a long feeding tube.",
            ecologicalRole         = "Feeds on hydroids and other small animals.",
            interestingFact        = "The legs can be many times longer than the body.",
            rdpReward              = 125,
            scanDifficulty         = 3,
            isStationary           = true,
            isShy                  = false,
            preferredBiome         = BiomeBand.Soft,
            minDepthFraction       = 0.00f,
            maxDepthFraction       = 0.50f,
            instanceCount          = 3,
            placeholderShape       = PlaceholderShape.Sphere,
            placeholderColor       = new Color(0.92f, 0.45f, 0.15f, 1f),
            placeholderScale       = new Vector3(1.8f, 0.3f, 1.8f),
            previewScaleMultiplier = 1.0f
        }));

        SpeciesDataGenerator.UpdateRegistryForZone(3, createdAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AbyssalSpeciesData] Successfully generated all {createdAssets.Count} official scientific species for Abyssal Zone (Zone 3)!");
    }
}
#endif
