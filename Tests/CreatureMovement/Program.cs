using System;
using UnityEngine;

// Stand-ins model deferred MovePosition calls: only the last request is committed.
// This catches propulsion being erased by a later correction from the old pose.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 right => new Vector3(1,0,0);
        public float sqrMagnitude => x*x+y*y+z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude < 0.00001f ? zero : this * (1f/magnitude);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator -(Vector3 a) => a*(-1);
        public static Vector3 operator -(Vector3 a,Vector3 b) => a+b*(-1);
        public static float Dot(Vector3 a,Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n) => v-n*(Dot(v,n)/n.sqrMagnitude);
        public static Vector3 Scale(Vector3 a,Vector3 b) => new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public void Normalize() { this=normalized; }
        public static float Distance(Vector3 a,Vector3 b) => (a-b).magnitude;
        public static Vector3 Reflect(Vector3 a,Vector3 normal) => a-normal*(2*Dot(a,normal));
        public static Vector3 Slerp(Vector3 a,Vector3 b,float t)
        {
            if(a.sqrMagnitude<.00001f || b.sqrMagnitude<.00001f) return Lerp(a,b,t);
            a=a.normalized;b=b.normalized;
            float dot=MathF.Max(-1,MathF.Min(1,Dot(a,b)));
            if(dot>.9995f) return Lerp(a,b,t).normalized;
            Vector3 tangent=b-a*dot;
            if(tangent.sqrMagnitude<.00001f) tangent=new Vector3(-a.z,0,a.x);
            float angle=MathF.Acos(dot)*t;
            return a*MathF.Cos(angle)+tangent.normalized*MathF.Sin(angle);
        }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a*(1-t)+b*t;
    }
    public struct Quaternion
    {
        private System.Numerics.Quaternion q;
        private Quaternion(System.Numerics.Quaternion value) { q=value; }
        public static Quaternion LookRotation(Vector3 dir,Vector3 up) => throw new Exception("Unexpected manual rotation in AI test.");
        public static Quaternion identity => new Quaternion(System.Numerics.Quaternion.Identity);
        public static Quaternion Euler(float x,float y,float z) => new Quaternion(System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t) => new Quaternion(System.Numerics.Quaternion.Slerp(a.q,b.q,t));
        public static float Angle(Quaternion a,Quaternion b) => 2f*MathF.Acos(MathF.Min(1,MathF.Abs(System.Numerics.Quaternion.Dot(a.q,b.q))))*180f/MathF.PI;
        public static Quaternion operator *(Quaternion a,Quaternion b) => new Quaternion(a.q*b.q);
        public static Vector3 operator *(Quaternion a,Vector3 b)
        {
            var v=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(b.x,b.y,b.z),a.q);
            return new Vector3(v.X,v.Y,v.Z);
        }
    }
    public struct Bounds
    {
        public Vector3 center,size;
        public Vector3 extents => size*0.5f;
        public Vector3 min => center-extents;
        public Vector3 max => center+extents;
        public Bounds(Vector3 c,Vector3 s) { center=c;size=s; }
        public void Encapsulate(Vector3 p)
        {
            var low=min;var high=max;
            low=new Vector3(MathF.Min(low.x,p.x),MathF.Min(low.y,p.y),MathF.Min(low.z,p.z));
            high=new Vector3(MathF.Max(high.x,p.x),MathF.Max(high.y,p.y),MathF.Max(high.z,p.z));
            center=(low+high)*.5f;size=high-low;
        }
    }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public const float Rad2Deg = 180f/MathF.PI;
        public static float Atan2(float y,float x) => MathF.Atan2(y,x);
        public static float Min(float a, float b) => MathF.Min(a,b);
        public static float Max(float a, float b) => MathF.Max(a,b);
        public static float Clamp(float x, float a, float b) => MathF.Max(a, MathF.Min(x,b));
        public static float Lerp(float a, float b, float t) => a+(b-a)*Clamp(t,0,1);
        public static float Sqrt(float x) => MathF.Sqrt(x);
        public static float Sin(float x) => MathF.Sin(x);
        public static float Abs(float x) => MathF.Abs(x);
        public static float Clamp01(float x) => Clamp(x,0,1);
        public static float Exp(float x) => MathF.Exp(x);
        public static bool Approximately(float a, float b) => MathF.Abs(a-b) < 0.00001f;
        public static float Sign(float x) => x < 0 ? -1 : 1;
    }
    public static class Time { public static float fixedDeltaTime = 0.02f, deltaTime=.02f, time; }
    public partial class Transform
    {
        public Vector3 position;
        public Vector3 forward = new Vector3(1,0,0), localScale = new Vector3(1,1,1);
    }
    public class Prefab { public Transform transform = new Transform(); }
    public class Animator { public float speed; }
    public partial class Rigidbody
    {
        public Vector3 position, Next;
        public Quaternion rotation=Quaternion.Euler(0,90,0);
        public Vector3 linearVelocity;
        public int Requests;
        public void MovePosition(Vector3 value) { Next=value; Requests++; }
        public void Commit() { position=Next; Requests=0; }
    }
}
public enum LocomotionArchetype { PelagicCruiser, HoverBurst, PulsatileJetter, BenthicFollower, SurfaceDrifter, Serpentine, Sessile }
public enum TaxonomicClass { Cephalopoda, Scyphozoa, Chondrichthyes, Malacostraca, Other }
public class SpeciesData
{
    public Prefab modelPrefab;
    public string speciesId="test";
    public bool isShy;
    public float fleeRange=10f,returnThreshold=40f,fleeSpeed=4.5f,wanderRadius=20f;
    public float pulseFrequency=0.8f, pulseDutyCycle=0.35f, moveSpeed=3f;
    public float modelYawOffset,minCruiseSpeedFraction, groundContactClearance=.01f, benthicSurfaceOffset=.4f;
    public Vector3[] benthicSupportPoints;
    public bool isStationary;
    public LocomotionArchetype locomotionArchetype=LocomotionArchetype.PulsatileJetter;
    public TaxonomicClass taxonomicClass=TaxonomicClass.Cephalopoda;
}
public partial class SpeciesAI
{
    public SpeciesData Data;
    public int Turns => _steering.GoalWrites;
}
public partial class ContextSteering
{
    public Vector3 LastSteerDir = new Vector3(1,0,0);
    public float MoveSpeed=3f, SafeHalfWidth=240f, SafeHalfLength=240f, SafeTopY=-3.5f, SafeBottomY=-180f;
    public float ModelYawOffset;
}
public class TerrainGenerator
{
    public static TerrainGenerator Instance = new TerrainGenerator();
    public bool HasGenerated=true;
    public float Floor=-200f;
    public float SampleHeight(float x, float z) => Floor;
}
public partial class MovementHost
{
    public Rigidbody _rb = new Rigidbody();
    public SpeciesData _data = new SpeciesData();
    public ContextSteering _steering = new ContextSteering();
    private SpeciesAI _ai;
    private float _cycleTimer, _currentSpeed, _bottomToPivot, _bodyLength=2f, _nextBoundaryTurnCooldown;
    private Vector3 _nextPosition, _baseModelScale;
    private Transform _visualModel;
    private Animator _animator;
    public Transform transform = new Transform();
    private static T FindFirstObjectByType<T>() where T: class => TerrainGenerator.Instance as T;
    private void UpdateBankingAndYaw() { }
    private void ApplySteeringRotation(Vector3 dir, float pitch, bool full3D, float maxPitch=0f,float turnBoost=1f) { }
    private void DeflectSurfaceDrifter(Vector3 direction) { }
    private void UpdateHoverBurst(Vector3 d, float s) => UpdatePelagicCruiser(d,s);
    private void UpdateBenthicFollower(Vector3 d, float s) => UpdatePelagicCruiser(d,s);
    private void UpdateSurfaceDrifter() => _nextPosition = _rb.position;
    private void UpdateSerpentine(Vector3 d, float s) => UpdatePelagicCruiser(d,s);
    public void Tick() => FixedUpdate();
}
public static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        var host = new MovementHost();
        host._rb.position = new Vector3(0,-181,0);
        host.Tick();
        Check(host._rb.Requests == 1, "Propulsion and correction must submit one movement request.");
        Check(host._rb.Next.x > 0f, "Depth correction erased horizontal squid progress.");
        Check(host._rb.Next.y == -180f, "Depth floor was not enforced on proposed position.");
        host._rb.Commit();
        float travelled=0;
        for (int i=0;i<1500;i++)
        {
            float previous=host._rb.position.x;
            host.Tick();
            Check(host._rb.Requests == 1, "Multiple movement requests in jet cycle.");
            Check(host._rb.Next.x > previous, "Squid stalled during thrust/glide near lower limit.");
            travelled+=host._rb.Next.x-previous;
            host._rb.Commit(); Time.time+=Time.fixedDeltaTime;
        }
        Check(travelled > 20f, "Squid does not explore over multiple pulse cycles.");

        host = new MovementHost();
        host._rb.position = new Vector3(239.999f,-60f,0);
        host._steering.LastSteerDir = new Vector3(1,0,1).normalized;
        host.Tick();
        Check(host._rb.Next.x <= 240f, "Proposed step crossed perimeter.");
        Check(host._rb.Next.z > 0f, "Perimeter correction erased tangent movement.");

        TerrainGenerator.Instance.Floor=-50f;
        host = new MovementHost();
        host._steering.SafeTopY=-70f;
        host._steering.SafeBottomY=-160f;
        host._rb.position = new Vector3(0,-70f,0);
        for (int i=0;i<300;i++)
        {
            host.Tick();
            Check(host._rb.Next.y == -47f, "Conflicting floor/depth bounds caused oscillation or terrain penetration.");
            Check(host._rb.Next.x > host._rb.position.x, "Conflicting bounds erased escape progress.");
            host._rb.Commit();
        }
        for (int i=0;i<1000;i++)
        {
            float low=-i*0.3f, high=-3f;
            float y=CreatureMotionBounds.ClampDepth(-180f,low,high);
            float feasibleHigh = MathF.Min(-3f,MathF.Max(low,high));
            Check(y <= feasibleHigh && y >= MathF.Min(low,feasibleHigh), "Depth projection outside feasible interval.");
            Check(CreatureMotionBounds.ClampDepth(y,low,high) == y, "Depth correction is not stable.");
        }
        TerrainGenerator.Instance.Floor=-200;
        foreach(float offset in new[] { -90f,90f,0f,180f })
        for(int yaw=0;yaw<360;yaw+=30)
        foreach(float bank in new[] { -20f,0f,20f })
        {
            Quaternion travel=Quaternion.Euler(0,yaw,bank);
            Quaternion model=CreatureFacing.ModelRotation(travel,offset);
            Vector3 heading=travel*Vector3.forward;
            Check(Vector3.Dot(CreatureFacing.Forward(model,offset),heading)>.9999f,"Facing conversion changes travel direction.");
            Vector3 nose=offset==-90 ? Vector3.right : offset==90 ? Vector3.right*(-1) : offset==180 ? Vector3.forward*(-1) : Vector3.forward;
            Check(Vector3.Dot(model*nose,heading)>.9999f,"Model nose does not follow travel heading.");
            host=new MovementHost();host._data.locomotionArchetype=LocomotionArchetype.PelagicCruiser;
            host._steering.ModelYawOffset=offset;host._data.modelYawOffset=offset;
            host._rb.rotation=model;host._rb.position=new Vector3(0,-60,0);host._steering.LastSteerDir=heading;
            host.Tick();
            Vector3 step=host._rb.Next-host._rb.position;
            Check(Vector3.Dot(step.normalized,model*nose)>.9999f,"Production cruiser thrust moves sideways after facing correction.");
        }
        Bounds sideModel=new Bounds(Vector3.zero,new Vector3(6,1,1));
        Bounds aligned=CreatureFacing.MovementBounds(sideModel,90);
        Check(MathF.Abs(aligned.size.z-6)<.0001f && MathF.Abs(aligned.size.x-1)<.0001f,"Sensors still measure a sideways model's length as its width.");

        foreach(float scale in new[] { .75f,1f,1.25f })
        for(int yaw=0;yaw<360;yaw+=45)
        foreach(float pitch in new[] { -20f,0f,20f })
        foreach(float roll in new[] { -15f,0f,15f })
        {
            Bounds underside=new Bounds(new Vector3(0,(-1.32f+.524f)*.5f,-2.23f)*scale,new Vector3(6.364f,1.844f,10.412f)*scale);
            Quaternion rotation=Quaternion.Euler(pitch,yaw,roll);
            Vector3 position=new Vector3(10,-100,-20);
            Func<Vector3,float> floor=p => -100+.10f*p.x-.20f*p.z;
            float clearance=.4f*scale;
            Vector3[] support=Array.ConvertAll(RaySupportProfile.Points,p=>p*scale);
            float y=BenthicClearance.MinimumRootY(position,rotation,underside,clearance,floor,support);
            for(int i=0;i<5;i++)
            {
                Vector3 p=support[i];
                Vector3 relative=rotation*p;Vector3 point=new Vector3(position.x,y,position.z)+relative;
                Check(point.y>=floor(point)+clearance-.0001f,"Ray underside penetrates slope during yaw/pitch/roll or scaling.");
            }
        }
        Bounds rayBounds=new Bounds(new Vector3(0,-.398f,-2.23f),new Vector3(6.364f,1.844f,10.412f));
        float flat=BenthicClearance.MinimumRootY(Vector3.zero,Quaternion.identity,rayBounds,.4f,p=>-100);
        Check(MathF.Abs(flat-(-98.28f))<.001f,"Animated fin allowance was not included.");
        float wing=BenthicClearance.MinimumRootY(Vector3.zero,Quaternion.identity,rayBounds,.4f,p=>p.x>3 ? -98 : -100);
        Check(wing>flat+1.9f,"Raised ground beneath a wing was missed by center-only clearance.");
        Func<Vector3,float> wingBump=p=>p.x>3 && MathF.Abs(p.z+.13886f)<.1f ? -98 : -100;
        float authored=BenthicClearance.MinimumRootY(Vector3.zero,Quaternion.identity,rayBounds,.4f,wingBump,RaySupportProfile.Points);
        float envelopeOnly=BenthicClearance.MinimumRootY(Vector3.zero,Quaternion.identity,rayBounds,.4f,wingBump);
        Check(authored>envelopeOnly+1.9f,"Authored wing location was replaced with the long-tail bounding-box center.");
        BenthicChecks.Run();
        FleeChecks.Run();
        Console.WriteLine($"PASS: {checks} movement, facing and ray-clearance assertions. Native skinning, contacts and animation require Unity checks.");
    }
}
