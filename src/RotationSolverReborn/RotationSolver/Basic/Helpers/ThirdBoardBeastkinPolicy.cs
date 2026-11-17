namespace RotationSolver.Basic.Helpers;
internal static class ThirdBoardBeastkinPolicy
{
    public static int NextSwap(int current,float familiarHp,bool prepareBuffalo,bool ymirAlive,bool cuReady,bool coeurlReady,bool buffaloReady)
    {
        if(prepareBuffalo && current!=26 && buffaloReady) return 26;
        if(familiarHp>(current==1 ? 0.6f : 0.2f)) return 0;
        if(current!=33 && coeurlReady) return 33;
        if(current!=1 && cuReady) return 1;
        return current!=26 && !ymirAlive && buffaloReady ? 26 : 0;
    }
    public static bool CanSwap(System.DateTime now,System.DateTime releaseHold,System.DateTime defenseHold)
        => now>=releaseHold && now>=defenseHold;
    public static bool PrepareBuffalo(bool ymirAlive,bool exposed,float shellHp,bool used)
        => ymirAlive && !used && (exposed || shellHp is >0 and <=0.45f);
    public static bool Release(int species,bool nature,bool beastskin,bool exposed,bool inRange,bool dangerousCast)
        => nature && (species==1 && beastskin || species==33 || species==26 && inRange && (exposed || dangerousCast));
    public static int Opening(bool needsBorrow,bool cuReady,bool coeurlReady,bool buffaloReady,bool prepareBuffalo)
        => needsBorrow && cuReady ? 1 : prepareBuffalo && buffaloReady ? 26 : coeurlReady ? 33 : cuReady ? 1 : buffaloReady ? 26 : 0;
}
