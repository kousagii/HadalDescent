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
        _n             = GridSizes[_zoneIndex];
        _totalTime     = Timers[_zoneIndex];
        _timeRemaining = _totalTime;
        _hintShown     = false;
        _finished      = false;

        BuildUI();
        InitPuzzle();
        if (_rootPanel != null) _rootPanel.gameObject.SetActive(true);
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (_finished || _rootPanel == null || !_rootPanel.gameObject.activeSelf) return;

        _timeRemaining -= Time.deltaTime;

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

    private IEnumerator ShowHint()
    {
        if (_hintOverlay != null) _hintOverlay.SetActive(true);
        yield return new WaitForSeconds(2f);
        if (_hintOverlay != null) _hintOverlay.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Win / Fail
    // -----------------------------------------------------------------------

    private void Finish(bool success)
    {
        if (_finished) return;
        _finished = true;

        if (_resultText != null)
        {
            _resultText.gameObject.SetActive(true);
            _resultText.text  = success ? "<b>SCAN COMPLETE!</b>" : "<b>TARGET LOST</b>";
            _resultText.color = success
                ? new Color(0.20f, 0.90f, 0.78f)
                : new Color(1f, 0.30f, 0.30f);
        }

        StartCoroutine(DelayedClose(success));
    }

    private IEnumerator DelayedClose(bool success)
    {
        yield return new WaitForSeconds(1.3f);
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
        for (int i = 0; i < _tiles.Length; i++)
        {
            bool isEmpty = _tiles[i] == 0;
            if (_tileImages != null && i < _tileImages.Length)
                _tileImages[i].color = isEmpty
                    ? new Color(0.05f, 0.07f, 0.10f, 1f)
                    : new Color(0.12f, 0.25f, 0.40f, 1f);
            if (_tileTMP != null && i < _tileTMP.Length)
            {
                _tileTMP[i].text    = isEmpty ? "" : _tiles[i].ToString();
                _tileTMP[i].enabled = !isEmpty;
            }
        }
    }

    // -----------------------------------------------------------------------
    // UI construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        if (_rootPanel != null) { RefreshTileDisplay(); return; }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Root dim
        var rootGO = new GameObject("ReconMinigame", typeof(RectTransform), typeof(Image));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootPanel = rootGO.GetComponent<RectTransform>();
        _rootPanel.anchorMin = Vector2.zero;
        _rootPanel.anchorMax = Vector2.one;
        _rootPanel.sizeDelta = Vector2.zero;
        rootGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.70f);

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(_rootPanel, false);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.0f, 0.88f);
        titleRect.anchorMax = new Vector2(1.0f, 0.97f);
        titleRect.sizeDelta = Vector2.zero;
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "RECONSTRUCTION SCAN - Arrange the tiles";
        titleTmp.fontSize = 36; titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.8f, 0.95f, 1f); titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableWordWrapping = false;
        titleTmp.overflowMode = TextOverflowModes.Overflow;

        // Timer strip
        var timerBG = new GameObject("TimerBG", typeof(RectTransform), typeof(Image));
        timerBG.transform.SetParent(_rootPanel, false);
        var tbrect = timerBG.GetComponent<RectTransform>();
        tbrect.anchorMin = new Vector2(0.1f, 0.83f); tbrect.anchorMax = new Vector2(0.9f, 0.88f); tbrect.sizeDelta = Vector2.zero;
        timerBG.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        var timerFillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        timerFillGO.transform.SetParent(timerBG.transform, false);
        var tfrect = timerFillGO.GetComponent<RectTransform>();
        tfrect.anchorMin = Vector2.zero; tfrect.anchorMax = Vector2.one; tfrect.sizeDelta = Vector2.zero;
        _timerFill = timerFillGO.GetComponent<Image>();
        _timerFill.type = Image.Type.Filled; _timerFill.fillMethod = Image.FillMethod.Horizontal;
        _timerFill.color = new Color(0.20f, 0.75f, 0.90f);

        var timerLblGO = new GameObject("TimerLbl", typeof(RectTransform));
        timerLblGO.transform.SetParent(timerBG.transform, false);
        var tlrect = timerLblGO.GetComponent<RectTransform>();
        tlrect.anchorMin = Vector2.zero; tlrect.anchorMax = Vector2.one; tlrect.sizeDelta = Vector2.zero;
        _timerText = timerLblGO.AddComponent<TextMeshProUGUI>();
        _timerText.text = _totalTime.ToString("0"); _timerText.fontSize = 13;
        _timerText.color = Color.white; _timerText.alignment = TextAlignmentOptions.Center;
        _timerText.fontStyle = FontStyles.Bold;

        // Grid container
        var canvasRect = canvas.GetComponent<RectTransform>();
        float canvasH  = canvasRect != null ? canvasRect.rect.height : 600f;
        float gridAreaH = Mathf.Min(canvasH * 0.55f, 340f);
        float gridAreaW = gridAreaH;
        _cellSize = gridAreaW / _n - 4f;

        var gridGO = new GameObject("Grid", typeof(RectTransform));
        gridGO.transform.SetParent(_rootPanel, false);
        var gridRect = gridGO.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.15f);
        gridRect.anchorMax = new Vector2(0.5f, 0.15f);
        gridRect.pivot     = new Vector2(0.5f, 0f);
        gridRect.sizeDelta = new Vector2(gridAreaW, gridAreaH);

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
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.sizeDelta = Vector2.zero;
            _tileTMP[i]           = numGO.AddComponent<TextMeshProUGUI>();
            _tileTMP[i].fontSize  = sz * 0.38f;
            _tileTMP[i].fontStyle = FontStyles.Bold;
            _tileTMP[i].color     = new Color(0.80f, 0.92f, 1f);
            _tileTMP[i].alignment = TextAlignmentOptions.Center;

            int captured = i;
            _tileButtons[i] = tileGO.GetComponent<Button>();
            _tileButtons[i].onClick.AddListener(() => OnTileTapped(captured));
        }

        // Hint overlay (shows solved order briefly)
        var hintGO = new GameObject("HintOverlay", typeof(RectTransform), typeof(Image));
        hintGO.transform.SetParent(gridGO.transform, false);
        var hintRect = hintGO.GetComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero; hintRect.anchorMax = Vector2.one; hintRect.sizeDelta = Vector2.zero;
        hintGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        var hintTmp = new GameObject("HintText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        hintTmp.transform.SetParent(hintGO.transform, false);
        ((RectTransform)hintTmp.transform).anchorMin = Vector2.zero;
        ((RectTransform)hintTmp.transform).anchorMax = Vector2.one;
        hintTmp.text = "HINT"; hintTmp.fontSize = 26; hintTmp.fontStyle = FontStyles.Bold;
        hintTmp.color = new Color(1f, 0.88f, 0.30f); hintTmp.alignment = TextAlignmentOptions.Center;
        _hintOverlay = hintGO;
        hintGO.SetActive(false);

        // Result text
        var resultGO = new GameObject("Result", typeof(RectTransform));
        resultGO.transform.SetParent(_rootPanel, false);
        var rr = resultGO.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.1f, 0.45f); rr.anchorMax = new Vector2(0.9f, 0.58f); rr.sizeDelta = Vector2.zero;
        _resultText = resultGO.AddComponent<TextMeshProUGUI>();
        _resultText.fontSize = 22; _resultText.alignment = TextAlignmentOptions.Center;
        _resultText.gameObject.SetActive(false);

        _rootPanel.gameObject.SetActive(false);
    }
}
