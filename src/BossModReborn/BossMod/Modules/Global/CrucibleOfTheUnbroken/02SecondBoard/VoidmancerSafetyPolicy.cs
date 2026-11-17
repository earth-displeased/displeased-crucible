namespace BossMod.Global.CrucibleOfTheUnbroken.SecondBoard;
public static class VoidmancerSafetyPolicy
{
    public static WPos RecoverInside(WPos p, WPos center) => new(Math.Clamp(p.X,center.X-18.5f,center.X+18.5f),Math.Clamp(p.Z,center.Z-18.5f,center.Z+18.5f));
    public static bool MarchSafe(WPos start, Angle facing, Angle turn, WPos center, WPos[] pools)
    {
        var direction=(facing+turn).ToDirection();
        var finish=start+direction*18f;
        if (Math.Abs(finish.X-center.X)>19 || Math.Abs(finish.Z-center.Z)>19) return false;
        foreach(var pool in pools)
        {
            var along=Math.Clamp((pool-start).Dot(direction),0,18);
            if ((start+along*direction-pool).LengthSq()<8.5f*8.5f) return false;
        }
        return true;
    }
}
