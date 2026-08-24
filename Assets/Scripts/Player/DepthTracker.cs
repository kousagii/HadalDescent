using UnityEngine;

/// <summary>
/// Tracks the submarine's current display depth (metres) within the loaded zone.
///
/// How it works:
///   - Each zone scene has Y = 0 at the top (zone entrance) and Y = -playableDepth at the bottom.
///   - This script reads the submarine's world Y and linearly remaps it to the zone's
///     display depth range (e.g. 1 000 m -> 4 000 m for the Midnight Zone).
///   - The remapped value is what appears on the player's HUD.
///
/// Setup:
///   Attach to the Submarine GameObject (same object as PlayerMovement).
///   ZoneManager.CurrentZoneIndex must be set before this script reads depth.
/// </summary>
public class DepthTracker : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Display depth in metres as shown on the HUD.</summary>
    public float DisplayDepthMetres { get; private set; }

    /// <summary>Alias for DisplayDepthMetres.</summary>
    public float CurrentDepth => DisplayDepthMetres;

    /// <summary>0.0 = zone top, 1.0 = zone bottom.</summary>
    public float ZoneProgress { get; private set; }

    /// <summary>True when submarine is within 10 units of the bottom boundary.</summary>
    public bool NearBottomBoundary => ZoneProgress >= 0.95f;

    /// <summary>True when submarine is within 10 units of the top boundary.</summary>
    public bool NearTopBoundary => ZoneProgress <= 0.05f;

    // -----------------------------------------------------------------------
    // Boundaries (set by ZoneManager after scene load)
    // -----------------------------------------------------------------------

    /// <summary>World Y of the zone entrance (top). Default = 0.</summary>
    public float ZoneTopY    { get; set; } = 0f;

    /// <summary>World Y of the zone exit (bottom). Default = -playableDepth.</summary>
    public float ZoneBottomY { get; set; } = -150f;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Update()
    {
        RecalculateDepth();
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void RecalculateDepth()
    {
        int zoneIndex = ZoneManager.CurrentZoneIndex;

        if (!ZoneConfig.IsValidZone(zoneIndex))
        {
            DisplayDepthMetres = 0f;
            ZoneProgress       = 0f;
            return;
        }

        ZoneDefinition zone = ZoneConfig.Zones[zoneIndex];

        float t = Mathf.InverseLerp(ZoneTopY, ZoneBottomY, transform.position.y);
        t = Mathf.Clamp01(t);

        ZoneProgress       = t;
        DisplayDepthMetres = Mathf.Lerp(zone.displayDepthMin, zone.displayDepthMax, t);
    }
}
