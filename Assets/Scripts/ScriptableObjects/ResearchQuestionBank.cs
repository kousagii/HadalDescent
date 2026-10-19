using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResearchQuestionFormat { Verification, CameraIdentification }
public enum ResearchSourceField { Habitat, Characteristics, EcologicalRole, InterestingFact }

/// <summary>Authored review content kept separate from species and spawning assets.</summary>
[CreateAssetMenu(fileName = "ResearchQuestions", menuName = "HadalDescent/Research Question Bank")]
public class ResearchQuestionBank : ScriptableObject
{
    [Range(0, 4)] public int zoneIndex;
    public List<ResearchQuestion> questions = new List<ResearchQuestion>();
}

[Serializable]
public class ResearchQuestion
{
    public string questionId;
    public SpeciesData species;
    public ResearchQuestionFormat format;
    public ResearchSourceField sourceField;
    [TextArea(2, 5)] public string prompt;
    [Tooltip("Verification choices. Camera questions select captured species instead.")]
    public string[] choices;
    public int correctChoice;
    [TextArea(2, 5)] public string explanation;

    public string SourceText => GetSourceText(species, sourceField);

    public static string GetSourceText(SpeciesData data, ResearchSourceField field)
    {
        if (data == null) return "";
        switch (field)
        {
            case ResearchSourceField.Habitat: return data.habitat;
            case ResearchSourceField.Characteristics: return data.characteristics;
            case ResearchSourceField.EcologicalRole: return data.ecologicalRole;
            default: return data.interestingFact;
        }
    }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(questionId) || species == null ||
            string.IsNullOrWhiteSpace(species.speciesId) || string.IsNullOrWhiteSpace(prompt) ||
            string.IsNullOrWhiteSpace(SourceText) || string.IsNullOrWhiteSpace(explanation)) return false;
        if (format == ResearchQuestionFormat.CameraIdentification) return true;
        if (choices == null || choices.Length < 2 || choices.Length > 4 ||
            correctChoice < 0 || correctChoice >= choices.Length) return false;
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string choice in choices)
            if (string.IsNullOrWhiteSpace(choice) || !unique.Add(choice.Trim())) return false;
        return true;
    }
}
