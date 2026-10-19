using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Read-only content/selection validation. Does not regenerate or resave assets.</summary>
public static class ResearchVerificationValidation
{
    [MenuItem("HadalDescent/Research/Validate Question Banks")]
    public static void ValidateBanks()
    {
        var registry = Resources.Load<SpeciesRegistry>("SpeciesRegistry");
        Require(registry != null, "Species registry is missing.");
        int checks = 0;
        for (int zone = 0; zone < ZoneConfig.ZoneCount; zone++)
        {
            var bank = Resources.Load<ResearchQuestionBank>($"Research/Zone{zone}Questions");
            Require(bank != null && bank.zoneIndex == zone, $"Zone {zone}: bank missing or mismatched.");
            Require(bank.questions.Select(q => q.questionId).Distinct().Count() == bank.questions.Count,
                $"Zone {zone}: duplicate question IDs.");
            var species = registry.GetSpeciesForZone(zone);
            foreach (var question in bank.questions)
            {
                Require(question.IsValid(), $"Invalid question: {question.questionId}");
                Require(species.Contains(question.species), $"Question references another zone: {question.questionId}");
                if (question.format == ResearchQuestionFormat.Verification)
                    Require(question.choices[question.correctChoice] == question.SourceText,
                        $"Correct answer differs from bestiary: {question.questionId}");
                checks++;
            }
            // Exercise varied minimum-unlock collections, including every individual species.
            // A fixed seed also verifies stable resumption without touching Unity's PCG random state.
            int minimum = Mathf.CeilToInt(ZoneConfig.Zones[zone].totalSpeciesCount * ZoneConfig.Zones[zone].unlockThreshold);
            for (int rotation = 0; rotation < species.Count; rotation++)
            {
                var captured = Enumerable.Range(0, minimum).Select(i => species[(rotation + i) % species.Count]).ToList();
                var selected = ResearchQuestionSelector.Select(bank, captured.Contains, rotation, out string error);
                Require(selected != null, error);
                Require(selected.Count == ResearchQuestionSelector.QuestionCounts[zone], "Incorrect question count.");
                Require(selected.All(q => captured.Contains(q.species)), "Uncaptured species leaked into the quiz.");
                Require(selected.Select(q => q.questionId).Distinct().Count() == selected.Count, "Repeated question.");
                var repeated = ResearchQuestionSelector.Select(bank, captured.Contains, rotation, out _);
                Require(selected.Select(q => q.questionId).SequenceEqual(repeated.Select(q => q.questionId)), "Unstable selection.");
                int cameraSpecies = captured.Count(ResearchQuestionSelector.HasCameraVisual);
                int expectedCameras = cameraSpecies >= 2 ? ResearchQuestionSelector.CameraCounts[zone] : 0;
                Require(selected.Count(q => q.format == ResearchQuestionFormat.CameraIdentification) == expectedCameras,
                    "Camera count / imagery fallback is incorrect.");
                checks += 5;
            }
            var empty = ResearchQuestionSelector.Select(bank, _ => false, 0, out string emptyError);
            Require(empty == null && !string.IsNullOrEmpty(emptyError), "Empty collections must fail with a useful error.");
            Debug.Log($"[ResearchValidation] Zone {zone}: {bank.questions.Count} valid questions; {species.Count} minimum-collection checks passed.");
        }
        int[] thresholds = { 4, 6, 8, 9, 11 };
        for (int zone = 0; zone < thresholds.Length; zone++)
            Require(ResearchQuestionSelector.RequiredCorrect(ResearchQuestionSelector.QuestionCounts[zone]) == thresholds[zone], "Passing threshold changed.");
        var draft = new ResearchReviewSession
        {
            zoneIndex = 2, seed = 51, questionIds = new List<string> { "a", "b" },
            capturedIds = new List<string> { "fish" }, answers = new List<int> { 1 },
            missedIds = new List<string> { "a" }, correcting = true, correctionIndex = 0, score = 0, pendingAnswer = 2
        };
        var restored = JsonUtility.FromJson<ResearchReviewSession>(JsonUtility.ToJson(draft));
        Require(restored.pendingAnswer == 2 && restored.correcting && restored.seed == 51 &&
            restored.questionIds.SequenceEqual(draft.questionIds) && restored.missedIds.SequenceEqual(draft.missedIds),
            "Review progress does not survive JSON serialization.");
        var failed = new ResearchReviewSession { questionIds = new List<string> { "a", "b", "c", "d", "e" } };
        for (int i = 0; i < 5; i++) failed.CommitAnswer(1, 0);
        Require(!failed.Passed && failed.missedIds.Count == 5, "Failed reviews must retain missed concepts.");
        failed.correcting = true;
        failed.CommitAnswer(1, 0);
        Require(failed.correctionIndex == 0, "Wrong corrections must not unlock progression.");
        for (int i = 0; i < 5; i++) failed.CommitAnswer(0, 0);
        Require(failed.CorrectionsComplete && failed.score == 0, "Guided correction must complete without rewriting the initial score.");
        ValidatePresentation();
        Debug.Log($"[ResearchValidation] PASS: {checks} content/selection assertions plus thresholds and saved-session round trip.");
    }

    // Run with Unity -batchmode -executeMethod ResearchVerificationValidation.RunBatch.
    public static void RunBatch()
    {
        try
        {
            ValidateBanks();
            ValidateMissingImages();
            RenderPreview();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateMissingImages()
    {
        var bank = ScriptableObject.CreateInstance<ResearchQuestionBank>();
        var species = new List<SpeciesData>();
        try
        {
            bank.zoneIndex = 4;
            for (int i = 0; i < 12; i++)
            {
                var data = ScriptableObject.CreateInstance<SpeciesData>();
                data.zoneIndex = 4;
                data.speciesId = "test_" + i;
                data.characteristics = "Recorded observation " + i;
                species.Add(data);
                for (int j = 0; j < 2; j++) bank.questions.Add(new ResearchQuestion
                {
                    questionId = $"test_{i}_{j}", species = data, sourceField = ResearchSourceField.Characteristics,
                    prompt = "Which observation is recorded?", choices = new[] { data.characteristics, "Other" },
                    correctChoice = 0, explanation = data.characteristics
                });
                bank.questions.Add(new ResearchQuestion
                {
                    questionId = "camera_" + i, species = data, format = ResearchQuestionFormat.CameraIdentification,
                    sourceField = ResearchSourceField.Characteristics, prompt = "Identify this specimen", explanation = data.characteristics
                });
            }
            var result = ResearchQuestionSelector.Select(bank, _ => true, 42, out string error);
            Require(result != null && result.Count == 13 && result.All(q => q.format == ResearchQuestionFormat.Verification),
                "Missing imagery must fill all 13 slots with verification questions: " + error);
            Debug.Log("[ResearchValidation] Missing-image fallback passed.");
        }
        finally
        {
            foreach (var data in species) UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(bank);
        }
    }

    private static void ValidatePresentation()
    {
        var host = new GameObject("ResearchLayoutValidation");
        try
        {
            var ui = host.AddComponent<ResearchVerificationUI>();
            ui.Show("VERIFY THE RECORD", "SUNLIGHT ZONE   1 / 5", "Question");
            ui.AddSpeciesVisuals(null, null);
            var frames = host.GetComponentsInChildren<HorizontalLayoutGroup>().First(g => g.name == "SpeciesVisuals");
            Require(frames.transform.childCount == 2 && frames.transform.GetChild(0).name == "ModelFrame" &&
                frames.transform.GetChild(1).name == "PhotoFrame", "Model/photo frames must sit side by side in that order.");
            Require(!host.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Caption"),
                "Species visual frames must not show model/photo headers.");
            ui.AddCameraAnswer("Camera specimen", null, null, () => { });
            var cameraAnswer = host.GetComponentsInChildren<Button>().First(b => b.name == "CameraAnswer");
            var cameraRow = cameraAnswer.transform.Find("CameraAnswerRow");
            Require(cameraRow.GetComponent<HorizontalLayoutGroup>() != null &&
                cameraRow.GetChild(0).name == "CameraVisuals" && cameraRow.GetChild(1).name == "LabelContainer",
                "Camera visuals must be on the left and the species text on the right.");
            Require(cameraRow.GetChild(0).GetChild(0).name == "ModelFrame" &&
                cameraRow.GetChild(0).GetChild(1).name == "PhotoFrame",
                "Each camera choice must pair its own model and photograph.");
            Require(cameraRow.GetChild(1).GetComponentInChildren<TMP_Text>().alignment == TextAlignmentOptions.Center,
                "Camera answer text must be centered in the right-hand area.");
            Require(cameraRow.GetChild(0).GetComponent<LayoutElement>().flexibleWidth == 6f &&
                cameraRow.GetChild(1).GetComponent<LayoutElement>().flexibleWidth == 4f &&
                cameraRow.GetComponent<LayoutElement>().preferredHeight == 260f,
                "Camera cards must reserve 60% width for visuals and use the larger height.");
            Require(frames.GetComponent<LayoutElement>().preferredHeight == 320f,
                "Verification visuals must use the larger row height.");
            ui.SetQuestionActions(() => { });
            var actions = host.GetComponentsInChildren<Button>().Where(b => b.name == "Action").ToArray();
            Require(actions.Length == 1 && actions[0].GetComponentInChildren<TMP_Text>().text == "SAVE & RETURN",
                "Unanswered questions must only offer Save & Return.");
            ui.SetQuestionActions(() => { }, () => { });
            actions = host.GetComponentsInChildren<Button>().Where(b => b.name == "Action").ToArray();
            Require(actions.Length == 2 && actions[0].GetComponentInChildren<TMP_Text>().text == "SAVE & RETURN" &&
                actions[1].GetComponentInChildren<TMP_Text>().text == "NEXT", "Answered question footer order is incorrect.");
            Require(actions[0].GetComponent<LayoutElement>().preferredWidth == actions[1].GetComponent<LayoutElement>().preferredWidth,
                "Action button widths must match.");
            Require(actions.All(b => b.GetComponentInChildren<TMP_Text>().alignment == TextAlignmentOptions.Center),
                "Action labels must be centered horizontally and vertically.");
            ui.AddAnswer("A long observation that must wrap within an equally wide answer card.", null, () => { });
            var answer = host.GetComponentsInChildren<Button>().First(b => b.name == "Answer");
            Require(answer.GetComponentInChildren<TMP_Text>().alignment == TextAlignmentOptions.Center,
                "Answer text must be centered.");
            ui.SetProceedAction(() => { });
            actions = host.GetComponentsInChildren<Button>().Where(b => b.name == "Action").ToArray();
            Require(actions.Length == 1 && actions[0].GetComponentInChildren<TMP_Text>().text == "PROCEED" &&
                actions[0].transform.parent.GetComponent<HorizontalLayoutGroup>().childAlignment == TextAnchor.MiddleCenter,
                "Verified research must offer only a centered Proceed button.");
            Debug.Log("[ResearchValidation] Footer ordering, equal widths, centered text and paired visual frames passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void RenderPreview()
    {
        var host = new GameObject("ResearchPreview");
        var cameraGO = new GameObject("ResearchPreviewCamera");
        var target = new RenderTexture(1920, 1080, 24);
        try
        {
            var ui = host.AddComponent<ResearchVerificationUI>();
            var bank = Resources.Load<ResearchQuestionBank>("Research/Zone0Questions");
            var question = bank.questions.First(q => q.format == ResearchQuestionFormat.Verification);
            ui.Show("VERIFY THE RECORD", "SUNLIGHT ZONE   1 / 5", question.prompt);
            ui.AddSpeciesVisuals(ModelPreviewSystem.Instance.GetOrRenderThumbnail(question.species, requireModel: true), question.species.photo);
            foreach (string answer in question.choices) ui.AddAnswer(answer, null, () => { });
            ui.SetQuestionActions(() => { });
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.03f, 0.06f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            camera.targetTexture = target;
            var canvas = host.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            for (int i = 0; i < 4; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in host.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());
            }
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("scratch");
            File.WriteAllBytes("scratch/ResearchVerificationPreview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("[ResearchValidation] UI preview rendered to scratch/ResearchVerificationPreview.png.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(cameraGO);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
