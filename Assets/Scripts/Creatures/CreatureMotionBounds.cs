using UnityEngine;

/// <summary>Stable depth projection, including terrain with no room inside the preferred depth band.</summary>
public static class CreatureMotionBounds
{
    public static float ClampDepth(float proposedY, float minimumY, float maximumY)
    {
        // Terrain clearance wins over a preferred habitat limit when a hill
        // leaves no room in that band. The water ceiling remains absolute.
        // A single feasible interval avoids alternating between floor and ceiling.
        float safeMaximum = Mathf.Min(-3f, Mathf.Max(minimumY, maximumY));
        return Mathf.Clamp(proposedY, Mathf.Min(minimumY, safeMaximum), safeMaximum);
    }
}
