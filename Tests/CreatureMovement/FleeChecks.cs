using System;
using UnityEngine;

// Runs production flee state/goal methods and the benthic escape-selection blocks.
// Native terrain, animation and rigidbody integration remain Unity checks.
public class CreatureLocomotion
{
    public void DeflectSurfaceDrifter(Vector3 normal) { }
}
public partial class ContextSteering
{
    public Vector3 TrueForward=Vector3.forward, Goal;
    public int GoalWrites;
    public void SetGoal(Vector3 goal) { Goal=goal; GoalWrites++; }
}
public partial class SpeciesAI
{
    private enum AIState { Wandering, Fleeing, Returning }
    private AIState _state=AIState.Wandering;
    private ContextSteering _steering=new ContextSteering();
    private Rigidbody _rb=new Rigidbody();
    private Transform _playerTransform;
    private Vector3 _spawnCenter, _wanderTarget;
    private float _wanderArrivalThreshold=2.5f, _speedMultiplier=1f;
    private bool _isWaitingAtWanderTarget;
    private float _fleeElapsed, _fleeCooldownUntil, _lastBoundaryTurnTime=-10;
    private Vector3 _fleeThreatPosition, _fleeDirection=Vector3.forward;
    public Transform transform=new Transform();
    private Vector3 FacingForward => _steering.TrueForward;
    public int WanderPicks;
    private void PickNewWanderTarget() { WanderPicks++; }
    private void UpdateWander() { }
    private T GetComponent<T>() where T:class => new CreatureLocomotion() as T;
    private static T FindFirstObjectByType<T>() where T:class => TerrainGenerator.Instance as T;
    public bool Returning => _state==AIState.Returning;
    public Vector3 Goal => _steering.Goal;
    public float Speed => _steering.MoveSpeed;
    public void Begin(SpeciesData profile,Vector3 position,Transform player=null)
    {
        Data=profile; transform.position=position; _spawnCenter=position;
        _playerTransform=player; Time.time=0;
    }
    public void Tick() { Time.time+=Time.deltaTime; Update(); }
    public void SetFacing(Vector3 facing) { _steering.TrueForward=facing; }
}
public partial class MovementHost
{
    private Vector3 _groundNormal=Vector3.up;
    public void EscapeProfile(SpeciesData data,SpeciesAI ai) { _data=data;_ai=ai;_currentSpeed=data.fleeSpeed;_steering.ModelYawOffset=data.modelYawOffset; }
    public static Vector3 Travel(Vector3 facing,Vector3 tangent,bool backward) => BenthicTravelDirection(facing,tangent,false,backward);
}
public static class FleeChecks
{
    private static int checks;
    private static void Check(bool value,string reason) { checks++;if(!value) throw new Exception(reason); }
    private static SpeciesData Shrimp() => new SpeciesData {
        speciesId="mino_nylon_shrimp_001",taxonomicClass=TaxonomicClass.Malacostraca,
        locomotionArchetype=LocomotionArchetype.BenthicFollower,isShy=true,moveSpeed=1.6f,fleeSpeed=4.5f,fleeRange=10,returnThreshold=40 };
    public static void Run()
    {
        Time.deltaTime=.02f;
        TerrainGenerator.Instance.Floor=-100f;
        var data=Shrimp();var ai=new SpeciesAI();
        var origin=new Vector3(0,-100,0);var threat=new Transform {position=origin-Vector3.right*2};
        ai.Begin(data,origin,threat);ai.Tick();
        Check(ai.IsFleeing,"Shrimp no longer flees when the submarine approaches.");
        Check(MathF.Abs(ai.Speed-4.5f)<.001f,"Shrimp's configured flee speed was lost.");
        Check(Vector3.Dot((ai.Goal-origin).normalized,Vector3.right)>.99f,"Escape goal does not lead away from the submarine.");
        var movement=new MovementHost();movement.EscapeProfile(data,ai);
        foreach(float offset in new[] { -90f,0f,90f })
        for(int yaw=0;yaw<360;yaw+=15)
        {
            movement._steering.ModelYawOffset=offset;
            movement._rb.rotation=Quaternion.Euler(0,yaw,0);
            var escape=movement.EscapeSelection(Vector3.right,4.5f);
            Check(!escape.backward,"Shrimp inherited a lobster-only backward escape.");
            Check(Vector3.Dot(escape.direction,Vector3.right)>.9999f,"Rotating the body rotated its escape direction: circling feedback returned.");
            Check(MathF.Abs(escape.speed-4.5f)<.001f,"Shrimp speed was incorrectly clamped to lobster speed.");
        }

        var lobster=Shrimp();lobster.speciesId="spiny_lobster_001";lobster.modelYawOffset=-90;
        var lobsterAI=new SpeciesAI();lobsterAI.Begin(lobster,origin);lobsterAI.Flee(origin-Vector3.right*2);
        movement.EscapeProfile(lobster,lobsterAI);
        var reverse=movement.EscapeSelection(Vector3.right,4.5f);
        Check(reverse.backward,"Lobster's short backward phase was lost.");
        for(int yaw=0;yaw<360;yaw+=15)
        {
            movement._rb.rotation=Quaternion.Euler(0,yaw,0);
            reverse=movement.EscapeSelection(Vector3.right,4.5f);
            var travel=MovementHost.Travel(CreatureFacing.Forward(movement._rb.rotation,-90),reverse.direction,true);
            var facing=movement.EscapeBodyHeading(reverse.direction,Vector3.forward,true);
            Check(Vector3.Dot(travel,Vector3.right)>.9999f,"Backward phase follows the rotating body instead of the escape goal.");
            Check(Vector3.Dot(facing,travel)<-.9999f,"Backward travel rotates the body nose-first, recreating feedback.");
        }
        for(int i=0;i<42;i++) lobsterAI.Tick();
        Check(!movement.EscapeSelection(Vector3.right,4.5f).backward,"Backward phase never ends.");

        // Even an immobilized creature must leave fleeing; repeated calls or
        // obstacle deflection cannot reset the episode indefinitely.
        ai=new SpeciesAI();ai.Begin(data,origin,threat);ai.Flee(threat.position);
        for(int i=0;i<310 && ai.IsFleeing;i++)
        {
            ai.Flee(threat.position);
            ai.TurnAwayFromBoundary(Vector3.right);
            Check(ai.IsFleeing,"Obstacle deflection prematurely replaced the flee state.");
            ai.Tick();
        }
        Check(ai.Returning && !ai.IsFleeing,"Blocked shrimp stayed in fleeing forever.");
        Check(MathF.Abs(ai.Speed-1.6f)<.001f,"Normal speed was not restored after fleeing.");
        ai.Tick(); // At home: returns to wandering, still close to the submarine.
        for(int i=0;i<100;i++) ai.Tick();
        Check(!ai.IsFleeing,"Proximity immediately retriggered fleeing during recovery.");
        for(int i=0;i<60;i++) ai.Tick();
        Check(ai.IsFleeing,"Recovery permanently disabled normal shy behaviour.");

        // Safe distance ends fleeing without requiring 40 m from spawn.
        ai=new SpeciesAI();ai.Begin(data,origin,threat);ai.Flee(threat.position);
        ai.transform.position=origin+Vector3.right*14;
        for(int i=0;i<40;i++) ai.Tick();
        Check(ai.Returning,"Safe shrimp still requires the distant spawn-return threshold.");
        ai=new SpeciesAI();ai.Begin(data,origin);ai.Flee(origin-Vector3.right);
        ai.transform.position=origin+Vector3.right*41;ai.Tick();
        Check(ai.Returning,"Existing maximum excursion safeguard was lost.");

        // Overlapping/overhead threats retain an initial horizontal direction,
        // even when the body turns. Missing player uses the supplied threat.
        ai=new SpeciesAI();ai.Begin(data,origin);ai.SetFacing(Vector3.right);ai.Flee(origin+Vector3.up*5);
        ai.SetFacing(-Vector3.right);ai.Tick();
        var heading=ai.Goal-origin;heading.y=0;
        Check(Vector3.Dot(heading.normalized,Vector3.right)>.9999f,"Overhead threat makes escape follow body rotation.");
        ai=new SpeciesAI();ai.Begin(data,origin);ai.Flee(origin-Vector3.right*2);
        for(int i=0;i<310 && ai.IsFleeing;i++) ai.Tick();
        Check(!ai.IsFleeing,"Missing player prevents flee timeout.");
        Console.WriteLine($"PASS: {checks} production flee selection, facing, timeout, safety and recovery checks.");
    }
}
