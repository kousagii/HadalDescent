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
    private CanvasGroup   _canvasGroup;
    private Button[]      _tileButtons;
    private TMP_Text[]    _tileTMP;
    private Image[]       _tileImages;
    private TMP_Text      _timerText;
    private Image         _timerFill;
    private GameObject    _resultBox;
    private RectTransform _resultBoxRect;
    private Image         _resultBorder;
    private Image         _resultTopStripe;
    private TMP_Text      _resultText;
    private GameObject    _hintOverlay;
    private Image         _hintImage;

    // Species photo slices (cropped with minimum cutoff to 1:1 square, then sliced into N x N grid)
    private Sprite[]      _tileSlices;
    private Sprite        _fullCroppedSprite;

    // Scanner Upgrade Perks (Tier 2: +10s, Tier 3: Move Guide, Tier 4: 1x Swap, Tier 5: 2x Swaps & +15s)
    private Image[]       _tileBorderImages;
    private int           _swapsRemaining;
    private bool          _swapModeActive;
    private int           _firstSwapIdx = -1;
    private GameObject    _swapButtonGO;
    private Button        _swapButton;
    private Image         _swapButtonBg;
    private Image         _swapButtonBorder;
    private TMP_Text      _swapButtonTMP;

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

    private int GetScannerTier()
    {
        if (GameManager.Instance == null) return 1;
        return Mathf.Clamp(GameManager.Instance.ScannerTier, 1, 5);
    }

    public void Show()
    {
        StopAllCoroutines();
        UIManager.Instance?.SetExplorationHUDVisible(false);
        _n = GridSizes[_zoneIndex];

        int tier = GetScannerTier();
        float bonusTime = (tier >= 5) ? 35f : (tier >= 3 ? 20f : (tier >= 2 ? 10f : 0f));
        _totalTime     = Timers[_zoneIndex] + bonusTime;
        _timeRemaining = _totalTime;
        _hintShown     = false;
        _finished      = false;

        _swapsRemaining = (tier >= 5) ? 2 : (tier >= 4 ? 1 : 0);
        _swapModeActive = false;
        _firstSwapIdx   = -1;

        PrepareSpeciesSlices();
        BuildUI();
        InitPuzzle();
        UpdateSwapButtonUI();
        if (_resultBox != null) _resultBox.SetActive(false);
        else if (_resultText != null) _resultText.gameObject.SetActive(false);
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

        bool isPaused = PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused;
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = !isPaused;
        if (isPaused) return;

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
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;

        // Direct Tile Swap Mode (Tier 4 & 5 perk: tap any two tiles to swap positions directly)
        if (_swapModeActive)
        {
            if (_tiles[idx] == 0) return; // Cannot swap empty slot

            if (_firstSwapIdx < 0)
            {
                _firstSwapIdx = idx;
                UpdateSwapButtonUI();
                RefreshTileDisplay();
            }
            else if (_firstSwapIdx == idx)
            {
                // Tapping same tile deselects
                _firstSwapIdx = -1;
                UpdateSwapButtonUI();
                RefreshTileDisplay();
            }
            else
            {
                // Execute direct swap between the two chosen tiles
                SwapTiles(_firstSwapIdx, idx);
                _firstSwapIdx = -1;
                _swapsRemaining--;
                _swapModeActive = false;

                UpdateSwapButtonUI();
                RefreshTileDisplay();

                if (IsSolved()) Finish(true);
            }
            return;
        }

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

    /// <summary>
    /// Evaluates adjacent moves to find the one that best minimizes total Manhattan distance to solved positions.
    /// Used by Next-Move Guide (Tier >= 3).
    /// </summary>
    private int GetOptimalNextMove()
    {
        if (_tiles == null || _emptyIdx < 0 || _n <= 0) return -1;
        int[] neighbors = GetNeighbors(_emptyIdx);
        if (neighbors == null || neighbors.Length == 0) return -1;

        int bestNeighbor = neighbors[0];
        int bestScore = int.MaxValue;

        for (int i = 0; i < neighbors.Length; i++)
        {
            int cand = neighbors[i];
            int val = _tiles[cand];
            if (val == 0) continue;

            int goalIdx = val - 1;
            int goalRow = goalIdx / _n;
            int goalCol = goalIdx % _n;

            int oldDist = Mathf.Abs(cand / _n - goalRow) + Mathf.Abs(cand % _n - goalCol);
            int newDist = Mathf.Abs(_emptyIdx / _n - goalRow) + Mathf.Abs(_emptyIdx % _n - goalCol);
            int delta   = newDist - oldDist;

            // Lower score is better:
            int score = delta * 1000;
            if (newDist == 0) score -= 400; // Bonus: moved into correct slot
            if (oldDist == 0) score += 800; // Penalty: displaced already-correct tile
            score += goalRow * 50;           // Tie-breaker: prioritize earlier rows

            if (score < bestScore)
            {
                bestScore = score;
                bestNeighbor = cand;
            }
        }

        return bestNeighbor;
    }

    // -----------------------------------------------------------------------
    // Hint (shows solved overlay briefly)
    // -----------------------------------------------------------------------

    public void CancelMinigame()
    {
        StopAllCoroutines();
        _finished = true;
        _swapModeActive = false;
        _firstSwapIdx = -1;
        _swapButtonGO = null;
        if (_rootPanel != null)
        {
            Destroy(_rootPanel.gameObject);
            _rootPanel = null;
        }
        CleanupSlices();
        UIManager.Instance?.SetExplorationHUDVisible(true);
    }

    private void OnDisable()
    {
        CancelMinigame();
    }

    private void OnDestroy()
    {
        CancelMinigame();
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
        _swapModeActive = false;
        _firstSwapIdx = -1;
        if (_swapButtonGO != null) _swapButtonGO.SetActive(false);

        if (success)
        {
            // Reveal the final missing piece in the empty slot to complete the photo!
            if (_tileImages != null && _emptyIdx >= 0 && _emptyIdx < _tileImages.Length &&
                _tileSlices != null && _tileSlices.Length == _tiles.Length)
            {
                _tileImages[_emptyIdx].sprite = _tileSlices[_tiles.Length - 1];
                _tileImages[_emptyIdx].color  = Color.white;
            }
            if (_tileBorderImages != null && _emptyIdx >= 0 && _emptyIdx < _tileBorderImages.Length && _tileBorderImages[_emptyIdx] != null)
            {
                _tileBorderImages[_emptyIdx].color = new Color(0.18f, 0.95f, 0.82f, 0.95f);
            }
            if (_tileTMP != null && _emptyIdx >= 0 && _emptyIdx < _tileTMP.Length)
            {
                _tileTMP[_emptyIdx].enabled = false;
            }
        }

        if (_resultText != null)
        {
            string speciesName = _data != null && !string.IsNullOrEmpty(_data.commonName)
                ? _data.commonName.ToUpper()
                : "SPECIES";
            _resultText.text  = success
                ? $"<b>SCAN COMPLETED!\n<size=32><color=#ffffff>{speciesName} CAPTURED</color></size></b>"
                : "<b>FOCUS LOST</b>";
            _resultText.color = success
                ? new Color(0.20f, 0.90f, 0.78f)
                : new Color(1f, 0.35f, 0.35f);

            if (_resultBorder != null)
                _resultBorder.color = success ? new Color(0.15f, 0.85f, 0.80f, 0.95f) : new Color(0.95f, 0.25f, 0.25f, 0.95f);

            if (_resultTopStripe != null)
                _resultTopStripe.color = success ? new Color(0.20f, 0.92f, 0.82f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);

            _resultText.ForceMeshUpdate();
            float textW = _resultText.preferredWidth;
            float textH = _resultText.preferredHeight;
            if (_resultBoxRect != null)
            {
                _resultBoxRect.sizeDelta = new Vector2(Mathf.Max(380f, textW + 64f), Mathf.Max(84f, textH + 32f));
            }

            if (_resultBox != null)
            {
                _resultBox.transform.SetAsLastSibling();
                _resultBox.SetActive(true);
            }
            _resultText.gameObject.SetActive(true);
        }

        StartCoroutine(DelayedClose(success));
    }

    private IEnumerator DelayedClose(bool success)
    {
        yield return new WaitForSecondsRealtime(1.3f);
        if (_rootPanel != null) _rootPanel.gameObject.SetActive(false);
        UIManager.Instance?.SetExplorationHUDVisible(true);
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
                    _tileImages[i].color  = new Color(0.01f, 0.03f, 0.06f, 0.98f);
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

            if (_tileBorderImages != null && i < _tileBorderImages.Length && _tileBorderImages[i] != null)
            {
                if (isEmpty)
                {
                    _tileBorderImages[i].color = new Color(0.01f, 0.03f, 0.06f, 0.4f);
                }
                else if (_swapModeActive && i == _firstSwapIdx)
                {
                    _tileBorderImages[i].color = new Color(1.0f, 0.82f, 0.20f, 1.0f); // Selected swap tile (Bright Gold)
                }
                else if (_swapModeActive)
                {
                    _tileBorderImages[i].color = new Color(0.85f, 0.65f, 0.15f, 0.45f); // Swap target candidate
                }
                else
                {
                    _tileBorderImages[i].color = new Color(0.12f, 0.22f, 0.32f, 0.80f); // Default tech border
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

        Canvas canvas = UIManager.GetExplorationCanvas();
        if (canvas == null) return;

        // Root panel - transparent background so 3D scene remains visible
        var rootGO = new GameObject("ReconMinigame", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvas.transform, false);
        _rootPanel = rootGO.GetComponent<RectTransform>();
        _canvasGroup = rootGO.GetComponent<CanvasGroup>();
        _rootPanel.anchorMin = Vector2.zero;
        _rootPanel.anchorMax = Vector2.one;
        _rootPanel.sizeDelta = Vector2.zero;
        rootGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.0f);
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
        _tileButtons      = new Button[total];
        _tileTMP          = new TMP_Text[total];
        _tileImages       = new Image[total];
        _tileBorderImages = new Image[total];

        float gap = 4f;
        float sz  = (gridAreaW - gap * (_n + 1)) / _n;

        for (int i = 0; i < total; i++)
        {
            int row = i / _n, col = i % _n;
            float x = gap + col * (sz + gap) + sz * 0.5f - gridAreaW * 0.5f;
            float y = gridAreaH - (gap + row * (sz + gap) + sz * 0.5f);

            // Outer frame / border
            var tileGO = new GameObject($"Tile{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            tileGO.transform.SetParent(gridGO.transform, false);
            var tr = tileGO.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0.5f); tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(sz, sz);
            tr.anchoredPosition = new Vector2(x, y - gridAreaH * 0.5f);

            var tileBorderImg = tileGO.GetComponent<Image>();
            tileBorderImg.sprite = CaptureAndFocusMinigame.WhiteSprite;
            tileBorderImg.color = new Color(0.12f, 0.22f, 0.32f, 0.80f);
            _tileBorderImages[i] = tileBorderImg;

            // Inner image (inset 2.5px to show outer border frame)
            var innerGO = new GameObject("InnerImg", typeof(RectTransform), typeof(Image));
            innerGO.transform.SetParent(tileGO.transform, false);
            var ir = innerGO.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(2.5f, 2.5f);
            ir.offsetMax = new Vector2(-2.5f, -2.5f);

            _tileImages[i] = innerGO.GetComponent<Image>();
            _tileImages[i].color = new Color(0.12f, 0.25f, 0.40f, 1f);
            _tileImages[i].raycastTarget = false;

            var numGO = new GameObject("Num", typeof(RectTransform));
            numGO.transform.SetParent(innerGO.transform, false);
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
            _tileTMP[i].raycastTarget = false;

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

        // Direct Tile Swap Button (Tier >= 4 perk: tap any 2 tiles to swap)
        if (GetScannerTier() >= 4)
        {
            float btnW = Mathf.Clamp(Mathf.Max(gridAreaW * 1.15f, 420f), 380f, 540f);
            float btnH = 56f;
            float swapBtnY = midY - gridAreaH * 0.5f - btnH * 0.5f - 24f;

            _swapButtonGO = new GameObject("SwapButtonFrame", typeof(RectTransform), typeof(Image), typeof(Button));
            _swapButtonGO.transform.SetParent(_rootPanel, false);
            var srect = _swapButtonGO.GetComponent<RectTransform>();
            srect.anchorMin = new Vector2(0.5f, 0.5f);
            srect.anchorMax = new Vector2(0.5f, 0.5f);
            srect.pivot     = new Vector2(0.5f, 0.5f);
            srect.anchoredPosition = new Vector2(0f, swapBtnY);
            srect.sizeDelta = new Vector2(btnW, btnH);

            _swapButtonBorder = _swapButtonGO.GetComponent<Image>();
            _swapButtonBorder.sprite = CaptureAndFocusMinigame.WhiteSprite;
            _swapButtonBorder.color = new Color(0.20f, 0.75f, 0.90f, 0.95f);

            var swapInnerGO = new GameObject("SwapInner", typeof(RectTransform), typeof(Image));
            swapInnerGO.transform.SetParent(_swapButtonGO.transform, false);
            var sirect = swapInnerGO.GetComponent<RectTransform>();
            sirect.anchorMin = Vector2.zero;
            sirect.anchorMax = Vector2.one;
            sirect.offsetMin = new Vector2(3f, 3f);
            sirect.offsetMax = new Vector2(-3f, -3f);
            _swapButtonBg = swapInnerGO.GetComponent<Image>();
            _swapButtonBg.sprite = CaptureAndFocusMinigame.WhiteSprite;
            _swapButtonBg.color = new Color(0.02f, 0.08f, 0.14f, 0.95f);
            _swapButtonBg.raycastTarget = false;

            var swapTextGO = new GameObject("SwapText", typeof(RectTransform));
            swapTextGO.transform.SetParent(swapInnerGO.transform, false);
            var stextRect = swapTextGO.GetComponent<RectTransform>();
            stextRect.anchorMin = Vector2.zero;
            stextRect.anchorMax = Vector2.one;
            stextRect.offsetMin = new Vector2(16f, 4f);
            stextRect.offsetMax = new Vector2(-16f, -4f);
            _swapButtonTMP = swapTextGO.AddComponent<TextMeshProUGUI>();
            if (font != null) _swapButtonTMP.font = font;
            _swapButtonTMP.enableAutoSizing = true;
            _swapButtonTMP.fontSizeMin = 14f;
            _swapButtonTMP.fontSizeMax = 22f;
            _swapButtonTMP.textWrappingMode = TextWrappingModes.NoWrap; // Strictly single line
            _swapButtonTMP.overflowMode = TextOverflowModes.Ellipsis;
            _swapButtonTMP.fontStyle = FontStyles.Bold;
            _swapButtonTMP.alignment = TextAlignmentOptions.Center;
            _swapButtonTMP.color = Color.white;
            _swapButtonTMP.raycastTarget = false;

            _swapButton = _swapButtonGO.GetComponent<Button>();
            _swapButton.onClick.AddListener(OnSwapButtonClicked);
        }

        // Result dialog card box (Confirmation popup style, compact and sized to fit text perfectly)
        _resultBox = new GameObject("ResultCardBox", typeof(RectTransform), typeof(Image));
        _resultBox.transform.SetParent(_rootPanel, false);
        _resultBoxRect = _resultBox.GetComponent<RectTransform>();
        _resultBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        _resultBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        _resultBoxRect.pivot     = new Vector2(0.5f, 0.5f);
        _resultBoxRect.anchoredPosition = new Vector2(0f, midY);
        _resultBoxRect.sizeDelta = new Vector2(480f, 96f);

        _resultBorder = _resultBox.GetComponent<Image>();
        _resultBorder.sprite = CaptureAndFocusMinigame.WhiteSprite;
        _resultBorder.color = new Color(0.95f, 0.25f, 0.25f, 0.95f);
        _resultBorder.raycastTarget = false;

        // Inner Dialog Card (inset 3px for glowing border frame, matching confirmation popup DialogCard color)
        var innerCardGO = new GameObject("DialogCard", typeof(RectTransform), typeof(Image));
        innerCardGO.transform.SetParent(_resultBox.transform, false);
        var innerCardRect = innerCardGO.GetComponent<RectTransform>();
        innerCardRect.anchorMin = Vector2.zero;
        innerCardRect.anchorMax = Vector2.one;
        innerCardRect.offsetMin = new Vector2(3f, 3f);
        innerCardRect.offsetMax = new Vector2(-3f, -3f);
        var innerCardImg = innerCardGO.GetComponent<Image>();
        innerCardImg.sprite = CaptureAndFocusMinigame.WhiteSprite;
        innerCardImg.color = new Color(0.04f, 0.08f, 0.15f, 0.98f);
        innerCardImg.raycastTarget = false;

        // Top accent line
        var topStripeGO = new GameObject("TopStripe", typeof(RectTransform), typeof(Image));
        topStripeGO.transform.SetParent(innerCardGO.transform, false);
        var tsRect = topStripeGO.GetComponent<RectTransform>();
        tsRect.anchorMin = new Vector2(0f, 1f);
        tsRect.anchorMax = new Vector2(1f, 1f);
        tsRect.pivot     = new Vector2(0.5f, 1f);
        tsRect.sizeDelta = new Vector2(0f, 3.5f);
        _resultTopStripe = topStripeGO.GetComponent<Image>();
        _resultTopStripe.sprite = CaptureAndFocusMinigame.WhiteSprite;
        _resultTopStripe.color = new Color(1f, 0.35f, 0.35f, 0.95f);
        _resultTopStripe.raycastTarget = false;

        // Result label inside DialogCard
        var resultGO = new GameObject("ResultText", typeof(RectTransform));
        resultGO.transform.SetParent(innerCardGO.transform, false);
        var rr = resultGO.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = new Vector2(24f, 12f);
        rr.offsetMax = new Vector2(-24f, -12f);
        _resultText = resultGO.AddComponent<TextMeshProUGUI>();
        if (font != null) _resultText.font = font;
        _resultText.fontSize = 36;
        _resultText.fontStyle = FontStyles.Bold;
        _resultText.alignment = TextAlignmentOptions.Center;
        _resultText.raycastTarget = false;

        _resultBox.SetActive(false);

        UIManager.CreateMinigamePauseButton(_rootPanel);

        _rootPanel.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Direct Tile Swap Actions (Scanner Tier 4 & 5)
    // -----------------------------------------------------------------------

    private void OnSwapButtonClicked()
    {
        if (_finished || _swapsRemaining <= 0) return;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;

        _swapModeActive = !_swapModeActive;
        _firstSwapIdx = -1;
        UpdateSwapButtonUI();
        RefreshTileDisplay();
    }

    private void UpdateSwapButtonUI()
    {
        if (_swapButtonGO == null) return;

        if (GetScannerTier() < 4)
        {
            _swapButtonGO.SetActive(false);
            return;
        }

        _swapButtonGO.SetActive(true);

        if (_swapsRemaining <= 0)
        {
            if (_swapButton != null) _swapButton.interactable = false;
            if (_swapButtonBg != null) _swapButtonBg.color = new Color(0.04f, 0.06f, 0.09f, 0.70f);
            if (_swapButtonBorder != null) _swapButtonBorder.color = new Color(0.25f, 0.30f, 0.35f, 0.50f);
            if (_swapButtonTMP != null)
            {
                _swapButtonTMP.text = "DIRECT SWAPS DEPLETED";
                _swapButtonTMP.color = new Color(0.5f, 0.55f, 0.6f, 0.7f);
            }
        }
        else if (_swapModeActive)
        {
            if (_swapButton != null) _swapButton.interactable = true;
            if (_swapButtonBg != null) _swapButtonBg.color = new Color(0.18f, 0.12f, 0.02f, 0.98f);
            if (_swapButtonBorder != null) _swapButtonBorder.color = new Color(1.0f, 0.80f, 0.20f, 1.0f);
            if (_swapButtonTMP != null)
            {
                _swapButtonTMP.text = _firstSwapIdx < 0
                    ? "TAP 1ST TILE TO SWAP (CANCEL)"
                    : "TAP 2ND TILE TO SWAP (CANCEL)";
                _swapButtonTMP.color = new Color(1f, 0.88f, 0.30f);
            }
        }
        else
        {
            if (_swapButton != null) _swapButton.interactable = true;
            if (_swapButtonBg != null) _swapButtonBg.color = new Color(0.02f, 0.08f, 0.14f, 0.95f);
            if (_swapButtonBorder != null) _swapButtonBorder.color = new Color(0.20f, 0.75f, 0.90f, 0.95f);
            if (_swapButtonTMP != null)
            {
                string swapWord = _swapsRemaining == 1 ? "SWAP" : "SWAPS";
                _swapButtonTMP.text = $"DIRECT TILE SWAP ({_swapsRemaining} {swapWord})";
                _swapButtonTMP.color = Color.white;
            }
        }
    }
}
