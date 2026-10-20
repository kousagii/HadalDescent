using System;
using Kind = HazardDodgeMinigame.HazardType;

/// <summary>Hazard selection and lane safety, independent of scene objects.</summary>
public static class HazardDodgeRules
{
    public static float SurgeIntervalSeconds(int zone, double roll)
        => (zone < 2 ? 120f : 60f) + (float)(Math.Max(0d, Math.Min(1d, roll)) * 60d);

    public sealed class Wave
    {
        public int OpenLane;
        public readonly Kind?[] Lanes = new Kind?[3];
        public readonly int[] CurrentDirections = new int[3];
        public readonly float[] ArrivalDelays = new float[3];
    }

    public static Kind Pick(int zone, bool ventField, double roll)
    {
        if (zone == 0)
            return roll < .30 ? Kind.CurrentSurge : roll < .85 ? Kind.RockOutcrop : Kind.SedimentCloud;
        if (zone >= 2 && ventField)
            return roll < .35 ? Kind.ThermalVent : roll < .55 ? Kind.RockOutcrop :
                roll < .70 ? Kind.FallingRock : roll < .90 ? Kind.CurrentSurge : Kind.SedimentCloud;
        return roll < .30 ? Kind.FallingRock : roll < .55 ? Kind.RockOutcrop :
            roll < .80 ? Kind.CurrentSurge : Kind.SedimentCloud;
    }

    public static Wave BuildWave(int zone, bool ventField, Random random)
    {
        var wave = new Wave { OpenLane = random.Next(3) };
        for (int lane = 0; lane < 3; lane++)
        {
            if (lane == wave.OpenLane)
            {
                if (random.NextDouble() < .35) wave.Lanes[lane] = Kind.DataPod;
            }
            else if (random.NextDouble() < .70)
                wave.Lanes[lane] = Pick(zone, ventField, random.NextDouble());
        }
        bool hasCurrent = Array.IndexOf(wave.Lanes, Kind.CurrentSurge) >= 0;
        if (hasCurrent && random.NextDouble() < .85)
        {
            // Pair most current waves with a rock farther along the destination lane.
            // The remaining outer lane is clear, and the delay allows a recovery swipe.
            Array.Clear(wave.Lanes, 0, wave.Lanes.Length);
            wave.OpenLane = random.Next(2) == 0 ? 0 : 2;
            int rockLane = 2 - wave.OpenLane;
            wave.Lanes[1] = Kind.CurrentSurge;
            wave.CurrentDirections[1] = rockLane - 1;
            wave.Lanes[rockLane] = Kind.RockOutcrop;
            wave.ArrivalDelays[rockLane] = .9f;
            if (random.NextDouble() < .35) wave.Lanes[wave.OpenLane] = Kind.DataPod;
        }
        return wave;
    }

    public static bool DamagesHull(Kind kind) => kind == Kind.RockOutcrop ||
        kind == Kind.FallingRock || kind == Kind.ThermalVent;

    public static int SafeCurrentDestination(int lane, int direction, int blockedMask)
    {
        int next = lane + Math.Sign(direction);
        if (next >= 0 && next <= 2 && (blockedMask & (1 << next)) == 0) return next;
        // A chevron must never push opposite its visible direction.
        return lane;
    }

    public static bool CrossesSubmarine(float previousZ, float nextZ, float submarineZ, float halfLength = 2f)
        => previousZ >= submarineZ - halfLength && nextZ <= submarineZ + halfLength;
}
