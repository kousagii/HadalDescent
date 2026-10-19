using UnityEngine;

/// <summary>Build-safe defaults for cleanup art; scene prefab overrides still take precedence.</summary>
[CreateAssetMenu(fileName = "CleanupModelLibrary", menuName = "HadalDescent/Cleanup Model Library")]
public class CleanupModelLibrary : ScriptableObject
{
    public GameObject waterBottle;
    public GameObject plasticBag;
    public GameObject sixPackRings;
    public GameObject sodaCan;
    public GameObject metalPipe;
    public GameObject metalTube;
    public GameObject metalDrum;
    public GameObject aaBattery;
    public GameObject electronicBoard;
    public GameObject submarineBatteryCell;
    public GameObject plasticBin;
    public GameObject metalBin;
    public GameObject hazardBin;
}
