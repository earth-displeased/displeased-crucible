namespace BossMod.Global.CrucibleOfTheUnbroken.SecondBoard;
public static class SecondBoardTargetPolicy
{
    public static bool DoomTileCharging(uint state) => state is 0x00020001 or 0x00200010 or 0x00800040 or 0x02000100;
    public static double HealthDifference(uint younger, uint youngerMax, uint elder, uint elderMax)
        => youngerMax == 0 || elderMax == 0 ? 0 : (double)younger / youngerMax - (double)elder / elderMax;
}
