using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Mouse/touch drag ownership and snap feedback for one recovered item.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class CleanupSortingItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public EnvironmentalCleanupMinigame Owner { get; private set; }
    public int ItemIndex { get; private set; }
    private RectTransform _rect;
    private RectTransform _dragLayer;
    private Transform _home;
    private CanvasGroup _group;
    private Vector2 _offset;
    private int _pointerId;
    private bool _dragging;
    private bool _accepted;
    private ScrollRect _trayScroll;
    private bool _scrolling;
    private PointerEventData _scrollEvent;
    public bool IsBeingDragged => _dragging && !_accepted;
    public bool IsDraggedBy(int pointerId) => IsBeingDragged && pointerId == _pointerId;

    public void Initialize(EnvironmentalCleanupMinigame owner, int index, RectTransform dragLayer)
    {
        Owner = owner;
        ItemIndex = index;
        _rect = (RectTransform)transform;
        _home = transform.parent;
        _dragLayer = dragLayer;
        _group = GetComponent<CanvasGroup>();
        _trayScroll = GetComponentInParent<ScrollRect>();
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (_dragging || _scrolling || _accepted || Owner == null || !Owner.CanSort || data.button != PointerEventData.InputButton.Left) return;
        _pointerId = data.pointerId;
        // Vertical swipes browse a long tray; a sideways drag picks up the item.
        if (_trayScroll != null && _trayScroll.content.rect.height > _trayScroll.viewport.rect.height + 1f &&
            Mathf.Abs(data.position.y - data.pressPosition.y) > Mathf.Abs(data.position.x - data.pressPosition.x))
        {
            _scrolling = true;
            _scrollEvent = data;
            _trayScroll.OnBeginDrag(data);
            return;
        }
        _trayScroll?.StopMovement();
        _dragging = true;
        Vector2 size = _rect.rect.size;
        Vector3 position = _rect.position;
        _rect.SetParent(_dragLayer, true);
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = size;
        _rect.position = position;
        _rect.SetAsLastSibling();
        _rect.localScale = Vector3.one * 1.06f;
        _group.blocksRaycasts = false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, data.position, data.pressEventCamera, out var point);
        _offset = (Vector2)_rect.localPosition - point;
    }

    public void OnDrag(PointerEventData data)
    {
        if (_scrolling && data.pointerId == _pointerId)
        {
            if (!Owner.CanSort) { FinishScrolling(data); _trayScroll.StopMovement(); return; }
            _trayScroll.OnDrag(data);
            return;
        }
        if (!_dragging || data.pointerId != _pointerId) return;
        if (!Owner.CanSort) { ReturnToTray(); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, data.position, data.pressEventCamera, out var point);
        _rect.localPosition = point + _offset;
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (_scrolling && data.pointerId == _pointerId) { FinishScrolling(data); return; }
        if (!_dragging || data.pointerId != _pointerId) return;
        _dragging = false;
        if (!_accepted) ReturnToTray();
    }

    public void Accept(RectTransform bin)
    {
        _accepted = true;
        _dragging = false;
        _group.blocksRaycasts = false;
        // Remove the empty slot so the remaining collected items close the gap.
        if (_home != null) _home.gameObject.SetActive(false);
        StartCoroutine(SnapIntoBin(bin));
    }

    private IEnumerator SnapIntoBin(RectTransform bin)
    {
        Vector3 start = _rect.position;
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            if (Owner == null || !Owner.CanSort) { yield return null; continue; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.25f);
            _rect.position = Vector3.Lerp(start, bin.position, t);
            _rect.localScale = Vector3.one * Mathf.Lerp(1.06f, 0.1f, t);
            _group.alpha = 1f - t;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_scrolling && (Owner == null || !Owner.CanSort))
        {
            FinishScrolling(_scrollEvent);
            if (_trayScroll != null) _trayScroll.StopMovement();
        }
        if (_dragging && (Owner == null || !Owner.CanSort)) ReturnToTray();
    }

    private void OnDisable()
    {
        // Closing the panel also closes its drag layer. Avoid reparenting during OnDisable.
        _dragging = false;
        if (_scrolling) FinishScrolling(_scrollEvent);
    }

    private void FinishScrolling(PointerEventData data)
    {
        if (_trayScroll != null && data != null) _trayScroll.OnEndDrag(data);
        _scrolling = false;
        _scrollEvent = null;
    }

    private void ReturnToTray()
    {
        _dragging = false;
        if (_home == null) return;
        _rect.SetParent(_home, false);
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.sizeDelta = Vector2.zero;
        _rect.localPosition = Vector3.zero;
        _rect.localScale = Vector3.one;
        _group.blocksRaycasts = true;
    }
}
