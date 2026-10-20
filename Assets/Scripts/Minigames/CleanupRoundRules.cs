using System;
using System.Collections.Generic;
using Category = EnvironmentalCleanupMinigame.DebrisCategory;
using Item = EnvironmentalCleanupMinigame.DebrisItemType;

/// <summary>Round generation and sorting scores, independent of scene objects and input.</summary>
public static class CleanupRoundRules
{
    public enum Variation { MixedCleanup, CurrentSweep, HeavyRecovery, PriorityRecovery }
    public const int MaximumItems = 11;
    public const float PhaseDurationSeconds = 30f;

    public static List<Item> BuildManifest(int zone, int plastics, int metals, int hazards,
        Variation variation, Random random)
    {
        var result = new List<Item>();
        AddCategory(result, Category.Plastic, Math.Max(0, Math.Min(4, plastics)), random);
        AddCategory(result, Category.Metal, Math.Max(0, Math.Min(4, metals)), random);
        AddCategory(result, Category.Hazardous, Math.Max(0, Math.Min(3, hazards)), random);
        int extra = Math.Max(0, Math.Min(4, zone));
        for (int i = 0; i < extra && result.Count < MaximumItems; i++)
        {
            Category category = variation == Variation.CurrentSweep ? Category.Plastic :
                variation == Variation.HeavyRecovery ? Category.Metal :
                variation == Variation.PriorityRecovery ? Category.Hazardous : (Category)random.Next(3);
            AddCategory(result, category, 1, random);
        }
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Item swap = result[i]; result[i] = result[j]; result[j] = swap;
        }
        return result;
    }

    private static void AddCategory(List<Item> output, Category category, int count, Random random)
    {
        Item[] pool = category == Category.Plastic
            ? new[] { Item.WaterBottle, Item.PlasticBag, Item.SixPackRings }
            : category == Category.Metal
                ? new[] { Item.SodaCan, Item.MetalPipe, Item.MetalTube, Item.MetalDrum }
                : new[] { Item.BatterySmall, Item.BatteryLarge, Item.ElectronicWaste };
        // Cycle a shuffled pool so small rounds show different silhouettes before duplicates.
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Item swap = pool[i]; pool[i] = pool[j]; pool[j] = swap;
        }
        for (int i = 0; i < count && output.Count < MaximumItems; i++) output.Add(pool[i % pool.Length]);
    }

    public static Category CategoryOf(Item item)
    {
        switch (item)
        {
            case Item.WaterBottle: case Item.PlasticBag: case Item.SixPackRings: return Category.Plastic;
            case Item.SodaCan: case Item.MetalPipe: case Item.MetalTube: case Item.MetalDrum: return Category.Metal;
            default: return Category.Hazardous;
        }
    }

    public static float SortingSeconds(int count) => count <= 0 ? 0f : PhaseDurationSeconds;

    public static bool TouchesFish(float centerX, float centerY, float halfWidth, float halfHeight,
        float fishX, float fishY)
    {
        return Math.Abs(centerX - fishX) < halfWidth + 0.55f &&
            Math.Abs(centerY - fishY) < halfHeight + 0.38f;
    }

    public sealed class SortingScore
    {
        private readonly HashSet<int> _sorted = new HashSet<int>();
        private readonly HashSet<int> _missed = new HashSet<int>();
        public int SortedCount => _sorted.Count;
        public int FirstTryCount { get; private set; }
        public int Streak { get; private set; }
        public int Bonus { get; private set; }

        public bool Submit(int index, Category actual, Category selected)
        {
            if (index < 0 || _sorted.Contains(index)) return false;
            if (actual != selected)
            {
                _missed.Add(index);
                Streak = 0;
                return false;
            }
            _sorted.Add(index);
            if (!_missed.Contains(index)) FirstTryCount++;
            Streak++;
            if (Streak % 3 == 0) Bonus += 5;
            return true;
        }

        public bool IsSorted(int index) => _sorted.Contains(index);
        public bool IsCleanSweep(int total) => total > 0 && SortedCount == total && FirstTryCount == total;
    }
}
