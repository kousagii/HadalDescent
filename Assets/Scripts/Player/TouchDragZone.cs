using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    /// <summary>Pixel-space drag movement this frame.</summary>
    public Vector2 TouchDelta { get; private set; }

    private bool _isDragging;
    public bool IsDragging => _isDragging;

    private void Awake()
    {
        var img = GetComponent<Image>();
        if (img == null)
        {
            img = gameObject.AddComponent<Image>();
            img.color = Color.clear;
        }
        img.raycastTarget = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if ((ShopManager.Instance != null && ShopManager.Instance.IsOpen) || SubmarinePreviewRotator.IsAnyDragging) return;
        _isDragging = true;
        TouchDelta  = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if ((ShopManager.Instance != null && ShopManager.Instance.IsOpen) || SubmarinePreviewRotator.IsAnyDragging)
        {
            TouchDelta = Vector2.zero;
            return;
        }
        TouchDelta += eventData.delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
    }

    /// <summary>
    /// Consumes and resets the accumulated touch drag delta.
    /// </summary>
    public Vector2 ConsumeTouchDelta()
    {
        Vector2 d = TouchDelta;
        TouchDelta = Vector2.zero;
        return d;
    }
}