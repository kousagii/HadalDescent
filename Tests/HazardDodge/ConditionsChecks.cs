using System;
using Kind = HazardDodgeMinigame.HazardType;

public static class ConditionsChecks
{
    private static int checks;
    private static void Check(bool condition,string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Advance(HazardDodgeConditions state,float seconds,float movement=0,float disturbance=0,float dt=1f/60f)
    {
        for (int i=0;i<(int)Math.Round(seconds/dt);i++) state.Tick(dt,movement,disturbance);
    }
    public static void Run()
    {
        foreach (int direction in new[]{-1,1})
        foreach (float speed in new[]{18f,20.7f,23.4f,26.1f,28.8f})
        foreach (bool dodge in new[]{false,true})
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.CurrentSurge,1,direction:direction);
            game.Add(Kind.RockOutcrop,1+direction,-1.7f+speed*.9f);
            game.Step(speed,.05f);
            Check(game._currentLane==1+direction,"Paired chevron failed to push toward the upcoming rock");
            for (int frame=0;frame<24;frame++)
            {
                if (dodge && frame==2) game.ShiftLane(-direction);
                game.Motion(.05f,speed);
                game.Step(speed,.05f);
            }
            Check(game._remainingHits==(dodge ? 3 : 2),
                dodge ? "Player cannot avoid paired rock with a recovery swipe" : "Ignoring the paired rock has no consequence");
        }
        foreach (int direction in new[]{-1,1})
        foreach (float dt in new[]{1f/30f,1f/60f,1f/120f,.1f})
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.CurrentSurge,1,direction:direction);
            game.Step();
            Check(game._currentLane==1+direction,"Chevron did not select the adjacent lane in its direction");
            game.Step();
            Check(game._currentLane==1+direction,"Same chevron pushed twice");
            for (int i=0;i<(int)Math.Round(2f/dt);i++) game.Motion(dt);
            Check(Math.Abs(game._currentX/game.laneWidth-direction)<.001f,"Push returned to the original lane or kept drifting");
            Check(!game._conditions.InCurrent,"Cosmetic current feedback never expired");
            game.ShiftLane(-direction);
            Check(game._currentLane==1,"Opposite swipe was consumed by stabilization");
            game.ShiftLane(-direction);
            Check(game._currentLane==1-direction,"Normal lane changes did not continue");
            Check(game._remainingHits==3,"Chevron directly damaged hull");
        }
        foreach (int direction in new[]{-1,1})
        {
            int lane=direction<0 ? 0 : 2;
            var game=new HazardDodgeMinigame { _currentLane=lane, _currentX=direction*7.2f,
                _steeringX=direction*7.2f, _targetX=direction*7.2f };
            game.Add(Kind.CurrentSurge,lane,direction:direction);
            game.Step();
            Check(game._currentLane==lane,"Edge chevron reversed direction");
            game.Motion(.1f);
            Check(Math.Abs(game._currentX)<=game.laneWidth,"Chevron pushed outside track");
        }
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.CurrentSurge,1,direction:1);
            game.Step();
            game.ShiftLane(-1);
            Check(game._currentLane==1 && game._conditions.InCurrent,"Swipe during current feedback failed to change lanes");
        }
        foreach (float density in new[]{.5f,1f})
        foreach (float dt in new[]{1f/30f,1f/60f,1f/120f,.1f})
        {
            var calm=new HazardDodgeConditions();
            var stirred=new HazardDodgeConditions();
            calm.EnterSediment(density); stirred.EnterSediment(density);
            Check(calm.Sediment>=.27f && calm.CloudTime>=2.8f,"Sediment entry has no immediate visibility penalty");
            Advance(calm,1,dt:dt);
            Advance(stirred,1,1,1,dt);
            Check(calm.Sediment>.4f,"Sediment remains imperceptibly faint");
            Check(stirred.Sediment>calm.Sediment+.15f,"Movement and disturbance do not raise density");
            Check(stirred.Sediment<=.9401f,"Sediment exceeds density cap");
            float value=stirred.Sediment,clock=stirred.Clock;
            stirred.Tick(0,1,1);
            Check(stirred.Sediment==value && stirred.Clock==clock,"Paused conditions continue simulating");
            Advance(stirred,12,1,1,dt);
            Check(stirred.Sediment==0 && stirred.CloudTime==0,"Sediment never settles after exposure");
            stirred.EnterSediment(density);
            stirred.Reset();
            Check(stirred.Sediment==0 && stirred.CloudTime==0 && !stirred.InCurrent,"Reset retained effects");
        }
        {
            var light=new HazardDodgeConditions();
            var dense=new HazardDodgeConditions();
            light.EnterSediment(.5f); dense.EnterSediment(1f);
            Advance(light,.3f); Advance(dense,.3f);
            Check(dense.Sediment>light.Sediment+.2f,"Dense and light sediment have the same visibility penalty");
            Advance(dense,1f);
            Check(dense.CloudTime>0 && dense.Sediment>.7f,"Dense sediment clears before affecting navigation");
        }
        {
            var game=new HazardDodgeMinigame();
            game._conditions.EnterSediment(1f);
            game._atmosphere=new HazardDodgeAtmosphere();
            UnityEngine.Time.frameCount=10;
            game.Feedback(0);
            Check(game._sedimentOverlay.color.a>.2f,"Missing screen material disabled fallback tint");
            game._atmosphere.RenderedForTest(8);
            game.Feedback(0);
            Check(game._sedimentOverlay.color.a>.2f,"Skipped render pass disabled fallback tint");
            game._atmosphere.RenderedForTest(9);
            game.Feedback(0);
            Check(game._sedimentOverlay.color.a==0,"Rendered screen effect stacked with fallback tint");
            UnityEngine.Time.frameCount=12;
            game.Feedback(0);
            Check(game._sedimentOverlay.color.a>.2f,"Fallback did not recover when screen rendering stopped");
        }
        Console.WriteLine($"Chevron/sediment interaction checks passed: {checks} assertions.");
    }
}
