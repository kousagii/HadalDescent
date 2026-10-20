using UnityEngine;

/// <summary>References existing art so hazard visuals are included in player builds.</summary>
[CreateAssetMenu(menuName = "HadalDescent/Hazard Dodge Visual Assets")]
public class HazardDodgeVisualAssets : ScriptableObject
{
    public GameObject[] rocks;
    public GameObject chimney;
    public Material particles;
    public Material underwater;
}
