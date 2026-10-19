using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the two-column collected tray readable as its viewport changes size.</summary>
public class CleanupSortingTrayUI : MonoBehaviour
{
    private ScrollRect _scroll;
    private GridLayoutGroup _grid;
    private float _previousWidth = -1f;
    private EnvironmentalCleanupMinigame _owner;

    public void Initialize(EnvironmentalCleanupMinigame owner, ScrollRect scroll, GridLayoutGroup grid)
    {
        _owner = owner;
        _scroll = scroll;
        _grid = grid;
        ResizeCards();
    }

    private void OnRectTransformDimensionsChange() => ResizeCards();
    private void LateUpdate()
    {
        ResizeCards();
        if (_scroll == null) return;
        bool canSort = _owner != null && _owner.CanSort;
        if (!canSort) _scroll.StopMovement();
        if (_scroll.enabled != canSort) _scroll.enabled = canSort;
        if (_scroll.verticalScrollbar != null) _scroll.verticalScrollbar.interactable = canSort;
    }

    private void ResizeCards()
    {
        if (_scroll == null || _grid == null || _scroll.viewport == null) return;
        float width = _scroll.viewport.rect.width;
        if (width <= 1f || Mathf.Abs(width - _previousWidth) < 0.5f) return;
        _previousWidth = width;
        float cardWidth = Mathf.Max(1f, (width - _grid.padding.horizontal - _grid.spacing.x) * 0.5f);
        _grid.cellSize = new Vector2(cardWidth, Mathf.Clamp(cardWidth * 1.1f, 150f, 240f));
    }
}
