using System;
using Kind = HazardDodgeMinigame.HazardType;

public static class Program
{
    private static int checks;
    private static void Check(bool condition,string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        TriggerChecks.Run();
        ConditionsChecks.Run();
        var random=new Random(417);
        int currentWaves=0, pairedWaves=0;
        for (int zone=0;zone<5;zone++)
        for (int field=0;field<2;field++)
        for (int i=0;i<1000;i++)
        {
            var wave=HazardDodgeRules.BuildWave(zone,field==1,random);
            Check(wave.OpenLane>=0 && wave.OpenLane<3,"Invalid open lane");
            Check(wave.Lanes[wave.OpenLane]==null || wave.Lanes[wave.OpenLane]==Kind.DataPod,"Wave blocks all lanes");
            bool current=Array.IndexOf(wave.Lanes,Kind.CurrentSurge)>=0;
            if (current) currentWaves++;
            if (wave.Lanes[1]==Kind.CurrentSurge && wave.CurrentDirections[1]!=0)
            {
                pairedWaves++;
                int target=1+wave.CurrentDirections[1];
                Check(wave.Lanes[target]==Kind.RockOutcrop,"Paired chevron does not point at a rock lane");
                Check(wave.ArrivalDelays[target]>=.9f,"Paired rock gives no recovery swipe time");
                Check(target!=wave.OpenLane,"Paired current blocks the escape lane");
                foreach (float speed in new[]{18f,20.7f,23.4f,26.1f,28.8f})
                    Check(speed*wave.ArrivalDelays[target]>5f+speed*.45f,
                        "Paired rock is so close that push safety cancels the intended threat");
            }
            foreach (var kind in wave.Lanes)
            {
                Check(kind!=Kind.ThermalVent || (zone>=2 && field==1),"Vent outside a deep vent field");
                Check(zone!=0 || kind!=Kind.FallingRock,"Rockfall in shallow route");
            }
        }
        Check((float)pairedWaves/currentWaves>.80f && (float)pairedWaves/currentWaves<.90f,
            "Current-to-rock pairing is outside the intended 85 percent range");
        for (int lane=0;lane<3;lane++)
        for (int mask=0;mask<8;mask++)
        foreach (int direction in new[]{-1,1})
        {
            int next=HazardDodgeRules.SafeCurrentDestination(lane,direction,mask);
            Check(next>=0 && next<3 && Math.Abs(next-lane)<=1,"Current skips a lane or exits track");
            Check(next==lane || (mask & (1<<next))==0,"Current forces a damaging destination");
            Check(next==lane || Math.Sign(next-lane)==direction,"Chevron pushes opposite its visible direction");
        }
        Check(HazardDodgeRules.CrossesSubmarine(5,-8,-2.2f),"Swept collision missed fast obstacle");
        Check(!HazardDodgeRules.CrossesSubmarine(8,5,-2.2f),"Distant obstacle collides early");
        Check(!HazardDodgeRules.CrossesSubmarine(-6,-8,-2.2f),"Passed obstacle hits again");

        foreach (var kind in new[]{Kind.RockOutcrop,Kind.FallingRock,Kind.ThermalVent})
        {
            var game=new HazardDodgeMinigame();
            game.Add(kind,1);
            game.Step();
            Check(game._remainingHits==2,"Solid hazard did not damage hull");
            game.Step();
            Check(game._remainingHits==2,"Same hazard hit twice");
        }
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.RockOutcrop,1,5);
            game.Step(100,.1f);
            Check(game._remainingHits==2,"Dispatch missed a fast obstacle");
        }
        {
            var game=new HazardDodgeMinigame { _currentLane=2, _currentX=0 };
            game.Add(Kind.RockOutcrop,1);
            game.Step();
            Check(game._remainingHits==2,"Lane-change target hid actual contact");
        }
        {
            var game=new HazardDodgeMinigame { _currentLane=1, _currentX=7.2f };
            game.Add(Kind.RockOutcrop,1);
            game.Step();
            Check(game._remainingHits==3,"Target lane caused phantom contact");
        }
        foreach (bool reverseOrder in new[]{false,true})
        {
            var game=new HazardDodgeMinigame();
            if (reverseOrder) game.Add(Kind.RockOutcrop,2);
            game.Add(Kind.CurrentSurge,1);
            if (!reverseOrder) game.Add(Kind.RockOutcrop,2);
            game.Step();
            Check(game._currentLane==1,"Blocked rightward chevron pushed into a rock or reversed left");
            Check(game._remainingHits==3,"Current damaged the hull");
        }
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.CurrentSurge,1);
            game.Add(Kind.RockOutcrop,0);
            game.Add(Kind.FallingRock,2,1);
            game.Step();
            Check(game._currentLane==1,"Current forced a move with both exits blocked");
        }
        {
            var game=new HazardDodgeMinigame();
            var visual=game.Add(Kind.SedimentCloud,1);
            game.Step();
            Check(game._remainingHits==3 && game._conditions.CloudTime>0f,"Sediment dispatch wrong");
            game.Motion(.1f);
            game.Feedback(.1f);
            Check(game._sedimentOverlay.color.a>.1f && game._sedimentOverlay.color.a<=.45f,"Fallback sediment is invisible or fully obscures the scene");
            for (int j=0;j<100;j++) { game.Motion(.1f); game.Feedback(.1f); }
            Check(game._sedimentOverlay.color.a==0,"Sediment did not clear");
            game._conditions.EnterSediment(1);
            GameManager.Instance=new GameManager();
            game.Finish(true);
            game.Finish(true);
            Check(visual.Paused && game._sedimentOverlay.color.a==0,"Finish retained effects");
            Check(game.FinishBanners==1 && GameManager.Instance.Added==50,"Finish duplicated reward");
        }
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.DataPod,1);
            game.Step();
            game.Step();
            Check(game._collectedRdp==5 && game._remainingHits==3,"Pod reward changed or duplicated");
        }
        {
            var game=new HazardDodgeMinigame { _remainingHits=1 };
            GameManager.Instance=new GameManager();
            game.Add(Kind.RockOutcrop,1);
            game.Add(Kind.FallingRock,1);
            game.Step();
            Check(game._isFinished && GameManager.Instance.Deducted==50 && game._remainingHits==0,"Fatal impact resolution wrong");
        }
        {
            var game=new HazardDodgeMinigame();
            game.Add(Kind.RockOutcrop,0,-11);
            game.Step();
            Check(game.Count==0 && game.Destroyed==1,"Passed hazard was not removed");
        }
        Console.WriteLine($"Hazard Dodge: {checks} checks passed (production rules and collision/effect dispatch).");
    }
}
