// Only for the portable logic checks. Unity compiles the real engine types in Assets.
using System;

namespace UnityEngine
{
    public class ScriptableObject { }
    public class Sprite { }
    public class GameObject { }
    public static class Mathf { public static int CeilToInt(float value) => (int)Math.Ceiling(value); }
    public class RangeAttribute : Attribute { public RangeAttribute(int min, int max) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
}

public class SpeciesData
{
    public string speciesId, commonName, habitat, characteristics, ecologicalRole, interestingFact;
    public int zoneIndex;
    public UnityEngine.Sprite photo, fullImage;
    public UnityEngine.GameObject modelPrefab;
}

public static class ZoneConfig
{
    public static bool IsValidZone(int zone) => zone >= 0 && zone < 5;
}
