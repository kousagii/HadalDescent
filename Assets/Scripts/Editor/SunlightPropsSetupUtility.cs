#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Setup utility for Sunlight Zone environmental props and biomes.
/// Run via: Menu -> HadalDescent -> Setup Sunlight Zone Props & Biomes
/// </summary>
public static class SunlightPropsSetupUtility
{
    private const string PrefabFolder = "Assets/Prefabs/Environment/Sunlight Zone";
    private const string ArtsFolder   = "Assets/Arts/Environment/Sunlight Zone";
    private const string PropsAssetPath = "Assets/Scripts/ScriptableObjects/Environment/SunlightProps.asset";

    [MenuItem("HadalDescent/Setup Sunlight Zone Props & Biomes")]
    public static void SetupSunlightPropsMenu()
    {
        SetupSunlightProps(true);
    }

    public static void SetupSunlightProps(bool interactive = false)
    {
        Debug.Log("[SunlightPropsSetup] Beginning Sunlight Zone Props & Biome setup...");

        if (!Directory.Exists(PrefabFolder))
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
        }

        // 1. Ensure Coral Fan Prefab exists from prop_sun_coral_fan.glb
        string fanGlbPath = $"{ArtsFolder}/prop_sun_coral_fan.glb";
        string fanPrefabPath = $"{PrefabFolder}/prop_sun_coral_fan.prefab";
        if (!File.Exists(fanPrefabPath) && File.Exists(fanGlbPath))
        {
            GameObject glbObj = AssetDatabase.LoadAssetAtPath<GameObject>(fanGlbPath);
            if (glbObj != null)
            {
                GameObject instance = Object.Instantiate(glbObj);
                instance.name = "prop_sun_coral_fan";
                PrefabUtility.SaveAsPrefabAsset(instance, fanPrefabPath);
                Object.DestroyImmediate(instance);
                Debug.Log($"[SunlightPropsSetup] Created prefab: {fanPrefabPath}");
            }
        }

        // 2. Ensure Sand Ripple Prefab exists from bisect.fbx
        string ripplePrefabPath = $"{PrefabFolder}/prop_sun_sand_ripple.prefab";
        string bisectFbxPath = "Assets/Arts/Environment/rocks-low-poly-starter-pack/source/bisect.fbx";
        Material seabedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Environment/Textures/Sunlight_Seabed.mat");
        if (!File.Exists(ripplePrefabPath) && File.Exists(bisectFbxPath))
        {
            GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(bisectFbxPath);
            if (fbxObj != null)
            {
                GameObject instance = Object.Instantiate(fbxObj);
                instance.name = "prop_sun_sand_ripple";
                instance.transform.localScale = new Vector3(3.5f, 0.35f, 2.0f);
                var renderer = instance.GetComponentInChildren<Renderer>();
                if (renderer != null && seabedMat != null)
                {
                    renderer.sharedMaterial = seabedMat;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, ripplePrefabPath);
                Object.DestroyImmediate(instance);
                Debug.Log($"[SunlightPropsSetup] Created prefab: {ripplePrefabPath}");
            }
        }

        // 3. Load Rock Prefabs
        GameObject conePinnacle1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cone_001.prefab");
        GameObject conePinnacle2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cone_002.prefab");
        GameObject cubePlateau   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cube_001.prefab");
        GameObject icoBoulder1   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Icosphere_001.prefab");
        GameObject icoBoulder3   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Icosphere_003.prefab");
        GameObject sphereBoulder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Sphere_002.prefab");
        GameObject cubeCrevice   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cube_002.prefab");
        GameObject cubePillar    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/Cube_004.prefab");
        GameObject bisectSlab    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Rocks/bisect.prefab");

        // 4. Load Materials
        Material limestoneMat     = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Environment/Materials/Mat_Rock_Sunlight_Limestone.mat");
        Material algaeLimestoneMat= AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Environment/Materials/Mat_Rock_Sunlight_AlgaeLimestone.mat");
        Material graniteMat       = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Environment/Materials/Mat_Rock_Sunlight_Granite.mat");

        // 5. Load User Sunlight Prefabs
        GameObject seagrassPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/prop_sun_seagrass_patch.prefab");
        GameObject clamShellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/prop_sun_buried_clam_shell.prefab");
        GameObject sandRipplePrefab= AssetDatabase.LoadAssetAtPath<GameObject>(ripplePrefabPath);
        GameObject moundCoralPrefab= AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/coral by Kelli Ray - 7Cs3rTEcpcD.prefab");
        GameObject branchCoralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Orange Coral by Device Lab - 3HEc6LvqCJd.prefab");
        GameObject coralFanPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(fanPrefabPath);

        // 6. Populate SunlightProps.asset
        EnvPropSet propSet = AssetDatabase.LoadAssetAtPath<EnvPropSet>(PropsAssetPath);
        if (propSet == null)
        {
            propSet = ScriptableObject.CreateInstance<EnvPropSet>();
            AssetDatabase.CreateAsset(propSet, PropsAssetPath);
        }

        propSet.seabedMaterial         = seabedMat;
        propSet.propDensity            = 2.2f;
        propSet.globalScaleMultiplier  = 1.0f;

        var entries = new List<EnvPropSet.PropEntry>();

        // ── SOFT BIOME (Sand Flats & Lagoons) ──────────────────────────────
        if (seagrassPrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = seagrassPrefab,
                targetBiome         = BiomeBand.Soft,
                weight              = 4.5f,
                minScale            = 1.4f,
                maxScale            = 2.2f,
                scaleMultiplier     = Vector3.one,
                rotationOffset      = Vector3.zero,
                alignToSurface      = false,
                surfaceTiltStrength = 0f,
                isObstacle          = false
            });
        }

        if (clamShellPrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = clamShellPrefab,
                targetBiome         = BiomeBand.Soft,
                weight              = 2.5f,
                minScale            = 5.0f,
                maxScale            = 9.0f,
                scaleMultiplier     = Vector3.one,
                alignToSurface      = true,
                surfaceTiltStrength = 0.8f,
                isObstacle          = false
            });
        }

        if (sandRipplePrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = sandRipplePrefab,
                targetBiome         = BiomeBand.Soft,
                weight              = 2.0f,
                minScale            = 1.2f,
                maxScale            = 2.2f,
                scaleMultiplier     = new Vector3(3.5f, 0.35f, 2.0f),
                alignToSurface      = true,
                surfaceTiltStrength = 0.4f,
                isObstacle          = false
            });
        }

        // ── HARD BIOME (Coral Reef Formations) ─────────────────────────────
        if (moundCoralPrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = moundCoralPrefab,
                targetBiome         = BiomeBand.Hard,
                weight              = 2.8f,
                minScale            = 0.45f,
                maxScale            = 0.85f,
                scaleMultiplier     = Vector3.one,
                alignToSurface      = true,
                surfaceTiltStrength = 0.4f,
                isObstacle          = true
            });
        }

        if (branchCoralPrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = branchCoralPrefab,
                targetBiome         = BiomeBand.Hard,
                weight              = 3.0f,
                minScale            = 3.0f,
                maxScale            = 6.0f,
                scaleMultiplier     = Vector3.one,
                alignToSurface      = true,
                surfaceTiltStrength = 0.45f,
                isObstacle          = true
            });
        }

        if (coralFanPrefab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = coralFanPrefab,
                targetBiome         = BiomeBand.Hard,
                weight              = 1.8f,
                minScale            = 0.5f,
                maxScale            = 1.0f,
                scaleMultiplier     = Vector3.one,
                alignToSurface      = true,
                surfaceTiltStrength = 0.35f,
                isObstacle          = true
            });
        }

        if (conePinnacle1 != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = conePinnacle1,
                targetBiome         = BiomeBand.Hard,
                weight              = 2.2f,
                minScale            = 25.0f,
                maxScale            = 50.0f,
                scaleMultiplier     = new Vector3(1.0f, 1.5f, 1.0f),
                materialOverride    = limestoneMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.35f,
                isObstacle          = true
            });
        }

        if (conePinnacle2 != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = conePinnacle2,
                targetBiome         = BiomeBand.Hard,
                weight              = 1.8f,
                minScale            = 20.0f,
                maxScale            = 40.0f,
                scaleMultiplier     = new Vector3(0.9f, 1.6f, 0.9f),
                materialOverride    = limestoneMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.35f,
                isObstacle          = true
            });
        }

        if (cubePlateau != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = cubePlateau,
                targetBiome         = BiomeBand.Hard,
                weight              = 2.2f,
                minScale            = 25.0f,
                maxScale            = 50.0f,
                scaleMultiplier     = new Vector3(2.2f, 0.45f, 2.2f),
                materialOverride    = algaeLimestoneMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.65f,
                isObstacle          = true
            });
        }

        // ── ROCK BIOME (Coastal Outcrops & Crevices) ───────────────────────
        if (icoBoulder1 != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = icoBoulder1,
                targetBiome         = BiomeBand.Rock,
                weight              = 3.0f,
                minScale            = 25.0f,
                maxScale            = 50.0f,
                scaleMultiplier     = new Vector3(1.2f, 1.2f, 1.2f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.75f,
                isObstacle          = true
            });
        }

        if (icoBoulder3 != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = icoBoulder3,
                targetBiome         = BiomeBand.Rock,
                weight              = 2.5f,
                minScale            = 30.0f,
                maxScale            = 60.0f,
                scaleMultiplier     = new Vector3(1.2f, 1.2f, 1.2f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.7f,
                isObstacle          = true
            });
        }

        if (sphereBoulder != null)
        {
            // Sphere_002 is exported in meters (3.77m x 2.22m x 0.40m).
            // Tuned to 0.45x - 0.85x so it forms a natural 1.7m - 3.2m reef boulder outcrop.
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = sphereBoulder,
                targetBiome         = BiomeBand.Rock,
                weight              = 2.0f,
                minScale            = 0.45f,
                maxScale            = 0.85f,
                scaleMultiplier     = new Vector3(1.0f, 1.0f, 1.5f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.8f,
                isObstacle          = true
            });
        }

        if (cubeCrevice != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = cubeCrevice,
                targetBiome         = BiomeBand.Rock,
                weight              = 2.2f,
                minScale            = 20.0f,
                maxScale            = 40.0f,
                scaleMultiplier     = new Vector3(1.5f, 1.2f, 1.8f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.7f,
                isObstacle          = true
            });
        }

        if (cubePillar != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = cubePillar,
                targetBiome         = BiomeBand.Rock,
                weight              = 2.0f,
                minScale            = 25.0f,
                maxScale            = 50.0f,
                scaleMultiplier     = new Vector3(1.2f, 1.6f, 1.2f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.6f,
                isObstacle          = true
            });
        }

        if (bisectSlab != null)
        {
            entries.Add(new EnvPropSet.PropEntry
            {
                prefab              = bisectSlab,
                targetBiome         = BiomeBand.Rock,
                weight              = 1.8f,
                minScale            = 20.0f,
                maxScale            = 40.0f,
                scaleMultiplier     = new Vector3(2.0f, 0.35f, 1.8f),
                materialOverride    = graniteMat,
                alignToSurface      = true,
                surfaceTiltStrength = 0.5f,
                isObstacle          = true
            });
        }

        propSet.props = entries.ToArray();
        EditorUtility.SetDirty(propSet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SunlightPropsSetup] Successfully configured SunlightProps.asset with {entries.Count} biome-mapped props.");

        if (interactive)
        {
            EditorUtility.DisplayDialog(
                "Sunlight Props Setup Complete",
                $"Successfully configured SunlightProps.asset with {entries.Count} biome-mapped environmental props!\n\n" +
                "• Soft Biome: Seagrass, Buried Clam Shells, Sand Ripples\n" +
                "• Hard Biome: Mound Corals, Branching Corals (with dynamic color variation), Coral Fans, Limestone Pinnacles, Plateau Ledges\n" +
                "• Rock Biome: Granite Boulders, Crevice Outcrops\n\n" +
                "Atmospheric VFX (Godrays & Plankton) will automatically simulate during gameplay in Sunlight Zone.",
                "OK"
            );
        }
    }
}
#endif
