using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minigame 2 � Reconstruction Scan (sliding tile puzzle).
///
/// Grid sizes: 3x3 for Sunlight/Twilight, 4x4 for Midnight/Abyss/Hadal.
/// Tiles: numbered placeholders (1..N-1 + empty) until species photos are added.
/// When speciesData.photo != null, each tile shows a sprite slice of the photo.
///
/// Mechanic:
///   - One tile is the empty slot (displayed as dark cell).
///   - Tapping a tile adjacent to the empty slot slides it into the empty slot.
///   - Puzzle is solved when all tiles are in 1..N-1 order, empty at last position.
///   - Timer counts down; at 50% remaining a brief hint (solved image overlay) appears.
///
/// Called by MinigameManager. Do not add to scene manually.
/// </summary>
public class ReconstructionScanMinigame : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Difficulty tables (indexed by zoneIndex 0�4)
    // -----------------------------------------------------------------------

    private static readonly int[]   GridSizes = { 3, 3, 4, 4, 4 };
    private static readonly float[] Timers    = { 60f, 50f, 60f, 50f, 40f };
    private static readonly float[] HintAt    = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }; // fraction of timer

    // -----------------------------------------------------------------------
    // Runtime params
    // -----------------------------------------------------------------------

    private SpeciesData _data;
    private int         _zoneIndex;
    private Action      _onSuccess;
    private Action      _onFail;

    // -----------------------------------------------------------------------
    // Puzzle state
    // -----------------------------------------------------------------------

    private int   _n;              // grid dimension (3 or 4)
    private int[] _tiles;          // _tiles[i] = value at position i (0 = empty)
    private int   _emptyIdx;       // current index of the empty slot
    private bool  _finished;
    private bool  _hintShown;

    // -----------------------------------------------------------------------
    // UI references
    // -----------------------------------------------------------------------

    private RectTransform _rootPanel;
    private Button[]      _tileButtons;
    private TMP_Text[]    _tileTMP;
    private Image[]       _tileImages;
    private TMP_Text      _timerText;
    private Image         _timerFill;
    private TMP_Text      _resultText;
    private GameObject    _hintOverlay;
    private Image         _hintImage;

    // Species photo slices (cropped with minimum cutoff to 1:1 square, then sliced into N x N grid)
    private Sprite[]      _tileSlices;
    private Sprite        _fullCroppedSprite;

    private float _timeRemaining;
    private float _totalTime;   
    private float _cellSize;

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

    public void Initialize(SpeciesData data, int zoneIndex, Action onSuccess, Action onFail)
    {
        _data      = data;
        _zoneIndex = Mathf.Clamp(zoneIndex, 0, 4);
        _onSuccess = onSuccess;
        _onFail    = onFail;
    }

    public void Show()
    {
        StopAllCoroutines();
        _n             = GridSizes[_zoneIndex];
        _totalTime     = Timers[_zoneIndex];
        _timeRemaining = _totalTime;
        _hintShown     = false;
        _finished      = false;

        PrepareSpeciesSlices();
        BuildUI();
        InitPuzzle();
        if (_resultText != null) _resultText.gameObject.SetActive(false);
        if (_rootPanel != null)
        {
            _rootPanel.gameObject.SetActive(true);
            _rootPanel.SetAsLastSibling();
        }
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_finished || _rootPanel == null || !_rootPanel.gameObject.activeSelf) return;

        _timeRemaining -= Time.unscaledDeltaTime;

        // Update timer UI
        if (_timerText != null) _timerText.text = Mathf.CeilToInt(Mathf.Max(0f, _timeRemaining)).ToString();
        if (_timerFill != null) _timerFill.fillAmount = _timeRemaining / _totalTime;

        // Hint
        if (!_hintShown && _timeRemaining <= _totalTime * HintAt[_zoneIndex])
        {
            _hintShown = true;
            StartCoroutine(ShowHint());
        }

        if (_timeRemaining <= 0f) Finish(false);
    }

    // -----------------------------------------------------------------------
    // Puzzle logic
    // -----------------------------------------------------------------------

    private void InitPuzzle()
    {
        int total = _n * _n;
        _tiles    = new int[total];

        // Build solved state: 1..(total-1), 0 at end
        for (int i = 0; i < total - 1; i++) _tiles[i] = i + 1;
        _tiles[total - 1] = 0;
        _emptyIdx         = total - 1;

        // Shuffle by executing random valid moves
        System.Random rng = new System.Random();
        for (int s = 0; s < total * 40; s++)
        {
            int[] neighbors = GetNeighbors(_emptyIdx);
            int pick = neighbors[rng.Next(neighbors.Length)];
            SwapTiles(pick, _emptyIdx);
        }

        RefreshTileDisplay();
    }

    private void OnTileTapped(int idx)
    {
        if (_finished) return;
        if (!IsAdjacentToEmpty(idx)) return;
        SwapTiles(idx, _emptyIdx);
        RefreshTileDisplay();
        if (IsSolved()) Finish(true);
    }

    private void SwapTiles(int a, int b)
    {
        int tmp = _tiles[a]; _tiles[a] = _tiles[b]; _tiles[b] = tmp;
        if (_tiles[a] == 0) _emptyIdx = a;
        else if (_tiles[b] == 0) _emptyIdx = b;
    }

    private bool IsAdjacentToEmpty(int idx)
    {
        int row = idx / _n, col = idx % _n;
        int er  = _emptyIdx / _n, ec = _emptyIdx % _n;
        return (row == er && Mathf.Abs(col - ec) == 1) ||
               (col == ec && Mathf.Abs(row - er) == 1);
    }

    private int[] GetNeighbors(int idx)
    {
        var list = new System.Collections.Generic.List<int>();
        int r = idx / _n, c = idx % _n;
        if (r > 0)      list.Add(idx - _n);
        if (r < _n - 1) list.Add(idx + _n);
        if (c > 0)      list.Add(idx - 1);
        if (c < _n - 1) list.Add(idx + 1);
        return list.ToArray();
    }

    private bool IsSolved()
    {
        int total = _n * _n;
        for (int i = 0; i < total - 1; i++)
            if (_tiles[i] != i + 1) return false;
        return _tiles[total - 1] == 0;
    }

    // -----------------------------------------------------------------------
    // Hint (shows solved overlay briefly)
    // -----------------------------------------------------------------------

    private void OnDestroy()
    {
        CleanupSlices();
    }

    private void PrepareSpeciesSlices()
    {
        CleanupSlices();

        Sprite source = _data != null
            ? (_data.photo != null ? _data.photo : _data.fullImage)
            : null;

        if (source == null || source.texture == null) return;

        int total = _n * _n;
        _tileSlices = new Sprite[total];

        // Crop source to a square with minimum cutoff (Aspect Fill / Center Crop)
        Rect r = source.packed ? source.textureRect : source.rect;
        float w = r.width;
        float h = r.height;
        float size = Mathf.Min(w, h);
        float cropX = r.x + (w - size) * 0.5f;
        float cropY = r.y + (h - size) * 0.5f;
        Rect cropRect = new Rect(cropX, cropY, size, size);

        _fullCroppedSprite = Sprite.Create(source.texture, cropRect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);

        // Slice into N x N grid of sprites
        float tileSize = size / _n;
        for (int row = 0; row < _n; row++)
        {
            for (int col = 0; col < _n; col++)
            {
                int idx = row * _n + col;
                // Texture Y starts from bottom (0) to top, so row 0 is top row
                float tileX = cropX + col * tileSize;
                float tileY = cropY + (_n - 1 - row) * tileSize;
                Rect tileRect = new Rect(tileX, tileY, tileSize, tileSize);
                _tileSlices[idx] = Sprite.Create(source.texture, tileRect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            }
        }
    }

    private void CleanupSlices()
    {
        if (_tileSlices != null)
        {
            for (int i = 0; i < _tileSlices.Length; i++)
            {
                if (_tileSlices[i] != null) Destroy(_tileSlices[i]);
            }
            _tileSlices = null;
        }
        if (_fullCroppedSprite != null)
        {
            Destroy(_fullCroppedSprite);
            _fullCroppedSprite = null;
        }
    }

    private IEnumerator ShowHint()
    {
        if (_hintOverlay != null)
        {
            if (_hintImage != null && _fullCroppedSprite != null)
            {
                _hintImage.sprite = _fullCroppedSprite;
            }
            _hintOverlay.SetActive(true);
        }
        yield return new WaitForSecondsRealtime(2f);
        if (_hintOverlay != null) _hintOverlay.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Win / Fail
    // -----------------------------------------------------------------------

    private void Finish(bool success)
    {
        if (_finished) return;
        _finished = true;

        if (success)
        {
            // Reveal the final missing piece in the empty slot to complete the photo!
            if (_tileImages != null && _emptyIdx >= 0 && _emptyIdx < _tileImages.Length &&
                _tileSlices != null && _tileSlices.Length == _tiles.Length)
            {
                _tileImages[_emptyIdx].sprite = _tileSlices[_tiles.Length - 1];
                _tileImages[_emptyIdx].color  = Color.white;
            }
            if (_tileTMP != null && _emptyIdx >= 0 && _emptyIdx < _tileTMP.Length)
            {
                _tileTMP[_emptyIdx].enabled = false;
            }
        }

        if (_resultText != null)
        {
            _resultText.gameObject.SetActive(true);
            string speciesName = _data != null && !string.IsNullOrEmpty(_data.commonName)
                ? _data.commonName.ToUpper()
                : "SPECIES";
            _resultText.text  = success
                ? $"<b>SCAN COMPLETE!\n<size=24><color=#ffffff>{speciesName} RECONSTRUCTED</color></size></b>"
                : "<b>TARGET LOST</b>";
            _resultText.color = success
                ? new Color(0.20f, 0.90f, 0.78f)
                : new Color(1f, 0.30f, 0.30f);
        }

        StartCoroutine(DelayedClose(success));
    }

    private IEnumerator DelayedClose(bool success)
    {
        yield return new WaitForSecondsRealtime(1.3f);
        if (_rootPanel != null) _rootPanel.gameObject.SetActive(false);
        if (success) _onSuccess?.Invoke();
        else         _onFail?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Tile display refresh
    // -----------------------------------------------------------------------

    private void RefreshTileDisplay()
    {
        if (_tileButtons == null) return;
        bool hasSlices = _tileSlices != null && _tileSlices.Length == _tiles.Length;

        for (int i = 0; i < _tiles.Length; i++)
        {
            bool isEmpty = _tiles[i] == 0;
            if (_tileImages != null && i < _tileImages.Length)
            {
                if (isEmpty)
                {
                    _tileImages[i].sprite = null;
                    _tileImages[i].color  = new Color(0.02f, 0.05f, 0.08f, 0.95f);
                }
                else if (hasSlices)
                {
                    int sliceIdx = _tiles[i] - 1;
                    _tileImages[i].sprite = _tileSlices[sliceIdx];
                    _tileImages[i].color  = Color.white;
                }
                else
                {
                    _tileImages[i].sprite = null;
                    _tileImages[i].color  = new Color(0.12f, 0.25f, 0.40f, 1f);
                }
            }

            if (_tileTMP != null && i < _tileTMP.Length)
            {
                if (isEmpty)
                {
                    _tileTMP[i].text    = "";
                    _tileTMP[i].enabled = false;
                }
                else
                {
                    _tileTMP[i].text    = _tiles[i].ToString();
                    _tileTMP[i].enabled = true;
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // UI construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        if (_rootPanel != null)
        {
            Destroy(_rootPanel.gameObject);
            _rootPanel = null;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Root panel - transparent background so exploration HUD and 3D scene remain completely visible (just like in the first pic)
        var rootGO = new GameObject("ReconMinigame", typeof(RectTransform), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootPanel = rootGO.GetComponent<RectTransform>();
        _rootPanel.anchorMin = Vector2.zero;
        _rootPanel.anchorMax = Vector2.one;
        _rootPanel.sizeDelta = Vector2.zero;
        rootGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.0f); // Transparent so HUD is never hidden or dimmed
        _rootPanel.SetAsLastSibling();

        var font = UIThemeManager.AlohaFont;

        // Top Header Border Frame - Glowing cyan outline border (responsive width, clamped to canvas)
        var canvasRect = canvas.GetComponent<RectTransform>();
        float canvasW  = canvasRect != null ? canvasRect.rect.width : 1920f;
        float headerW  = Mathf.Min(1120f, canvasW * 0.94f);

        var borderFrameGO = new GameObject("HeaderBorderFrame", typeof(RectTransform), typeof(Image));
        borderFrameGO.transform.SetParent(_rootPanel, false);
        var borderRect = borderFrameGO.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 1.0f);
        borderRect.anchorMax = new Vector2(0.5f, 1.0f);
        borderRect.pivot     = new Vector2(0.5f, 1.0f);
        borderRect.anchoredPosition = new Vector2(0f, -50f);
        borderRect.sizeDelta = new Vector2(headerW, 136f);
        var borderImg = borderFrameGO.GetComponent<Image>();
        borderImg.color = new Color(0.15f, 0.75f, 0.95f, 0.95f); // Glowing cyan border
        borderImg.raycastTarget = false;

        // Inner Dark Header Card (inset 3px inside border frame) - Deep dark oceanic slate like minigame 3
        var headerCardGO = new GameObject("HeaderCard", typeof(RectTransform), typeof(Image));
        headerCardGO.transform.SetParent(borderFrameGO.transform, false);
        var headerRect = headerCardGO.GetComponent<RectTransform>();
        headerRect.anchorMin = Vector2.zero;
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = new Vector2(3f, 3f);
        headerRect.offsetMax = new Vector2(-3f, -3f);
        var headerCardImg = headerCardGO.GetComponent<Image>();
        headerCardImg.color = new Color(0.01f, 0.03f, 0.06f, 0.98f); // Deep dark oceanic slate (like minigame 3 status banner)
        headerCardImg.raycastTarget = false;

        // Title & Brief Instruction inside Header Box (formatted like minigame 1 with auto-sizing to strictly prevent overflow)
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(headerCardGO.transform, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.03f, 0.44f);
        titleRect.anchorMax = new Vector2(0.97f, 0.94f);
        titleRect.sizeDelta = Vector2.zero;
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTmp.font = font;
        titleTmp.text = "RECONSTRUCTION SCAN - ARRANGE THE TILES";
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 18f;
        titleTmp.fontSizeMax = 30f;
        titleTmp.fontStyle = FontStyles.Normal;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
        titleTmp.overflowMode = TextOverflowModes.Ellipsis;

        // Timer strip inside Header Box
        var timerBG = new GameObject("TimerBG", typeof(RectTransform), typeof(Image));
        timerBG.transform.SetParent(headerCardGO.transform, false);
        var tbrect = timerBG.GetComponent<RectTransform>();
        tbrect.anchorMin = new Vector2(0.05f, 0.12f);
        tbrect.anchorMax = new Vector2(0.95f, 0.38f);
        tbrect.sizeDelta = Vector2.zero;
        timerBG.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.09f, 0.95f);

        var timerFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        timerFillGO.transform.SetParent(timerBG.transform, false);
        var tfrect = timerFillGO.GetComponent<RectTransform>();
        tfrect.anchorMin = Vector2.zero;
        tfrect.anchorMax = Vector2.one;
        tfrect.sizeDelta = Vector2.zero;
        _timerFill = timerFillGO.GetComponent<Image>();
        _timerFill.type = Image.Type.Filled;
        _timerFill.fillMethod = Image.FillMethod.Horizontal;
        _timerFill.color = new Color(0.20f, 0.75f, 0.90f);

        var timerLblGO = new GameObject("TimerLbl", typeof(RectTransform));
        timerLblGO.transform.SetParent(timerBG.transform, false);
        var tlrect = timerLblGO.GetComponent<RectTransform>();
        tlrect.anchorMin = Vector2.zero;
        tlrect.anchorMax = Vector2.one;
        tlrect.sizeDelta = Vector2.zero;
        _timerText = timerLblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _timerText.font = font;
        _timerText.text = _totalTime.ToString("0");
        _timerText.fontSize = 26;
        _timerText.color = Color.white;
        _timerText.alignment = TextAlignmentOptions.Center;
        _timerText.fontStyle = FontStyles.Bold;

        // Grid container
        float canvasH  = canvasRect != null ? canvasRect.rect.height : 1080f;
        
        // Header banner bottom edge is at Y = -186 from canvas top.
        // Available vertical space is canvasH - 186f.
        float availableH = canvasH - 186f;
        float maxGridH = availableH - 60f;
        float gridAreaH = Mathf.Clamp(canvasH * 0.48f, 320f, Mathf.Min(maxGridH, 440f));
        float gridAreaW = gridAreaH;
        _cellSize = gridAreaW / _n - 4f;

        // Perfectly centered in the playable screen space below the header banner
        float midY = -93f;

        var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(Image));
        gridGO.transform.SetParent(_rootPanel, false);
        var gridRect = gridGO.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot     = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, midY);
        gridRect.sizeDelta = new Vector2(gridAreaW, gridAreaH);
        gridGO.GetComponent<Image>().color = new Color(0.01f, 0.03f, 0.06f, 0.92f); // Dark backing plate so seams look high-tech

        int total = _n * _n;
        _tileButtons = new Button[total];
        _tileTMP     = new TMP_Text[total];
        _tileImages  = new Image[total];

        float gap = 4f;
        float sz  = (gridAreaW - gap * (_n + 1)) / _n;

        for (int i = 0; i < total; i++)
        {
            int row = i / _n, col = i % _n;
            float x = gap + col * (sz + gap) + sz * 0.5f - gridAreaW * 0.5f;
            float y = gridAreaH - (gap + row * (sz + gap) + sz * 0.5f);

            var tileGO = new GameObject($"Tile{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            tileGO.transform.SetParent(gridGO.transform, false);
            var tr = tileGO.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.5f); tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(sz, sz);
            tr.anchoredPosition = new Vector2(x, y - gridAreaH * 0.5f);

            _tileImages[i] = tileGO.GetComponent<Image>();
            _tileImages[i].color = new Color(0.12f, 0.25f, 0.40f, 1f);

            var numGO = new GameObject("Num", typeof(RectTransform));
            numGO.transform.SetParent(tileGO.transform, false);
            var nr = numGO.GetComponent<RectTransform>();
            bool hasSlices = _tileSlices != null && _tileSlices.Length == total;
            if (hasSlices)
            {
                nr.anchorMin = new Vector2(0.06f, 0.58f);
                nr.anchorMax = new Vector2(0.42f, 0.94f);
                nr.sizeDelta = Vector2.zero;
            }
            else
            {
                nr.anchorMin = Vector2.zero;
                nr.anchorMax = Vector2.one;
                nr.sizeDelta = Vector2.zero;
            }

            _tileTMP[i]           = numGO.AddComponent<TextMeshProUGUI>();
            if (font != null) _tileTMP[i].font = font;
            _tileTMP[i].fontSize  = hasSlices ? Mathf.Max(16f, sz * 0.22f) : Mathf.Max(36f, sz * 0.38f);
            _tileTMP[i].fontStyle = FontStyles.Bold;
            _tileTMP[i].color     = hasSlices ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.80f, 0.92f, 1f);
            _tileTMP[i].alignment = hasSlices ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
            _tileTMP[i].outlineColor = new Color(0f, 0f, 0f, 0.85f);
            _tileTMP[i].outlineWidth = 0.25f;

            int captured = i;
            _tileButtons[i] = tileGO.GetComponent<Button>();
            _tileButtons[i].onClick.AddListener(() => OnTileTapped(captured));
        }

        // Hint overlay (shows solved photo schematic briefly)
        var hintGO = new GameObject("HintOverlay", typeof(RectTransform), typeof(Image));
        hintGO.transform.SetParent(gridGO.transform, false);
        var hintRect = hintGO.GetComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.sizeDelta = Vector2.zero;
        _hintImage = hintGO.GetComponent<Image>();
        _hintImage.color = Color.white;
        if (_fullCroppedSprite != null)
        {
            _hintImage.sprite = _fullCroppedSprite;
        }

        var hintStrip = new GameObject("HintStrip", typeof(RectTransform), typeof(Image));
        hintStrip.transform.SetParent(hintGO.transform, false);
        var hsRect = hintStrip.GetComponent<RectTransform>();
        hsRect.anchorMin = new Vector2(0f, 0f);
        hsRect.anchorMax = new Vector2(1f, 0f);
        hsRect.pivot     = new Vector2(0.5f, 0f);
        hsRect.sizeDelta = new Vector2(0f, 44f);
        hintStrip.GetComponent<Image>().color = new Color(0.01f, 0.04f, 0.08f, 0.92f);

        var hintTmp = new GameObject("HintText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        hintTmp.transform.SetParent(hintStrip.transform, false);
        ((RectTransform)hintTmp.transform).anchorMin = Vector2.zero;
        ((RectTransform)hintTmp.transform).anchorMax = Vector2.one;
        ((RectTransform)hintTmp.transform).sizeDelta = Vector2.zero;
        if (font != null) hintTmp.font = font;
        hintTmp.text = "PREVIEW";
        hintTmp.fontSize = 24;
        hintTmp.fontStyle = FontStyles.Bold;
        hintTmp.color = Color.white;
        hintTmp.alignment = TextAlignmentOptions.Center;

        _hintOverlay = hintGO;
        hintGO.SetActive(false);

        // Result text
        var resultGO = new GameObject("Result", typeof(RectTransform));
        resultGO.transform.SetParent(_rootPanel, false);
        var rr = resultGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot     = new Vector2(0.5f, 0.5f);
        rr.anchoredPosition = new Vector2(0f, midY);
        rr.sizeDelta = new Vector2(gridAreaW + 100f, 120f);
        _resultText = resultGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _resultText.font = font;
        _resultText.fontSize = 36;
        _resultText.fontStyle = FontStyles.Bold;
        _resultText.alignment = TextAlignmentOptions.Center;
        _resultText.gameObject.SetActive(false);

        _rootPanel.gameObject.SetActive(false);
    }
}
