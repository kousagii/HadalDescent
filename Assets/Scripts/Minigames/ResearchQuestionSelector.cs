using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Builds a distinct, species-balanced review from discovered bestiary entries.</summary>
public static class ResearchQuestionSelector
{
    public static readonly int[] QuestionCounts = { 5, 7, 9, 11, 13 };
    public static readonly int[] CameraCounts = { 1, 2, 2, 3, 3 };

    public static int RequiredCorrect(int count) => Mathf.CeilToInt(count * 0.8f);

    public static bool HasCameraVisual(SpeciesData species) => species != null && species.modelPrefab != null;

    public static List<ResearchQuestion> Select(ResearchQuestionBank bank, Func<SpeciesData, bool> discovered,
        int seed, out string error)
    {
        error = null;
        if (bank == null || !ZoneConfig.IsValidZone(bank.zoneIndex))
        {
            error = "The research question bank is missing.";
            return null;
        }

        var random = new System.Random(seed);
        var eligible = bank.questions.Where(q => q != null && q.IsValid() &&
            q.species.zoneIndex == bank.zoneIndex && discovered(q.species))
            .GroupBy(q => q.questionId).Select(g => g.First()).ToList();
        var result = new List<ResearchQuestion>();
        var speciesUse = new Dictionary<string, int>();
        var visualSpecies = eligible.Select(q => q.species).Distinct().Count(HasCameraVisual);
        int cameraCount = visualSpecies >= 2 ? CameraCounts[bank.zoneIndex] : 0;
        var cameras = eligible.Where(q => q.format == ResearchQuestionFormat.CameraIdentification &&
            HasCameraVisual(q.species)).ToList();
        TakeBalanced(cameras, cameraCount, result, speciesUse, random);
        // Missing imagery never produces blank camera choices or an impossible review.
        var verification = eligible.Where(q => q.format == ResearchQuestionFormat.Verification).ToList();
        TakeBalanced(verification, QuestionCounts[bank.zoneIndex] - result.Count, result, speciesUse, random);
        if (result.Count != QuestionCounts[bank.zoneIndex])
        {
            error = $"Not enough valid research questions for captured species ({result.Count}/{QuestionCounts[bank.zoneIndex]}).";
            return null;
        }
        return result.OrderBy(_ => random.Next()).ToList();
    }

    public static bool PrepareSavedCameraQuestions(ResearchQuestionBank bank, List<SpeciesData> captured,
        ResearchReviewSession session, List<ResearchQuestion> questions, out string error)
    {
        error = null;
        int activeIndex = session.correcting
            ? (session.correctionIndex < session.missedIds.Count
                ? questions.FindIndex(q => q.questionId == session.missedIds[session.correctionIndex]) : -1)
            : session.answers.Count;
        bool enoughModels = captured.Count(ResearchQuestionSelector.HasCameraVisual) >= 2;
        for (int i = 0; i < questions.Count; i++)
        {
            var camera = questions[i];
            if (camera.format != ResearchQuestionFormat.CameraIdentification) continue;
            bool substitute = !enoughModels || !ResearchQuestionSelector.HasCameraVisual(camera.species);
            // Photo-era answer indices refer to a different option pool. Re-present only
            // that uncommitted answer; retain the saved score, IDs and completed records.
            if (i == activeIndex && (session.cameraPresentationVersion == 0 || substitute))
                session.pendingAnswer = -1;
            if (!substitute) continue;
            var record = bank.questions.FirstOrDefault(q => q != null && q.IsValid() &&
                q.species == camera.species && q.sourceField == camera.sourceField &&
                q.format == ResearchQuestionFormat.Verification);
            if (record == null) { error = "A saved camera record has no matching verification question."; return false; }
            questions[i] = new ResearchQuestion
            {
                questionId = camera.questionId, species = camera.species,
                format = ResearchQuestionFormat.Verification, sourceField = record.sourceField,
                prompt = record.prompt, choices = record.choices, correctChoice = record.correctChoice,
                explanation = record.explanation
            };
        }
        session.cameraPresentationVersion = 1;
        return true;
    }

    private static void TakeBalanced(List<ResearchQuestion> pool, int count, List<ResearchQuestion> result,
        Dictionary<string, int> speciesUse, System.Random random)
    {
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var question = pool.OrderBy(q => speciesUse.TryGetValue(q.species.speciesId, out int n) ? n : 0)
                .ThenBy(_ => random.Next()).First();
            pool.Remove(question);
            result.Add(question);
            string id = question.species.speciesId;
            speciesUse[id] = speciesUse.TryGetValue(id, out int used) ? used + 1 : 1;
        }
    }
}

/// <summary>Stable question IDs and choices let an interrupted check resume across launches.</summary>
[Serializable]
public class ResearchReviewSession
{
    public int zoneIndex;
    public int seed;
    public List<string> questionIds = new List<string>();
    public List<string> capturedIds = new List<string>();
    public List<int> answers = new List<int>();
    public List<string> missedIds = new List<string>();
    public int correctionIndex;
    public bool correcting;
    public int score;
    public int pendingAnswer = -1;
    public int cameraPresentationVersion;

    public bool Passed => questionIds.Count > 0 && answers.Count == questionIds.Count &&
        score >= ResearchQuestionSelector.RequiredCorrect(questionIds.Count);
    public bool CorrectionsComplete => correcting && answers.Count == questionIds.Count &&
        correctionIndex >= missedIds.Count;

    public void CommitAnswer(int choice, int correctChoice)
    {
        if (correcting)
        {
            if (choice == correctChoice && correctionIndex < missedIds.Count) correctionIndex++;
        }
        else if (answers.Count < questionIds.Count)
        {
            string questionId = questionIds[answers.Count];
            answers.Add(choice);
            if (choice == correctChoice) score++;
            else missedIds.Add(questionId);
        }
        pendingAnswer = -1;
    }
}
