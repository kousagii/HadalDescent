using System;
using System.Collections.Generic;
using UnityEngine;

// Inject query results; Unity's native box/mesh intersection is a Play Mode check.
namespace UnityEngine
{
    public partial class Transform
    {
        public Transform parent;
        public Quaternion rotation=Quaternion.identity, localRotation=Quaternion.identity;
        public Vector3 lossyScale => localScale;
        public Bounds Geometry;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position=p;rotation=r; }
        public bool IsChildOf(Transform ancestor) => this == ancestor || (parent != null && parent.IsChildOf(ancestor));
    }
    public partial class Rigidbody
    {
        public Quaternion NextRotation;
        public void MoveRotation(Quaternion value) { NextRotation = value; }
    }
    public class GameObject
    {
        public Transform transform = new Transform();
    }
    public class Collider
    {
        public Transform transform = new Transform();
        public Bounds bounds;
        public bool Environment;
        public ScanTarget Target;
        public EnvironmentFactTarget Fact;
        public T GetComponentInParent<T>() where T : class => typeof(T) == typeof(ScanTarget) ? Target as T : Fact as T;
    }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public static Func<Vector3, Vector3, Quaternion, Collider[]> Overlaps = (p, e, r) => Array.Empty<Collider>();
        public static int OverlapBoxNonAlloc(Vector3 p, Vector3 e, Collider[] buffer, Quaternion r, int mask, QueryTriggerInteraction triggers)
        {
            if (mask != ~0 || triggers != QueryTriggerInteraction.Ignore) throw new Exception("Footprint query changed filtering.");
            var hits = Overlaps(p, e, r);
            int count = Math.Min(hits.Length, buffer.Length);
            Array.Copy(hits, buffer, count);
            return count;
        }
    }
}
public class ScanTarget { public SpeciesData Data; }
public class EnvironmentFactTarget { }
public partial class ContextSteering
{
    public static bool IsEnvironmentProp(Collider col) => col.Environment;
}
public partial class MovementHost
{
    private Quaternion _benthicNextRotation;
    private Bounds _movementBounds, _benthicObstacleBounds;
    private float _benthicContactClearance;
    private Func<Vector3,float> _floorSampler;
    private Vector3[] _benthicSupportPoints;
    private readonly Collider[] _benthicOverlapBuffer = new Collider[128];
    private float SampleBenthicFloor(Vector3 point) => TerrainGenerator.Instance.SampleHeight(point.x, point.z);
    public Vector3 ResolveBenthic(Vector3 from, Vector3 to, Quaternion current, Quaternion next, Bounds bounds,
        Vector3[] points, Func<Vector3,float> floor)
    {
        _rb.position=from; _rb.rotation=current; _nextPosition=to; _benthicNextRotation=next;
        _movementBounds=bounds; _benthicObstacleBounds=bounds; _benthicSupportPoints=points;
        _benthicContactClearance=.01f; _floorSampler=floor; _ai=new SpeciesAI();
        FinalizeBenthicStep();
        return _nextPosition;
    }
    public int AvoidanceTurns => _ai.Turns;
}
public static class CreatureGeometry
{
    public static bool TryMeasure(Transform t,Quaternion rotation,out Bounds bounds,out Vector3[] supports)
    { bounds=t.Geometry; supports=null; return bounds.size.sqrMagnitude>0; }
}
public partial class SpawnPlacementHost
{
    private TerrainGenerator terrain;
    private ZoneShape _zoneDef;
    private readonly Collider[] _benthicOverlapBuffer=new Collider[128];
    public class ZoneShape { public float playableDepth=200; }
    public Queue<Vector3> Candidates=new Queue<Vector3>();
    public bool Separated=true;
    public int ContactCalls;
    public Action<GameObject> Ground = go=>{};
    private void PlaceContact(GameObject go,SpeciesData data,Bounds? body,Vector3[] supports) { ContactCalls++; Ground(go); }
    private bool TryFindSpawnPosition(SpeciesData data,out Vector3 p,out Quaternion r)
    { r=Quaternion.identity;p=Candidates.Count>0?Candidates.Dequeue():Vector3.zero;return p.sqrMagnitude>0; }
    private float GetMinSeparationForSpecies(SpeciesData data)=>3.5f;
    private bool IsSeparated(Vector3 p,float separation)=>Separated;
    public bool Place(GameObject go,SpeciesData data)=>TryPlaceBenthicInstance(go,data);
}
public static class BenthicChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        var vertices = new List<Vector3>();
        // Long arms/antennae and uneven feet: preserve the actual low point in each cell.
        for(int x=0;x<4;x++) for(int z=0;z<4;z++)
        {
            vertices.Add(new Vector3(x*2-3, -.3f-.05f*x, z*2-3));
            vertices.Add(new Vector3(x*2-3, 2f, z*2-3));
        }
        Bounds shape=new Bounds(new Vector3(0,.775f,0),new Vector3(6,2.45f,6));
        var supports=BenthicClearance.BuildSupportPoints(vertices,shape);
        Check(supports.Length==16,"Ground samples lost a foot/arm cell.");
        foreach(var point in supports) Check(point.y<0,"Upper shell replaced a measured contact foot.");
        foreach(float scale in new[] { .75f,1f,1.25f })
        for(int yaw=0;yaw<360;yaw+=45)
        foreach(float pitch in new[] { -18f,0f,18f })
        {
            var scaled=Array.ConvertAll(supports,p=>p*scale);
            var rotation=Quaternion.Euler(pitch,yaw,0);
            Func<Vector3,float> floor=p=>-100+.15f*p.x-.12f*p.z;
            float y=BenthicClearance.MinimumRootY(new Vector3(0,-100,0),rotation,shape,.01f*scale,floor,scaled);
            foreach(var foot in scaled)
            {
                var world=new Vector3(0,y,0)+rotation*foot;
                Check(world.y>=floor(world)+.01f*scale-.0001f,"Crawler foot buried after scaling/turning on a slope.");
            }
        }

        var parent=new Transform(); var owner=new Transform {parent=parent};
        var own=new Collider {transform=new Transform {parent=owner}, Environment=true};
        var seabed=new Collider();
        var mobile=new Collider {Target=new ScanTarget {Data=new SpeciesData()}};
        var prop=new Collider {Environment=true, transform=new Transform {parent=parent}, bounds=new Bounds(new Vector3(3,0,0),Vector3.one)};
        var stationary=new Collider {Target=new ScanTarget {Data=new SpeciesData {isStationary=true}}};
        var fact=new Collider {Fact=new EnvironmentFactTarget()};
        var buffer=new Collider[8];
        Physics.Overlaps=(p,e,r)=>new[] {own,seabed,mobile};
        Check(!BenthicPlacement.IsBlocked(Vector3.zero,Quaternion.identity,shape,owner,buffer,out _),"Own mesh or bare seabed blocks placement.");
        foreach(var obstacle in new[] {prop,stationary,fact})
        {
            Physics.Overlaps=(p,e,r)=>new[] {obstacle};
            Check(BenthicPlacement.IsBlocked(Vector3.zero,Quaternion.identity,shape,owner,buffer,out _),"Solid prop/stationary species was ignored, including a sibling under a shared root.");
        }
        Physics.Overlaps=(p,e,r)=>new[] {seabed,seabed};
        Check(BenthicPlacement.IsBlocked(Vector3.zero,Quaternion.identity,shape,owner,new Collider[2],out _),"Truncated overlap results accepted as safe.");
        Physics.Overlaps=(p,e,r)=>
        {
            var expected=new Vector3(10,0,5)+r*shape.center;
            Check((p-expected).magnitude<.001f,"Final root position/rotation was not applied to visual footprint center.");
            Check(e.x>=3.04f && e.z>=3.04f,"Footprint shrank to a root-centered probe.");
            return Array.Empty<Collider>();
        };
        BenthicPlacement.IsBlocked(new Vector3(10,0,5),Quaternion.Euler(0,90,0),shape,owner,buffer,out _);

        Bounds body=new Bounds(Vector3.zero,new Vector3(1,1,1));
        Vector3[] feet={new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f)};
        var host=new MovementHost();
        Physics.Overlaps=(p,e,r)=>Array.Empty<Collider>();
        var placed=host.ResolveBenthic(new Vector3(0,-99.49f,0),new Vector3(2,-101,0),Quaternion.identity,Quaternion.identity,body,feet,p=>-100+p.x*.3f);
        foreach(var foot in feet) Check((placed+foot).y>=-100+(placed+foot).x*.3f+.0099f,"Production final step failed immediate uphill clearance.");
        var descending=host.ResolveBenthic(new Vector3(0,-95,0),new Vector3(0,-95.1f,0),Quaternion.identity,Quaternion.identity,body,feet,p=>-100);
        Check(MathF.Abs(descending.y+95.1f)<.001f,"Floor safety forced an abrupt downward snap.");
        Physics.Overlaps=(p,e,r)=>p.x>1 ? new[] {prop} : Array.Empty<Collider>();
        placed=host.ResolveBenthic(new Vector3(0,-99.49f,0),new Vector3(2,-99.49f,0),Quaternion.identity,Quaternion.identity,body,feet,p=>-100);
        Check(placed.x==0 && host.AvoidanceTurns==1,"Production follower entered a prop instead of cancelling and rerouting.");
        Physics.Overlaps=(p,e,r)=>p.x>.9f && p.x<1.1f ? new[] {prop} : Array.Empty<Collider>();
        placed=host.ResolveBenthic(new Vector3(0,-99.49f,0),new Vector3(2,-99.49f,0),Quaternion.identity,Quaternion.identity,body,feet,p=>-100);
        Check(placed.x==0,"Mid-step obstacle was skipped.");
        Physics.Overlaps=(p,e,r)=>(r*Vector3.forward).x>.1f ? new[] {prop} : Array.Empty<Collider>();
        host.ResolveBenthic(new Vector3(0,-99.49f,0),new Vector3(0,-99.49f,0),Quaternion.identity,Quaternion.Euler(0,90,0),body,feet,p=>-100);
        Check(MathF.Abs((host._rb.NextRotation*Vector3.forward).x)<.001f,"Turn in place rotated a limb into a prop.");
        Physics.Overlaps=(p,e,r)=>Array.Empty<Collider>();
        // Execute the actual final placement loop: even permissive upstream
        // fallbacks must pass the final, shifted/scaled visual-footprint query.
        var specimen=new GameObject(); specimen.transform.Geometry=shape;
        var placement=new SpawnPlacementHost();
        placement.Ground=go=>go.transform.position+=new Vector3(1,0,0);
        placement.Candidates.Enqueue(new Vector3(10,0,0));
        Physics.Overlaps=(p,e,r)=>p.x<5 ? new[] {prop} : Array.Empty<Collider>();
        Check(placement.Place(specimen,new SpeciesData()),"Final placement did not retry an intersecting candidate.");
        Check(placement.ContactCalls==2 && specimen.transform.position.x==11,"Final grounding shift was not revalidated on retry.");
        placement=new SpawnPlacementHost();
        for(int i=0;i<20;i++) placement.Candidates.Enqueue(new Vector3(i+10,0,0));
        Physics.Overlaps=(p,e,r)=>new[] {prop};
        Check(!placement.Place(specimen,new SpeciesData()),"Exhausted prop clearance forced an unsafe fallback spawn.");
        Check(placement.ContactCalls==12,"Failed placement was not bounded.");
        placement=new SpawnPlacementHost {Separated=false};
        placement.Candidates.Enqueue(new Vector3(50,0,0));
        Physics.Overlaps=(p,e,r)=>Array.Empty<Collider>();
        Check(!placement.Place(specimen,new SpeciesData()),"Fallback bypassed final species separation.");
        Console.WriteLine($"PASS: {checks} crawler support, footprint filtering, slope and production final-step assertions (queries substituted).");
    }
}
