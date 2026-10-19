using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class Program
{
    private static int _assertions;

    public static void Main(string[] args)
    {
        Check(!ResearchQuestionSelector.HasCameraVisual(new SpeciesData { photo = new UnityEngine.Sprite() }),
            "A photograph alone must not qualify for a model-only camera round");
        Check(!ResearchQuestionSelector.HasCameraVisual(new SpeciesData { fullImage = new UnityEngine.Sprite() }),
            "A secondary photograph must not qualify for a model-only camera round");
        Check(ResearchQuestionSelector.HasCameraVisual(new SpeciesData { modelPrefab = new UnityEngine.GameObject() }),
            "An actual model prefab should qualify for camera rounds");
        using var input = JsonDocument.Parse(File.ReadAllText(args[0]));
        var data = new Dictionary<string, SpeciesData>();
        foreach (var item in input.RootElement.GetProperty("species").EnumerateArray())
        {
            var species = new SpeciesData
            {
                speciesId = Get(item, "speciesId"), commonName = Get(item, "commonName"),
                zoneIndex = item.GetProperty("zoneIndex").GetInt32(), habitat = Get(item, "habitat"),
                characteristics = Get(item, "characteristics"), ecologicalRole = Get(item, "ecologicalRole"),
                interestingFact = Get(item, "interestingFact"),
                photo = item.GetProperty("photo").GetBoolean() ? new UnityEngine.Sprite() : null,
                modelPrefab = item.GetProperty("modelPrefab").GetBoolean() ? new UnityEngine.GameObject() : null
            };
            data.Add(Get(item, "guid"), species);
        }
        foreach (var item in input.RootElement.GetProperty("banks").EnumerateArray())
        {
            var bank = new ResearchQuestionBank { zoneIndex = item.GetProperty("zoneIndex").GetInt32() };
            foreach (var question in item.GetProperty("questions").EnumerateArray()) bank.questions.Add(new ResearchQuestion
            {
                questionId = Get(question, "questionId"), species = data[Get(question, "species")],
                sourceField = (ResearchSourceField)question.GetProperty("sourceField").GetInt32(),
                format = (ResearchQuestionFormat)question.GetProperty("format").GetInt32(),
                prompt = Get(question, "prompt"), explanation = Get(question, "explanation"),
                choices = question.GetProperty("choices").EnumerateArray().Select(c => c.GetString()).ToArray(),
                correctChoice = question.GetProperty("correctChoice").GetInt32()
            });
            foreach (var question in bank.questions)
            {
                Check(question.IsValid(), "Malformed question: " + question.questionId);
                Check(question.species.zoneIndex == bank.zoneIndex, "Cross-zone content");
                if (question.format == ResearchQuestionFormat.Verification)
                    Check(question.choices[question.correctChoice] == question.SourceText, "Answer differs from current bestiary: " + question.questionId);
            }
            var species = data.Values.Where(s => s.zoneIndex == bank.zoneIndex).ToList();
            int minimum = (species.Count + 1) / 2;
            for (int sample = 0; sample < 100; sample++)
            {
                var random = new Random(sample);
                var captured = species.OrderBy(_ => random.Next()).Take(minimum).ToList();
                var selected = ResearchQuestionSelector.Select(bank, captured.Contains, sample, out string error);
                Check(selected != null, error);
                Check(selected.Count == ResearchQuestionSelector.QuestionCounts[bank.zoneIndex], "Wrong review length");
                Check(selected.All(q => captured.Contains(q.species)), "Uncaptured species selected");
                Check(selected.Select(q => q.questionId).Distinct().Count() == selected.Count, "Repeated question");
                var again = ResearchQuestionSelector.Select(bank, captured.Contains, sample, out _);
                Check(selected.Select(q => q.questionId).SequenceEqual(again.Select(q => q.questionId)), "Selection cannot resume deterministically");
                int cameras = captured.Count(ResearchQuestionSelector.HasCameraVisual) >= 2
                    ? ResearchQuestionSelector.CameraCounts[bank.zoneIndex] : 0;
                Check(selected.Count(q => q.format == ResearchQuestionFormat.CameraIdentification) == cameras, "Wrong camera quota");
            }
            // Exercise a completely missing art collection using the same production bank.
            foreach (var entry in species) { entry.photo = null; entry.modelPrefab = null; entry.fullImage = null; }
            var fallback = ResearchQuestionSelector.Select(bank, _ => true, 12, out string fallbackError);
            Check(fallback != null && fallback.Count == ResearchQuestionSelector.QuestionCounts[bank.zoneIndex], fallbackError);
            Check(fallback.All(q => q.format == ResearchQuestionFormat.Verification), "Blank photos must not enter camera rounds");
            var cameraRecord = bank.questions.First(q => q.format == ResearchQuestionFormat.CameraIdentification);
            var savedQuestions = fallback.ToList();
            savedQuestions[1] = cameraRecord;
            var savedSession = new ResearchReviewSession
            {
                zoneIndex = bank.zoneIndex, questionIds = savedQuestions.Select(q => q.questionId).ToList(),
                capturedIds = species.Select(s => s.speciesId).ToList(), answers = new List<int> { 0 },
                score = 1, pendingAnswer = 1
            };
            Check(ResearchQuestionSelector.PrepareSavedCameraQuestions(bank, species, savedSession, savedQuestions, out string migrationError), migrationError);
            Check(savedQuestions[1].format == ResearchQuestionFormat.Verification && savedQuestions[1].questionId == cameraRecord.questionId,
                "Saved camera fallback must preserve the record ID");
            Check(savedSession.questionIds.SequenceEqual(savedQuestions.Select(q => q.questionId)) && savedSession.score == 1 && savedSession.answers.Count == 1,
                "Model-only migration must preserve completed research progress");
            Check(savedSession.pendingAnswer == -1 && savedSession.cameraPresentationVersion == 1,
                "An uncommitted photo-era answer must be presented with the new choices");
            Check(ResearchQuestionSelector.Select(bank, _ => false, 1, out string emptyError) == null && !string.IsNullOrWhiteSpace(emptyError), "Empty collections must report a recoverable error");
            Console.WriteLine($"Zone {bank.zoneIndex}: {bank.questions.Count} questions, 100 captured-collection samples and missing-art fallback passed.");
        }

        int[] passScores = { 4, 6, 8, 9, 11 };
        for (int zone = 0; zone < 5; zone++)
        {
            int count = ResearchQuestionSelector.QuestionCounts[zone];
            Check(ResearchQuestionSelector.RequiredCorrect(count) == passScores[zone], "Wrong pass threshold");
            for (int score = 0; score <= count; score++)
            {
                var session = new ResearchReviewSession
                {
                    zoneIndex = zone, seed = 42,
                    questionIds = Enumerable.Range(0, count).Select(i => "q_" + i).ToList(),
                    capturedIds = new List<string> { "captured" }, cameraPresentationVersion = 1
                };
                for (int i = 0; i < count; i++) session.CommitAnswer(i < score ? 0 : 1, 0);
                Check(session.score == score && session.Passed == (score >= passScores[zone]), "Pass/fail boundary incorrect");
                Check(session.missedIds.Count == count - score, "Missed questions not preserved");
                if (!session.Passed)
                {
                    session.correcting = true;
                    session.CommitAnswer(1, 0);
                    Check(session.correctionIndex == 0 && !session.CorrectionsComplete, "Wrong correction must not advance");
                    for (int i = 0; i < count - score; i++) session.CommitAnswer(0, 0);
                    Check(session.CorrectionsComplete && session.score == score, "Corrections must unlock without overwriting the original score");
                }
                session.pendingAnswer = 2;
                var options = new JsonSerializerOptions { IncludeFields = true };
                var restored = JsonSerializer.Deserialize<ResearchReviewSession>(JsonSerializer.Serialize(session, options), options);
                Check(restored.pendingAnswer == 2 && restored.cameraPresentationVersion == 1 && restored.questionIds.SequenceEqual(session.questionIds) &&
                    restored.missedIds.SequenceEqual(session.missedIds) && restored.correcting == session.correcting,
                    "Session snapshot failed to round-trip");
            }
        }
        Check(ResearchQuestionSelector.Select(null, _ => true, 1, out _) == null, "Missing bank should be recoverable");
        Console.WriteLine($"PASS: {_assertions} assertions against production selection, question validation, scoring and correction code.");
    }

    private static string Get(JsonElement item, string key) => item.GetProperty(key).GetString();
    private static void Check(bool passed, string message)
    {
        _assertions++;
        if (!passed) throw new InvalidOperationException(message);
    }
}
