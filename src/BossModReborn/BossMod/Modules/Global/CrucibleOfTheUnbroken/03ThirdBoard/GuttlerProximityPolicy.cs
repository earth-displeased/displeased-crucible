namespace BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard;
public static class GuttlerProximityPolicy
{
    // Proximity damage does not become safe at the old 30-yalm navigation cutoff.
    // Make distance valuable throughout the legal arena while other hazard constraints remain active.
    public static float Score(float distanceSquared)=>10f*MathF.Sqrt(Math.Max(0f,distanceSquared));
}
