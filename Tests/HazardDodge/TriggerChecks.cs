using System;
using System.Collections;
using UnityEngine;

public class ZoneManager { public static int CurrentZoneIndex; }
public class ZoneBoundaryTrigger { public static float _globalCooldownTimer; }
public class CinematicSequencer { public static CinematicSequencer Instance; public bool IsPlaying; }
public class TutorialManager
{
    public enum TutorialPhase { Other, Step7_HazardEncounter }
    public static TutorialManager Instance;
    public bool IsTutorialActive;
    public TutorialPhase CurrentPhase;
    public void OnHazardCompleted(bool survived) {}
}
public class UIManager
{
    public static bool ModalOpen;
    public static bool IsAnyPanelOrModalOpen(bool includeMinigames=true) => ModalOpen ||
        (includeMinigames && MinigameManager.Instance != null && MinigameManager.Instance.IsMinigameActive);
}
public class MinigameManager
{
    public static MinigameManager Instance;
    public bool IsMinigameActive;
    public int Launches;
    public void TriggerHazardDodgeMinigame(int zone,Action<int> onSuccess,Action onFail)
    {
        Launches++;
        IsMinigameActive=true;
    }
}
public class Marker { public bool enabled=true; }
public partial class HazardZoneTrigger
{
    public float sceneStartGracePeriod=25, _sceneStartTime, _lastTriggerTime, _periodicTimer;
    public bool enablePeriodicSurge=true, enableSpatialProximity=true, _isTriggering;
    public int zoneIndex;
    public GameObject gameObject=new GameObject();
    public Marker _collider=new Marker(), _sonarTrackable=new Marker();
    public IEnumerator Schedule() => PeriodicSurgeRoutine();
    public void ManualTest() => TestTriggerHazardMinigameNow();
}
public static class TriggerChecks
{
    private static int checks;
    private static void Check(bool condition,string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static HazardZoneTrigger Reset(int zone)
    {
        Time.time=0;
        Time.deltaTime=1;
        UnityEngine.Random.value=.5f;
        Application.isPlaying=true;
        ZoneManager.CurrentZoneIndex=zone;
        ZoneBoundaryTrigger._globalCooldownTimer=0;
        MinigameManager.Instance=new MinigameManager();
        TutorialManager.Instance=null;
        CinematicSequencer.Instance=null;
        UIManager.ModalOpen=false;
        return new HazardZoneTrigger();
    }
    private static void Advance(IEnumerator routine,int seconds)
    {
        for (int i=0;i<seconds;i++) { Time.time++; routine.MoveNext(); }
    }
    public static void Run()
    {
        for (int zone=0;zone<5;zone++)
        {
            float min=zone<2 ? 120 : 60;
            Check(HazardDodgeRules.SurgeIntervalSeconds(zone,0)==min,"Interval lower bound wrong");
            Check(HazardDodgeRules.SurgeIntervalSeconds(zone,1)==min+60,"Interval upper bound wrong");
            var trigger=Reset(zone);
            var routine=trigger.Schedule();
            int expected=zone<2 ? 150 : 90;
            Advance(routine,expected-1);
            Check(MinigameManager.Instance.Launches==0,"Encounter fired early");
            Advance(routine,1);
            Check(MinigameManager.Instance.Launches==1,"Encounter interval includes extra startup delay");
            Check(trigger.gameObject.activeSelf && !trigger.enableSpatialProximity,"Periodic manager retired after first encounter");
            Check(trigger._periodicTimer==expected,"Encounter did not restart countdown");
            Advance(routine,100);
            Check(trigger._periodicTimer==expected,"Active encounter consumed the next interval");
            MinigameManager.Instance.IsMinigameActive=false;
            Advance(routine,expected);
            Check(MinigameManager.Instance.Launches==2,"Second encounter never fired");
        }
        {
            var trigger=Reset(0);
            var routine=trigger.Schedule();
            Advance(routine,20);
            float remaining=trigger._periodicTimer;
            UIManager.ModalOpen=true;
            Advance(routine,300);
            Check(trigger._periodicTimer==remaining,"Menu time consumed exploration interval");
            UIManager.ModalOpen=false;
            TutorialManager.Instance=new TutorialManager { IsTutorialActive=true };
            Advance(routine,100);
            Check(trigger._periodicTimer==remaining,"Tutorial time consumed exploration interval");
            trigger.DeactivateHazard();
            Check(trigger.gameObject.activeSelf && trigger._periodicTimer==150,"Tutorial retired periodic manager");
            TutorialManager.Instance=null;
            CinematicSequencer.Instance=new CinematicSequencer { IsPlaying=true };
            Advance(routine,100);
            Check(trigger._periodicTimer==150,"Cinematic time consumed exploration interval");
            CinematicSequencer.Instance=null;
            ZoneBoundaryTrigger._globalCooldownTimer=3;
            Advance(routine,10);
            Check(trigger._periodicTimer==150,"Zone transition consumed interval");
            ZoneBoundaryTrigger._globalCooldownTimer=0;
            Advance(routine,150);
            Check(MinigameManager.Instance.Launches==1,"Encounter never resumed after blockers closed");
        }
        {
            var trigger=Reset(2);
            trigger.TriggerHazardMinigame();
            Check(MinigameManager.Instance.Launches==0,"Normal trigger bypassed startup grace");
            trigger.ManualTest();
            Check(MinigameManager.Instance.Launches==1,"Inspector test did not skip startup grace");
            trigger.ManualTest();
            Check(MinigameManager.Instance.Launches==1,"Inspector test duplicated active activity");
        }
        {
            var trigger=Reset(0);
            UIManager.ModalOpen=true;
            trigger.ManualTest();
            Check(MinigameManager.Instance.Launches==0,"Inspector test interrupted modal");
            UIManager.ModalOpen=false;
            TutorialManager.Instance=new TutorialManager { IsTutorialActive=true };
            trigger.ManualTest();
            Check(MinigameManager.Instance.Launches==0,"Inspector test interrupted tutorial");
            TutorialManager.Instance=null;
            Application.isPlaying=false;
            trigger.ManualTest();
            Check(MinigameManager.Instance.Launches==0,"Inspector test ran outside Play Mode");
        }
        {
            var trigger=Reset(1);
            trigger.enablePeriodicSurge=false;
            trigger.DeactivateHazard();
            Check(!trigger.gameObject.activeSelf && !trigger._collider.enabled && !trigger._sonarTrackable.enabled,
                "Spatial-only hazard no longer retires");
        }
        Console.WriteLine($"Hazard automatic-trigger checks passed: {checks} assertions.");
    }
}
