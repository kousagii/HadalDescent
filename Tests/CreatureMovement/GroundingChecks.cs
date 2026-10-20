using System;
using System.Reflection;
using UnityEngine;

namespace UnityEngine
{
    public class DisallowMultipleComponent : Attribute { }
    public class SerializeField : Attribute { }
    public class Tooltip : Attribute { public Tooltip(string text) { } }
    public class Min : Attribute { public Min(float value) { } }
    public class MonoBehaviour { public bool enabled=true; public Transform transform = new Transform(); }
    public static class Mathf { public static float Max(float a,float b) => MathF.Max(a,b); }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 down => new Vector3(0,-1,0);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public class Transform
    {
        public Transform Parent;
        public Vector3 Offset;
        private Vector3 _position;
        public Vector3 position
        {
            get => Parent != null ? Parent.position+Offset : _position;
            set { if(Parent != null) Offset=value-Parent.position; else _position=value; }
        }
        public bool IsChildOf(Transform root) => this == root || (Parent != null && Parent.IsChildOf(root));
    }
    public struct Ray { public Vector3 origin,direction; public Ray(Vector3 o,Vector3 d) { origin=o; direction=d; } }
    public struct Bounds { public Vector3 max,size; }
    public struct RaycastHit { public Vector3 point,normal; }
    public class MeshCollider
    {
        public bool enabled=true,isTrigger,Miss,InvalidNormal;
        public object sharedMesh=new object();
        public Bounds bounds=new Bounds { max=new Vector3(500,-50,500),size=new Vector3(1000,200,1000) };
        public Func<float,float,float> Height = (x,z) => -100f;
        public int RayCalls;
        public bool Raycast(Ray ray,out RaycastHit hit,float distance)
        {
            RayCalls++;
            float y=Height(ray.origin.x,ray.origin.z);
            if(ray.direction.y != -1 || ray.origin.y < y || ray.origin.y-distance > y) throw new Exception("Ray does not cover the seabed.");
            hit=new RaycastHit { point=new Vector3(ray.origin.x,y,ray.origin.z),normal=InvalidNormal ? new Vector3(1,0,0) : Vector3.up };
            return !Miss;
        }
    }
}
public class TerrainGenerator
{
    public float AnalyticHeight=-99;
    public OceanFloorMeshGenerator Floor=new OceanFloorMeshGenerator();
    public float SampleHeight(float x,float z) => AnalyticHeight;
    public T GetComponent<T>() where T:class => Floor as T;
}
public class OceanFloorMeshGenerator
{
    public MeshCollider Collider=new MeshCollider();
    public T GetComponent<T>() where T:class => Collider as T;
}
public static class GroundingChecks
{
    private static int checks;
    private static void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
    private static bool Near(float a,float b) => MathF.Abs(a-b)<0.0001f;
    private static CreatureGroundContact Create(Vector3 root,Vector3 offset)
    {
        var contact=new CreatureGroundContact();
        contact.transform.position=root;
        var point=new Transform { Parent=contact.transform,Offset=offset };
        typeof(CreatureGroundContact).GetField("contactPoint",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(contact,point);
        return contact;
    }
    public static void Main()
    {
        var terrain=new TerrainGenerator();
        foreach(float scale in new[] { 1.875f,2.5f,3.125f })
        for(int yaw=0;yaw<360;yaw+=30)
        {
            float r=yaw*MathF.PI/180;
            float x=-0.006163f*scale,z=-0.000075f*scale;
            var offset=new Vector3(x*MathF.Cos(r)+z*MathF.Sin(r),-0.270217f*scale,-x*MathF.Sin(r)+z*MathF.Cos(r));
            var contact=Create(new Vector3(25,-80,-47),offset);
            Check(contact.TryPlace(terrain,-200),"Configured anchor was not placed.");
            Check(Near(contact.ContactPoint.position.y,-100.05f),"Analytical height overrode physical seabed, or embed scaled with specimen.");
            Check(Near(contact.transform.position.x,25) && Near(contact.transform.position.z,-47),"Spawn footprint changed.");
            float y=contact.transform.position.y;
            contact.TryPlace(terrain,-200);
            Check(Near(y,contact.transform.position.y),"Repeated placement drifts.");
        }
        Check(terrain.Floor.Collider.RayCalls==72,"Expected direct queries of the generated floor only.");
        terrain.Floor.Collider.Height=(x,z) => -100+0.1f*x-0.2f*z;
        var slope=Create(new Vector3(25,-80,-47),new Vector3(0.3f,-0.6f,0.1f));
        slope.TryPlace(terrain,-200);
        Check(Near(slope.ContactPoint.position.y,terrain.Floor.Collider.Height(25.3f,-46.9f)-0.05f),"Slope sampled below root instead of trunk.");
        terrain.Floor.Collider.Miss=true;
        slope.TryPlace(terrain,-200);
        Check(Near(slope.ContactPoint.position.y,-99.05f),"Missing mesh hit does not use configured terrain fallback.");
        terrain.Floor.Collider.Miss=false;
        terrain.Floor.Collider.InvalidNormal=true;
        slope.TryPlace(terrain,-200);
        Check(Near(slope.ContactPoint.position.y,-99.05f),"Vertical surface accepted as ground.");
        slope.TryPlace(null,-200);
        Check(Near(slope.ContactPoint.position.y,-200.05f),"No-terrain preview fallback failed.");
        var empty=new CreatureGroundContact();
        Check(!empty.TryPlace(terrain,-200),"Missing anchor must retain old bounds fallback.");
        var external=Create(new Vector3(),new Vector3());
        external.ContactPoint.Parent=null;
        Check(!external.TryPlace(terrain,-200),"External transform must not be an attachment point.");
        slope.enabled=false;
        Check(!slope.TryPlace(terrain,-200),"Disabled grounding override was used.");
        Console.WriteLine($"PASS: {checks} anchored-grounding checks across specimen scales/headings. Native collider intersection is not simulated.");
    }
}
