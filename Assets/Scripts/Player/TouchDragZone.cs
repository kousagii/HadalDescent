using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Captures finger-drag delta from a UI RectTransform panel and exposes it
/// as TouchDelta for SubmarineCamera (and any other consumer).
///
/// The delta is valid for exactly one frame: it is set during OnDrag and
/// cleared in LateUpdate (after SubmarineCamera has consumed it in LateUpdate
/// at the same or earlier sort order — ensure SubmarineCamera runs AFTER this).
/// </summary>
public class TouchDragZone : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Sensitivity")]
    [SerializeField] private float sensitivity = 0.2f;

    /// <summary>Pixel-space drag movement this frame, scaled by sensitivity.</summary>
    public Vector2 TouchDelta { get; private set; }

    private Vector2 _accumulatedDelta;
    private bool _isDragging;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging       = true;
        _accumulatedDelta = Vector2.zero;
        TouchDelta        = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // eventData.delta is in screen pixels; multiply by sensitivity
        _accumulatedDelta += eventData.delta * sensitivity;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging       = false;
        _accumulatedDelta = Vector2.zero;
        TouchDelta        = Vector2.zero;
    }

    private void Update()
    {
        // Expose accumulated drag delta throughout the frame for SubmarineCamera
        TouchDelta        = _accumulatedDelta;
        _accumulatedDelta = Vector2.zero;
    }
}