using System;
using UnityEngine;

public partial class PlacementChecks
{
    private static int checks;

    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }

    private static TerrainGenerator Flat(BiomeBand biome, float height) => new TerrainGenerator
    {
        Biome = (x, z) => biome,
        Height = (x, z) => height
    };

    private bool Find(TerrainGenerator terrain, bool grass, out Vector3 position,
        float halfWidth = 240f, float halfLength = 240f, int zone = 0) =>
        FindFlatBiomeLocation(terrain, grass ? BiomeBand.Soft : BiomeBand.Hard,
            Mathf.PI, halfWidth, halfLength, 22f, out position, out _, out _, zone, !grass, grass);

    public static void Main()
    {
        var search = new PlacementChecks();
        Check("Grass accepts shallow soft sediment", search.Find(Flat(BiomeBand.Soft, -20f), true, out _));
        Check("Coral accepts hard reef", search.Find(Flat(BiomeBand.Hard, -30f), false, out _));
        Check("Grass never falls back to hard reef", !search.Find(Flat(BiomeBand.Hard, -20f), true, out _));
        Check("Coral never falls back to soft sediment", !search.Find(Flat(BiomeBand.Soft, -30f), false, out _));
        Check("Grass rejects deep soft sediment", !search.Find(Flat(BiomeBand.Soft, -80f), true, out _));
        Check("Sunlight coral rejects deep reef", !search.Find(Flat(BiomeBand.Hard, -80f), false, out _));
        Check("Grass accepts 40m boundary", search.Find(Flat(BiomeBand.Soft, -40f), true, out _));
        Check("Coral accepts 50m boundary", search.Find(Flat(BiomeBand.Hard, -50f), false, out _));
        Check("Cold coral retains deeper habitat", search.Find(Flat(BiomeBand.Hard, -500f), false, out _, zone: 1));
        Check("Missing terrain has no unchecked fallback", !search.Find(null, true, out _));

        var smallPatch = Flat(BiomeBand.Soft, -20f);
        smallPatch.Biome = (x, z) => x * x + z * z < 100f ? BiomeBand.Soft : BiomeBand.Hard;
        Check("Center match cannot straddle wrong habitat", !search.Find(smallPatch, true, out _));

        var deepEdges = Flat(BiomeBand.Soft, -20f);
        deepEdges.Height = (x, z) => x * x + z * z < 100f ? -20f : -80f;
        Check("Entire footprint must meet depth rule", !search.Find(deepEdges, true, out _));

        var cliffs = Flat(BiomeBand.Hard, -30f);
        cliffs.Normal = (x, z) => new Vector3(0f, 0.6f, 0f);
        Check("Cliff is rejected", !search.Find(cliffs, false, out _));

        var distantHabitat = Flat(BiomeBand.Soft, -20f);
        distantHabitat.Biome = (x, z) => x > 100f && z > 25f ? BiomeBand.Soft : BiomeBand.Hard;
        for (int seed = 0; seed < 30; seed++)
        {
            UnityEngine.Random.InitState(seed);
            Check("Full rectangular search finds habitat outside preferred sector",
                search.Find(distantHabitat, true, out var pos, halfLength: 80f));
            Check("Full footprint stays in rectangular bounds", Math.Abs(pos.x) + 22f <= 240f && Math.Abs(pos.z) + 22f <= 80f);
            Check("Distant footprint belongs to soft habitat", pos.x - 22f > 100f && pos.z - 22f > 25f);
        }

        search.Find(Flat(BiomeBand.Soft, -20f), true, out var first);
        _registeredLandmarks.Add(new LandmarkZone { center = first, radius = 22f });
        Check("Second landmark has a valid separated site", search.Find(Flat(BiomeBand.Soft, -20f), true, out var second));
        float dx = first.x - second.x, dz = first.z - second.z;
        Check("Landmark footprints do not overlap", dx * dx + dz * dz >= 49f * 49f);

        var soft = Flat(BiomeBand.Soft, -20f);
        var hard = Flat(BiomeBand.Hard, -20f);
        Check("Grass prop accepts soft sediment", PropChecks.CanPlace("seagrass", soft, -20f));
        Check("Grass prop rejects hard reef", !PropChecks.CanPlace("seagrass", hard, -20f));
        Check("Coral prop accepts hard reef", PropChecks.CanPlace("coral", hard, -30f));
        Check("Coral prop rejects soft sediment", !PropChecks.CanPlace("coral", soft, -30f));
        Check("Grass prop rejects deep sediment", !PropChecks.CanPlace("seagrass", soft, -80f));
        Check("Coral prop rejects deep sunlight terrain", !PropChecks.CanPlace("coral", hard, -80f));
        Check("Cold coral prop accepts deep reef", PropChecks.CanPlace("coral", hard, -500f, sunlight: false));
        soft.Biome = (x, z) => x < 0f ? BiomeBand.Soft : BiomeBand.Hard;
        Check("Cluster offset checks destination habitat", !PropChecks.CanPlace("seagrass", soft, -20f, x: 1f));
        Check("Cluster origin in soft habitat remains valid", PropChecks.CanPlace("seagrass", soft, -20f, x: -1f));
        Console.WriteLine($"PASS: {checks} habitat placement checks.");
    }
}

public partial class PropChecks
{
    public static bool CanPlace(string name, TerrainGenerator terrain, float height, bool sunlight = true, float x = 0f)
    {
        var scatterer = new PropChecks { _terrain = terrain };
        var entry = new EnvPropSet.PropEntry { prefab = new GameObject { name = name } };
        return scatterer.TryPlacePropInstance(new OceanFloorMeshGenerator { Height = height, Sunlight = sunlight },
            null, entry, x, 0f, new System.Collections.Generic.List<Vector3>(), 0.9f, -1);
    }
}
