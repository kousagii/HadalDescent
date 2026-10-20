using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Color
    {
        public float a;
        public Color(float r,float g,float b,float a=1) { this.a=a; }
    }
    public struct Quaternion { public static Quaternion Euler(float x,float y,float z) => new Quaternion(); }
    public class Transform
    {
        public Vector3 position, localPosition;
        public Quaternion localRotation;
        public void Rotate(Vector3 axis,float angle) {}
    }
    public class GameObject
    {
        public Transform transform = new Transform();
        public bool activeSelf=true;
        public void SetActive(bool active) { activeSelf=active; }
    }
    public static class Random { public static float value=.5f; }
    public static class Time { public static float time, deltaTime; public static int frameCount; }
    public static class Application { public static bool isPlaying=true; }
    public static class Debug { public static void Log(string message) {} }
    public static class Mathf
    {
        public static float Abs(float n) => Math.Abs(n);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static int Clamp(int n,int a,int b) => Math.Clamp(n,a,b);
        public static float Clamp01(float n) => Math.Clamp(n,0f,1f);
        public static int RoundToInt(float n) => (int)Math.Round(n);
        public static float Clamp(float n,float a,float b) => Math.Clamp(n,a,b);
        public static float Exp(float n) => (float)Math.Exp(n);
        public static float Sin(float n) => (float)Math.Sin(n);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
    }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x; this.y=y; }
        public static Vector2 zero => new Vector2();
    }
}
public class AudioManager
{
    public static AudioManager Instance;
    public void PlaySFX(object clip) {}
    public void PlayPearlCollectSFX() {}
    public void PlayBumpSFX() {}
    public void PlaySubSwipeSFX() {}
}
public class GameManager
{
    public static GameManager Instance;
    public int Added, Deducted;
    public void AddRDP(int n) { Added+=n; }
    public void DeductRDP(int n) { Deducted+=n; }
}
public class HazardDodgeVisual
{
    public bool Paused;
    public void Tick(float seconds,float disturbance=0,float current=0) {}
    public void SetPaused(bool paused) { Paused=paused; }
}
public class Overlay { public Color color; }
public partial class HazardDodgeAtmosphere
{
    private class Pass { public int LastRenderedFrame; }
    private Pass _pass;
    private object _material;
    public void RenderedForTest(int frame)
    {
        _pass=new Pass { LastRenderedFrame=frame };
        _material=new object();
    }
    public void ClearConditions() {}
    public void SetPaused(bool paused) {}
}

public partial class HazardDodgeMinigame
{
    private readonly List<ActiveHazardItem> _activeItems = new List<ActiveHazardItem>();
    public int _currentLane=1, _collectedRdp, _remainingHits=3, _zoneIndex;
    public float laneWidth=7.2f, laneSwitchSpeed=14, _currentX, _targetX, _impactCooldown;
    public readonly HazardDodgeConditions _conditions=new HazardDodgeConditions();
    public float _steeringX, _movementIntensity, _nearDisturbance, submarineYawOffset;
    public HazardDodgeAtmosphere _atmosphere;
    public GameObject _submarineObj;
    public bool _isFinished, _isRunning=true;
    public Overlay _sedimentOverlay = new Overlay();
    private Vector3 _stageOrigin;
    private object customPearlSFX, customBumpSFX, customSwipeSFX;
    private static readonly int[] ZoneBaseBonus = {50,100,150,200,250};
    public int Count => _activeItems.Count;
    public int FinishBanners, Destroyed;
    public HazardDodgeVisual Add(HazardType type,int lane,float z=-1.7f,int direction=1,float intensity=1)
    {
        var visual=new HazardDodgeVisual();
        _activeItems.Add(new ActiveHazardItem { GameObject=new GameObject(), Type=type,
            Lane=lane,ZPos=z,CurrentDirection=direction,Intensity=intensity,Visual=visual });
        return visual;
    }
    public bool Hit(int index) => _activeItems[index].CollectedOrHit;
    public void Step(float speed=10f,float dt=.1f) => UpdateActiveItems(speed,dt);
    public void Feedback(float dt) => UpdateHazardFeedback();
    public void Motion(float dt,float speed=10) => UpdateSubmarineMotion(dt,speed);
    public void Finish(bool success) => FinishRun(success);
    private void Destroy(GameObject obj) { Destroyed++; }
    private void StartCoroutine(IEnumerator routine) {}
    private IEnumerator FlashItemCollection(GameObject obj) { yield break; }
    private IEnumerator ScreenShakeAndImpactFlash() { yield break; }
    private IEnumerator DelayedClose(bool success,int reward) { yield break; }
    private void ShowFinishBanner(bool success,int reward) { FinishBanners++; }
}
