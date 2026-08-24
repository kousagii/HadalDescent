using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any world entity (Species, Debris, Hazard, Clam Shell, Environment)
/// to make it detectable and trackable by the SonarMapUI.
///
/// Features:
///   - Static registry (AllTrackables) for zero-allocation, high-performance sonar queries.
///   - Categorized by SonarTargetType for distinct blip colors, icons, and priority.
///   - Tracks discovery state for species.
/// </summary>
public class SonarTrackable : MonoBehaviour
{
    public enum SonarTargetType
    {
        Species,        // Marine creatures (mobile or stationary)
        Debris,         // Underwater trash / cleanup minigame targets
        Hazard,         // Thermal vents, falling rocks, current surges
        Collectible,    // Clam shells, hidden data pods, oxygen bubbles
        Environment     // Significant seabed landmarks or educational props
    }

    // -----------------------------------------------------------------------
    // Static Registry (fast iteration, no FindObjectsByType)
    // -----------------------------------------------------------------------

    private static readonly List<SonarTrackable> _allTrackables = new List<SonarTrackable>();
    public static IReadOnlyList<SonarTrackable> AllTrackables => _allTrackables;

    // -----------------------------------------------------------------------
    // Inspector & State
    // -----------------------------------------------------------------------

    [Header("Sonar Classification")]
    [SerializeField] private SonarTargetType targetType = SonarTargetType.Species;
    [SerializeField] private string targetName = "Unknown Contact";

    [Header("Visual Overrides (Optional)")]
    [Tooltip("Optional custom sprite for the radar blip.")]
    [SerializeField] private Sprite customBlipIcon;
    [Tooltip("Override color if not using default category color. Set alpha > 0 to use.")]
    [SerializeField] private Color customBlipColor = Color.clear;

    [Header("State")]
    [SerializeField] private bool isDiscovered = false;

    // -----------------------------------------------------------------------
    // Public Properties
    // -----------------------------------------------------------------------

    public SonarTargetType TargetType => targetType;
    public string TargetName          => targetName;
    public Sprite CustomBlipIcon      => customBlipIcon;
    public Color CustomBlipColor      => customBlipColor;
    public bool IsDiscovered
    {
        get => isDiscovered;
        set => isDiscovered = value;
    }

    public Vector3 WorldPosition => transform.position;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        Register();
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Register()
    {
        if (!_allTrackables.Contains(this))
            _allTrackables.Add(this);
    }

    private void Unregister()
    {
        _allTrackables.Remove(this);
    }

    // -----------------------------------------------------------------------
    // Initialization Helpers
    // -----------------------------------------------------------------------

    public void Initialize(SonarTargetType type, string name, bool discovered = false)
    {
        targetType   = type;
        targetName   = name;
        isDiscovered = discovered;
        Register();
    }

    public void SetDiscovered(bool discovered)
    {
        isDiscovered = discovered;
    }
}
