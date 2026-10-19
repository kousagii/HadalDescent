using System;
using System.Linq;
using Category = EnvironmentalCleanupMinigame.DebrisCategory;
using Item = EnvironmentalCleanupMinigame.DebrisItemType;

// Only the host's serialized enum definitions are supplied by the runner; production rules are compiled directly.
public static class Program
{
    private static int _checks;
    private static void Check(bool valid, string message)
    {
        _checks++;
        if (!valid) throw new Exception(message);
    }

    public static void Main()
    {
        var observed = new System.Collections.Generic.HashSet<Item>();
        var layouts = new System.Collections.Generic.HashSet<string>();
        foreach (CleanupRoundRules.Variation variation in Enum.GetValues(typeof(CleanupRoundRules.Variation)))
        for (int zone = 0; zone < 5; zone++)
        for (int seed = 0; seed < 100; seed++)
        {
            var manifest = CleanupRoundRules.BuildManifest(zone, 2, 2, 1, variation, new Random(seed));
            Check(manifest.Count == 5 + zone, "Zone item count");
            Check(manifest.All(item => Enum.IsDefined(typeof(Item), item)), "Only supported items");
            Check(manifest.Count(item => CleanupRoundRules.CategoryOf(item) == Category.Plastic) >= 2, "Cluster plastics retained");
            Check(manifest.Count(item => CleanupRoundRules.CategoryOf(item) == Category.Metal) >= 2, "Cluster metals retained");
            Check(manifest.Count(item => CleanupRoundRules.CategoryOf(item) == Category.Hazardous) >= 1, "Cluster hazards retained");
            if (variation != CleanupRoundRules.Variation.MixedCleanup)
            {
                var focus = variation == CleanupRoundRules.Variation.CurrentSweep ? Category.Plastic :
                    variation == CleanupRoundRules.Variation.HeavyRecovery ? Category.Metal : Category.Hazardous;
                int baseline = focus == Category.Hazardous ? 1 : 2;
                Check(manifest.Count(item => CleanupRoundRules.CategoryOf(item) == focus) == baseline + zone, "Focused extras");
            }
            foreach (var item in manifest) observed.Add(item);
            layouts.Add(string.Join(",", manifest));
            var repeated = CleanupRoundRules.BuildManifest(zone, 2, 2, 1, variation, new Random(seed));
            Check(manifest.SequenceEqual(repeated), "Reproducible generation");
            Check(CleanupRoundRules.SortingSeconds(manifest.Count) == 8 + 5 * manifest.Count, "Proportional sorting budget");
        }
        Check(observed.Count == 10, "All ten asset variants appear, including both pipes");
        Check(layouts.Count > 100, "Meaningfully varied selections and order");
        Check(CleanupRoundRules.BuildManifest(4, 4, 4, 3, CleanupRoundRules.Variation.MixedCleanup, new Random(1)).Count == 11, "Maximum capacity");
        Check(CleanupRoundRules.BuildManifest(-4, -1, -1, -1, CleanupRoundRules.Variation.MixedCleanup, new Random(1)).Count == 0, "Negative configuration bounded");
        Check(CleanupRoundRules.SortingSeconds(0) == 0, "No fabricated item or empty-round timer");

        var score = new CleanupRoundRules.SortingScore();
        Check(!score.Submit(2, Category.Hazardous, Category.Metal), "Wrong bin rejected");
        Check(score.SortedCount == 0 && !score.IsSorted(2), "Wrong item stays available");
        Check(score.Submit(2, Category.Hazardous, Category.Hazardous), "Correction accepted");
        Check(score.FirstTryCount == 0, "Correction cannot erase accuracy history");
        Check(score.Submit(0, Category.Plastic, Category.Plastic), "Free-order sorting");
        Check(score.Submit(1, Category.Metal, Category.Metal), "Third correct drop");
        Check(score.Bonus == 5 && score.Streak == 3, "Three-item streak bonus");
        Check(!score.Submit(1, Category.Metal, Category.Metal), "Duplicate drop rejected");
        Check(score.Bonus == 5 && score.SortedCount == 3 && score.Streak == 3, "Duplicate cannot farm rewards");
        Check(!score.IsCleanSweep(3), "Corrected run is not a clean sweep");
        Check(!score.Submit(3, Category.Metal, Category.Plastic) && score.Streak == 0, "Mistake resets streak");
        Check(score.Bonus == 5, "Existing bonuses retained");
        Check(!score.Submit(-1, Category.Metal, Category.Metal), "Invalid index rejected");
        var perfect = new CleanupRoundRules.SortingScore();
        for (int i = 0; i < 9; i++) perfect.Submit(i, Category.Plastic, Category.Plastic);
        Check(perfect.Bonus == 15 && perfect.IsCleanSweep(9), "Perfect run rewards");
        Check(!new CleanupRoundRules.SortingScore().IsCleanSweep(0), "Empty run cannot earn clean sweep");
        Check(CleanupRoundRules.TouchesFish(0f, 0f, 0f, 0f, 0.3f, 0.2f), "Direct claw/fish contact");
        Check(!CleanupRoundRules.TouchesFish(0f, 1.1f, 0f, 0f, 0f, 0f), "Claw clears fish");
        Check(CleanupRoundRules.TouchesFish(0f, 0.2f, 0.35f, 0.25f, 0f, 0f), "Cargo can touch while claw clears fish");
        Check(CleanupRoundRules.TouchesFish(0.8f, 0f, 0.3f, 0.2f, 0f, 0f), "Cargo edge contact");
        Check(!CleanupRoundRules.TouchesFish(0.9f, 0f, 0.3f, 0.2f, 0f, 0f), "Horizontal near miss");
        Check(!CleanupRoundRules.TouchesFish(0f, 0.7f, 0.3f, 0.2f, 0f, 0f), "Vertical near miss");
        Console.WriteLine($"Cleanup checks passed: {_checks} assertions; {layouts.Count} distinct manifests; ten debris variants.");
    }
}
