using UnityEngine;

/// <summary>
/// Marks a stationary scannable object (coral, sponge, bivalve, etc.) for
/// detection by ScannerSystem. No movement - just a data container + collider.
///
/// SpeciesSpawner adds this component automatically when isStationary = true.
/// The sphere trigger collider is also added by SpeciesSpawner.
/// </summary>
public class ScanTarget : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Public state (read by ScannerSystem)
    // -----------------------------------------------------------------------

    /// <summary>The species this scannable object represents in the bestiary.</summary>
    public SpeciesData Data { get; private set; }

    private bool _isDiscovered;
    /// <summary>True once the player has successfully scanned this entry.</summary>
    public bool IsDiscovered
    {
        get => _isDiscovered;
        set
        {
            _isDiscovered = value;
            var trackable = GetComponent<SonarTrackable>();
            if (trackable != null) trackable.IsDiscovered = value;
        }
    }

    // -----------------------------------------------------------------------
    // Initialization (called by SpeciesSpawner after AddComponent)
    // -----------------------------------------------------------------------

    public void Initialize(SpeciesData data)
    {
        Data = data;

        // Tag so ScannerSystem OverlapSphere can identify it quickly
        try { gameObject.tag = "Species"; }
        catch (UnityException) { /* Tag "Species" may not exist yet - add it in Tags & Layers */ }
    }
}