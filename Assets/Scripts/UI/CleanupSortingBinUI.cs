using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>Fixed, generous drop target; hover feedback never reveals the correct category.</summary>
[RequireComponent(typeof(Image))]
public class CleanupSortingBinUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private EnvironmentalCleanupMinigame _owner;
    private EnvironmentalCleanupMinigame.DebrisCategory _category;
    private Image _image;
    private Color _baseColor;
    private TMP_Text _rewardPopup;

    public void Initialize(EnvironmentalCleanupMinigame owner, EnvironmentalCleanupMinigame.DebrisCategory category)
    {
        _owner = owner;
        _category = category;
        _image = GetComponent<Image>();
        _baseColor = _image.color;
        var popup = new GameObject("RewardPopup", typeof(RectTransform), typeof(TextMeshProUGUI));
        popup.transform.SetParent(transform, false);
        var rect = (RectTransform)popup.transform;
        rect.anchorMin = new Vector2(0.02f, 0.7f);
        rect.anchorMax = new Vector2(0.98f, 0.9f);
        rect.sizeDelta = Vector2.zero;
        _rewardPopup = popup.GetComponent<TextMeshProUGUI>();
        if (UIThemeManager.AlohaFont != null) _rewardPopup.font = UIThemeManager.AlohaFont;
        _rewardPopup.text = "+15 RDP";
        _rewardPopup.fontSize = 30;
        _rewardPopup.alignment = TextAlignmentOptions.Center;
        _rewardPopup.raycastTarget = false;
        _rewardPopup.color = new Color(0.3f, 1f, 0.75f, 0f);
    }

    public void OnPointerEnter(PointerEventData data)
    {
        var item = data.pointerDrag != null ? data.pointerDrag.GetComponent<CleanupSortingItemUI>() : null;
        if (item != null && item.IsBeingDragged && item.Owner == _owner && _owner.CanSort)
            _image.color = new Color(0.1f, 0.48f, 0.52f, 1f);
    }

    public void OnPointerExit(PointerEventData data) => _image.color = _baseColor;

    public void OnDrop(PointerEventData data)
    {
        _image.color = _baseColor;
        var item = data.pointerDrag != null ? data.pointerDrag.GetComponent<CleanupSortingItemUI>() : null;
        if (item == null || !item.IsDraggedBy(data.pointerId) || item.Owner != _owner || !_owner.CanSort) return;
        if (_owner.TrySortItem(item.ItemIndex, _category))
        {
            item.Accept((RectTransform)transform);
            StopAllCoroutines();
            StartCoroutine(Pulse());
        }
    }

    private IEnumerator Pulse()
    {
        float elapsed = 0f;
        while (elapsed < 0.65f)
        {
            if (!_owner.CanSort) { yield return null; continue; }
            elapsed += Time.deltaTime;
            float pulse = Mathf.Clamp01(elapsed / 0.3f);
            transform.localScale = Vector3.one * (1f + Mathf.Sin(pulse * Mathf.PI) * 0.04f);
            float t = Mathf.Clamp01(elapsed / 0.65f);
            _rewardPopup.rectTransform.anchoredPosition = Vector2.up * (t * 40f);
            _rewardPopup.color = new Color(0.3f, 1f, 0.75f, 1f - t);
            yield return null;
        }
        transform.localScale = Vector3.one;
        _rewardPopup.color = new Color(0.3f, 1f, 0.75f, 0f);
    }
}
