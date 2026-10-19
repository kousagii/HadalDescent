using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One saved research checkpoint per zone. Runs before first descent, not after each scan.
/// All species facts come from authored banks linked to unlocked bestiary records.
/// </summary>
public class ResearchVerificationMinigame : MonoBehaviour
{
    public static ResearchVerificationMinigame Instance { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsIntroducing { get; private set; }

    [Header("Optional Overrides (Resources/Research is used by default)")]
    [SerializeField] private ResearchQuestionBank[] questionBanks = new ResearchQuestionBank[5];
    [SerializeField] private ResearchVerificationUI reviewUI;

    private ResearchReviewSession _session;
    private List<ResearchQuestion> _questions;
    private List<SpeciesData> _captured;
    private Action _onComplete;
    private Action _onCancel;
    private bool _showingBestiary;
    private bool _answered;
    private bool _results;
    private int _selectedAnswer = -1;
    private int _correctAnswer;
    private string[] _answerLabels;
    private Sprite[] _answerModels;
    private Sprite[] _answerPhotographs;
    private SpeciesData[] _answerSpecies;
    private bool _isPlaytest;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (IsOpen) CancelMinigame();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        // Existing bestiary/pause/boundary close methods restore the world independently.
        // This checkpoint owns the pause until it finishes or is cancelled.
        Time.timeScale = 0f;
        UIManager.Instance?.SetExplorationHUDVisible(false);
        ScanReticleUI.Instance?.SetVisible(false);
    }

    public bool Open(int zoneIndex, Action onComplete, Action onCancel = null)
    {
        if (IsOpen || !ZoneConfig.IsValidZone(zoneIndex)) return false;
        _onComplete = onComplete;
        _onCancel = onCancel;
        _results = false;
        _answerLabels = null;
        IsOpen = true;
        if (reviewUI == null) reviewUI = GetComponent<ResearchVerificationUI>() ?? gameObject.AddComponent<ResearchVerificationUI>();
        MinigameManager.Instance.SetResearchReviewState(true, false);
        UIManager.Instance?.SetExplorationHUDVisible(false);
        Time.timeScale = 0f;

        var bank = questionBanks != null && zoneIndex < questionBanks.Length ? questionBanks[zoneIndex] : null;
        if (bank == null) bank = Resources.Load<ResearchQuestionBank>($"Research/Zone{zoneIndex}Questions");
        if (!LoadSession(bank, zoneIndex, out string error))
        {
            Debug.LogError($"[ResearchVerification] {error}");
            reviewUI.Show("RESEARCH CHECK UNAVAILABLE", "", "Dr. Santos: The research records could not be loaded. Your discoveries are safe.\n\n" + error);
            reviewUI.AddAction("RETURN", CancelMinigame);
            return true;
        }
        if (_isPlaytest) { ShowPreparation(); return true; }
        GameManager.Instance.SaveGame();

        IsIntroducing = true;
        var radio = RadioTextboxUI.Instance;
        radio.gameObject.SetActive(true);
        StartCoroutine(Introduce(radio, zoneIndex));
        return true;
    }

    private IEnumerator Introduce(RadioTextboxUI radio, int zoneIndex)
    {
        // A newly auto-created radio hides itself in Start. Let that initialization finish.
        yield return null;
        if (!IsOpen) yield break;
        if (radio != null)
        {
            radio.DisplayMessage("Dr. Santos", zoneIndex == 4
                ? "Researcher, before we complete this expedition, let's verify the records of the species you captured in the Hadal Zone."
                : "Researcher, before we descend, let's verify the records of the species you captured in this zone.",
                onComplete: () => { if (IsOpen) ShowPreparation(); }, forceImmediate: true);
        }
        else ShowPreparation();
    }

    private bool LoadSession(ResearchQuestionBank bank, int zoneIndex, out string error)
    {
        error = null;
        var registry = Resources.Load<SpeciesRegistry>("SpeciesRegistry");
        if (registry == null || bank == null || bank.zoneIndex != zoneIndex)
        {
            error = "Missing species registry or zone question bank.";
            return false;
        }
        _captured = registry.GetSpeciesForZone(zoneIndex).Where(s =>
            !GameManager.IsLandmarkFactId(s.speciesId) && (_isPlaytest || GameManager.Instance.IsDiscovered(s.speciesId))).ToList();
        _session = null;
        string draft = _isPlaytest ? "" : GameManager.Instance.GetResearchReviewDraft(zoneIndex);
        if (!string.IsNullOrEmpty(draft))
        {
            try { _session = JsonUtility.FromJson<ResearchReviewSession>(draft); }
            catch (ArgumentException) { _session = null; }
        }
        var byId = bank.questions.Where(q => q != null && q.IsValid()).GroupBy(q => q.questionId)
            .ToDictionary(g => g.Key, g => g.First());
        if (_session != null && _session.zoneIndex == zoneIndex && _session.questionIds != null &&
            _session.questionIds.Count == ResearchQuestionSelector.QuestionCounts[zoneIndex] &&
            _session.questionIds.Distinct().Count() == _session.questionIds.Count &&
            _session.questionIds.All(id => byId.ContainsKey(id) && _captured.Contains(byId[id].species)) &&
            _session.capturedIds != null && _session.capturedIds.Count > 0 &&
            _session.capturedIds.Distinct().Count() == _session.capturedIds.Count &&
            _session.capturedIds.All(id => _captured.Any(s => s.speciesId == id)) &&
            _session.answers != null && _session.answers.Count <= _session.questionIds.Count &&
            _session.answers.All(answer => answer >= 0 && answer < 4) &&
            _session.score >= 0 && _session.score <= _session.answers.Count &&
            _session.pendingAnswer >= -1 && _session.pendingAnswer < 4 &&
            _session.missedIds != null && _session.missedIds.All(id => _session.questionIds.Contains(id)) &&
            _session.missedIds.Distinct().Count() == _session.missedIds.Count &&
            _session.missedIds.Count == _session.answers.Count - _session.score &&
            (!_session.correcting || _session.answers.Count == _session.questionIds.Count) &&
            _session.correctionIndex >= 0 && _session.correctionIndex <= _session.missedIds.Count)
        {
            _captured = _session.capturedIds.Select(id => _captured.First(s => s.speciesId == id)).ToList();
            _questions = _session.questionIds.Select(id => byId[id]).ToList();
            if (!ResearchQuestionSelector.PrepareSavedCameraQuestions(bank, _captured, _session, _questions, out error)) return false;
            SaveDraft();
            return true;
        }
        int seed = Guid.NewGuid().GetHashCode();
        _questions = ResearchQuestionSelector.Select(bank, s => _captured.Contains(s), seed, out error);
        if (_questions == null) return false;
        _session = new ResearchReviewSession
        {
            zoneIndex = zoneIndex, seed = seed,
            questionIds = _questions.Select(q => q.questionId).ToList(),
            capturedIds = _captured.Select(s => s.speciesId).ToList()
        };
        _session.cameraPresentationVersion = 1;
        SaveDraft();
        return true;
    }

    private void ShowPreparation()
    {
        IsIntroducing = false;
        MinigameManager.Instance.SetResearchReviewState(true, true);
        RadioTextboxUI.Instance?.HideInstant();
        string zone = ZoneConfig.Zones[_session.zoneIndex].zoneName;
        reviewUI.Show("RESEARCH VERIFICATION", zone.ToUpper() + (_isPlaytest ? " / QUICK TEST" : ""),
            "Dr. Santos: Before we continue, let's go over the species you've captured. " +
            "Would you like to review your bestiary first?\n\n" +
            $"We'll check {_questions.Count} observations. Verify at least {ResearchQuestionSelector.RequiredCorrect(_questions.Count)} to proceed. " +
            "Take your time, researcher. We'll review any records that need attention together.");
        reviewUI.AddAction("REVIEW BESTIARY", OpenBestiary);
        reviewUI.AddAction(_session.answers.Count > 0 ? "RESUME CHECK" : "BEGIN CHECK", ContinueSession);
        reviewUI.AddAction("RETURN", CancelMinigame);
    }

    private void ContinueSession()
    {
        _results = false;
        if (_session.correcting)
        {
            if (_session.correctionIndex >= _session.missedIds.Count) ShowReady();
            else ShowQuestion();
        }
        else if (_session.answers.Count >= _questions.Count) ShowResults();
        else ShowQuestion();
    }

    private ResearchQuestion CurrentQuestion => _session.correcting
        ? _questions.First(q => q.questionId == _session.missedIds[_session.correctionIndex])
        : _questions[_session.answers.Count];

    private void ShowQuestion()
    {
        _answered = false;
        _selectedAnswer = -1;
        var question = CurrentQuestion;
        // Stable order also survives cancelling on the same unanswered question.
        int position = _questions.IndexOf(question);
        var random = new System.Random(unchecked(_session.seed + position * 7919));
        if (question.format == ResearchQuestionFormat.CameraIdentification)
        {
            var options = _captured.Where(s => s != question.species && ResearchQuestionSelector.HasCameraVisual(s))
                .OrderBy(_ => random.Next()).Take(2).ToList();
            options.Add(question.species);
            options = options.OrderBy(_ => random.Next()).ToList();
            _answerLabels = options.Select(s => s.commonName).ToArray();
            _answerSpecies = options.ToArray();
            _answerModels = options.Select(ModelFor).ToArray();
            _answerPhotographs = options.Select(PhotoFor).ToArray();
            _correctAnswer = options.IndexOf(question.species);
            AudioManager.Instance?.PlayCameraShutter();
        }
        else
        {
            var order = Enumerable.Range(0, question.choices.Length).OrderBy(_ => random.Next()).ToList();
            _answerLabels = order.Select(i => question.choices[i]).ToArray();
            _answerModels = new Sprite[order.Count];
            _answerPhotographs = new Sprite[order.Count];
            _answerSpecies = new SpeciesData[order.Count];
            _correctAnswer = order.IndexOf(question.correctChoice);
        }
        if (_session.pendingAnswer >= 0 && _session.pendingAnswer < _answerLabels.Length)
        {
            _answered = true;
            _selectedAnswer = _session.pendingAnswer;
        }
        RenderQuestion();
    }

    private static Sprite PhotoFor(SpeciesData data)
    {
        if (data.photo != null) return data.photo;
        return data.fullImage;
    }

    private static Sprite ModelFor(SpeciesData data) => data.modelPrefab != null
        ? ModelPreviewSystem.Instance.GetOrRenderThumbnail(data, requireModel: true) : null;

    private void RenderQuestion()
    {
        var question = CurrentQuestion;
        int number = _session.correcting ? _session.correctionIndex + 1 : _session.answers.Count + 1;
        int total = _session.correcting ? _session.missedIds.Count : _questions.Count;
        string title = question.format == ResearchQuestionFormat.CameraIdentification ? "WHO'S IN THE CAMERA?" : "VERIFY THE RECORD";
        reviewUI.Show(title, $"{(_session.correcting ? "GUIDED CORRECTION" : ZoneConfig.Zones[_session.zoneIndex].zoneName.ToUpper())}   {number} / {total}",
            question.prompt);
        if (question.format == ResearchQuestionFormat.Verification)
            reviewUI.AddSpeciesVisuals(ModelFor(question.species), PhotoFor(question.species), question.species);
        for (int i = 0; i < _answerLabels.Length; i++)
        {
            int index = i;
            if (question.format == ResearchQuestionFormat.CameraIdentification)
                reviewUI.AddCameraAnswer(_answerLabels[i], _answerModels[i], _answerPhotographs[i],
                    () => Answer(index), !_answered, _answered && index == _correctAnswer, _answerSpecies[i]);
            else
                reviewUI.AddAnswer(_answerLabels[i], null, () => Answer(index), !_answered,
                    _answered && index == _correctAnswer);
        }
        if (_answered)
        {
            reviewUI.AddFeedback((_selectedAnswer == _correctAnswer
                ? "That's right, researcher.\n" : "Here's what our observations show.\n") +
                question.explanation);
        }
        reviewUI.SetQuestionActions(CancelMinigame, _answered ? (Action)Advance : null);
    }

    private void Answer(int index)
    {
        if (_answered || !IsOpen || _showingBestiary) return;
        _answered = true;
        _selectedAnswer = index;
        _session.pendingAnswer = index;
        SaveDraft();
        RenderQuestion();
    }

    private void Advance()
    {
        if (!_answered) return;
        _answered = false;
        _session.CommitAnswer(_selectedAnswer, _correctAnswer);
        SaveDraft();
        ContinueSession();
    }

    private void ShowResults()
    {
        _results = true;
        bool passed = _session.Passed;
        reviewUI.Show(passed ? (_session.zoneIndex == 4 ? "EXPEDITION REVIEW PASSED" : "RESEARCH CHECK PASSED") : "SOME RECORDS NEED REVIEW", $"{_session.score} / {_questions.Count} CORRECT",
            passed ? "Dr. Santos: Your records are verified. Excellent work, researcher. You are cleared to proceed."
                : "Dr. Santos: A few details need checking before we continue. Let's go over those observations together.");
        foreach (string id in _session.missedIds)
        {
            var question = _questions.First(q => q.questionId == id);
            reviewUI.AddFeedback(question.explanation);
        }
        if (passed)
        {
            reviewUI.SetProceedAction(Complete);
            return;
        }
        reviewUI.AddAction("CORRECT RECORDS", () =>
        {
            _session.correcting = true;
            SaveDraft();
            ContinueSession();
        });
        reviewUI.AddAction("REVIEW BESTIARY", OpenBestiary);
        reviewUI.AddAction("SAVE & RETURN", CancelMinigame);
    }

    private void ShowReady()
    {
        _results = true;
        reviewUI.Show(_session.zoneIndex == 4 ? "EXPEDITION RESEARCH COMPLETE" : "RESEARCH VERIFIED", "ALL CORRECTIONS COMPLETE",
            "Dr. Santos: All records are verified. Thank you for checking the evidence, researcher. You are cleared to proceed.");
        reviewUI.SetProceedAction(Complete);
    }

    private void OpenBestiary()
    {
        // Study is available before the check and during the records review, never mid-question.
        if (_showingBestiary || (_answerLabels != null && !_results)) return;
        _showingBestiary = true;
        reviewUI.Hide();
        var bestiary = BestiaryManager.OpenBestiary();
        if (bestiary == null) { _showingBestiary = false; ReturnFromBestiary(); return; }
        bestiary.SelectTab(_session.zoneIndex);
        bestiary.OnBestiaryClosed += ReturnFromBestiary;
    }

    private void ReturnFromBestiary()
    {
        if (BestiaryManager.Instance != null) BestiaryManager.Instance.OnBestiaryClosed -= ReturnFromBestiary;
        _showingBestiary = false;
        if (!IsOpen) return;
        MinigameManager.Instance.SetResearchReviewState(true, true);
        if (_results)
        {
            if (_session.correcting) ShowReady();
            else ShowResults();
        }
        else if (_answerLabels != null) RenderQuestion();
        else ShowPreparation();
    }

    private void SaveDraft()
    {
        if (!_isPlaytest && _session != null) GameManager.Instance.SetResearchReviewDraft(_session.zoneIndex, JsonUtility.ToJson(_session));
    }

    private void Complete()
    {
        if (!IsOpen || _session == null || (!_session.Passed && !_session.CorrectionsComplete)) return;
        if (!_isPlaytest) GameManager.Instance.MarkResearchVerified(_session.zoneIndex);
        var callback = _onComplete;
        Close();
        callback?.Invoke();
    }

    public void CancelMinigame()
    {
        if (!IsOpen) return;
        SaveDraft();
        var callback = _onCancel;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        IsOpen = false;
        IsIntroducing = false;
        StopAllCoroutines();
        if (BestiaryManager.Instance != null)
        {
            BestiaryManager.Instance.OnBestiaryClosed -= ReturnFromBestiary;
            if (_showingBestiary) BestiaryManager.Instance.HideBestiary();
        }
        _showingBestiary = false;
        RadioTextboxUI.Instance?.HideInstant();
        reviewUI?.Release();
        MinigameManager.Instance.SetResearchReviewState(false, false);
        _onComplete = _onCancel = null;
        _answerLabels = null;
        _session = null;
        _isPlaytest = false;
        ZoneBoundaryTrigger.PushPlayerFromActiveBoundary();
    }

#if UNITY_EDITOR
    /// <summary>Editor-only session using all zone species without unlocking or saving them.</summary>
    public bool OpenForTesting(int zoneIndex)
    {
        if (!Application.isPlaying || IsOpen) return false;
        _isPlaytest = true;
        bool opened = Open(zoneIndex, () => Debug.Log("[ResearchQuickTest] Review completed; game progress was not changed."));
        if (!opened) _isPlaytest = false;
        return opened;
    }
#endif
}
