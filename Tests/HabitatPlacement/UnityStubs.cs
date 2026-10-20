// Engine sampling is substituted; the search method is extracted verbatim from production.
using System;

namespace UnityEngine
{
    public class GameObject { public string name; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public static float Cos(float x) => (float)Math.Cos(x);
        public static float Sin(float x) => (float)Math.Sin(x);
        public static float Abs(float x) => Math.Abs(x);
        public static float Min(float x, float y) => Math.Min(x, y);
        public static float Max(float x, float y) => Math.Max(x, y);
        public static float Lerp(float x, float y, float t) => x + (y - x) * t;
    }
    public static class Random
    {
        private static System.Random random = new System.Random(42);
        public static void InitState(int seed) => random = new System.Random(seed);
        public static float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();
    }
}

public enum BiomeBand { OpenWater, Soft, Hard, Rock, Special }
public class TerrainGenerator
{
    public Func<float, float, BiomeBand> Biome;
    public Func<float, float, float> Height;
    public Func<float, float, UnityEngine.Vector3> Normal = (x, z) => UnityEngine.Vector3.up;
    public BiomeBand GetBiomeAt(float x, float z) => Biome(x, z);
    public float SampleHeight(float x, float z) => Height(x, z);
    public UnityEngine.Vector3 SampleNormal(float x, float z) => Normal(x, z);
}

public class OceanFloorMeshGenerator
{
    public float Height;
    public bool Sunlight = true;
    public float BaseY => -1000f;
    public float SampleHeight(float x, float z) => Height;
    public UnityEngine.Vector3 SampleNormal(float x, float z) => UnityEngine.Vector3.up;
    public bool IsSunlightZone() => Sunlight;
}
public class EnvPropSet
{
    public class PropEntry
    {
        public UnityEngine.GameObject prefab;
        public BiomeBand targetBiome;
    }
}
