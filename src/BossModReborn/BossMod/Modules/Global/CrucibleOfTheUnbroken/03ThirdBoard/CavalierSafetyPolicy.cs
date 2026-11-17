namespace BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard;
public static class CavalierSafetyPolicy
{
    public static WPos LeastMenaceOverlap(WPos center,WPos player,IReadOnlyList<WPos> origins)
    {
        var best=center;var bestHits=int.MaxValue;var bestTravel=float.MaxValue;
        for(var x=-18.5f;x<=18.5f;x+=1f)
            for(var z=-18.5f;z<=18.5f;z+=1f)
            {
                var p=center+new WDir(x,z);
                var hits=origins.Count(o=>(o-p).LengthSq()<=20.75f*20.75f);
                var travel=(p-player).LengthSq();
                if(hits<bestHits || hits==bestHits && travel<bestTravel)
                { best=p;bestHits=hits;bestTravel=travel; }
            }
        return best;
    }
    // A lane volley can end only two seconds before Steelripper. Move to their shared
    // safe area during the volley instead of waiting to begin the cone dodge afterward.
    // Only collapse deadlines when there is a shared safe area; sequential mechanics
    // with no common safe area must retain their original activation times.
    public static bool Prepare(AIHints hints,WPos center,DateTime now)
    {
        var zones=hints.ForbiddenZones.Where(z=>z.activation<=now.AddSeconds(8)).ToArray();
        if(zones.Length<2) return false;
        bool Safe(WPos p)=>zones.All(z=>!z.shapeDistance.Contains(p));
        var found=false;
        for(var x=-18.5f;x<=18.5f && !found;x+=1f)
            for(var z=-18.5f;z<=18.5f && !found;z+=1f)
            {
                var p=center+new WDir(x,z);
                found=Safe(p)&&Safe(p+new WDir(.6f,0))&&Safe(p-new WDir(.6f,0))
                    &&Safe(p+new WDir(0,.6f))&&Safe(p-new WDir(0,.6f));
            }
        if(!found)
        {
            var first=zones.Min(z=>z.activation);
            var early=zones.Where(z=>z.activation<=first.AddSeconds(.75)).ToArray();
            var later=zones.Where(z=>z.activation>first.AddSeconds(.75)).ToArray();
            if(later.Length>0)
                hints.GoalZones.Add(p=>early.All(z=>!z.shapeDistance.Contains(p))
                    ? Math.Max(0f,100f+later.Min(z=>z.shapeDistance.Distance(p))) : 0f);
            return false;
        }
        for(var i=0;i<hints.ForbiddenZones.Count;i++)
        {
            var zone=hints.ForbiddenZones[i];
            if(zone.activation<=now.AddSeconds(8)) hints.ForbiddenZones[i]=(zone.shapeDistance,now,zone.Source);
        }
        return true;
    }
}
