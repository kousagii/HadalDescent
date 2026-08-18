using UnityEngine;

/// <summary>
/// Adds a pulsing cyan emission highlight to a species GameObject when it is
/// inside the scanner reticle. Works with Standard-shader primitives and any
/// material that has an _EmissionColor property.
///
/// Usage (called by ScannerSystem):
///   SpeciesHighlighter.SetHighlight(gameObject, true);   // when targeted
///   SpeciesHighlighter.SetHighlight(gameObject, false);  // when lost / scan done
/// </summary>
public class SpeciesHighlighter : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Static colour palette
    // -----------------------------------------------------------------------
    private static readonly Color HighlightColor = new Color(0.0f, 0.95f, 0.90f);  // cyan-teal
    private static readonly Color IdleEmission   = Color.black;

    // -----------------------------------------------------------------------
    // Per-instance state
    // -----------------------------------------------------------------------
    private Renderer[] _renderers;
    private bool        _active;
    private float       _pulseTime;

    [Tooltip("Emission intensity multiplier at peak of pulse.")]
    [SerializeField] private float peakIntensity = 2.2f;
    [Tooltip("Cycles per second of the pulse.")]
    [SerializeField] private float pulseFrequency = 2.0f;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in _renderers)
            r.material.EnableKeyword("_EMISSION");
    }

    private void Update()
    {
        if (!_active) return;
        _pulseTime += Time.deltaTime * pulseFrequency * Mathf.PI * 2f;
        float t          = (Mathf.Sin(_pulseTime) + 1f) * 0.5f;   // 0 ? 1
        Color emitColor  = HighlightColor * Mathf.Lerp(0.4f, peakIntensity, t);
        foreach (var r in _renderers)
            r.material.SetColor("_EmissionColor", emitColor);
    }

    // -----------------------------------------------------------------------
    // Static helper — ScannerSystem calls this
    // -----------------------------------------------------------------------

    /// <summary>Enable or disable the cyan emission highlight on a species object.</summary>
    public static void SetHighlight(GameObject go, bool on)
    {
        if (go == null) return;
        var hl = go.GetComponent<SpeciesHighlighter>() ?? go.AddComponent<SpeciesHighlighter>();

        hl._active    = on;
        hl._pulseTime = 0f;

        if (!on)
        {
            foreach (var r in hl._renderers)
                r.material.SetColor("_EmissionColor", IdleEmission);
        }
    }
}
