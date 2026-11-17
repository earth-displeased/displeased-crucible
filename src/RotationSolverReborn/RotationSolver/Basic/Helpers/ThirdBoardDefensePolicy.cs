namespace RotationSolver.Basic.Helpers;
internal static class ThirdBoardDefensePolicy
{
    public static bool ReserveGapCloser(uint oid,uint cast)=>oid==0x4CAC && cast==48619;
    public static bool Incoming(uint oid,uint cast) => oid switch
    {
        0x4C93 => cast==48479, // Ymir: Blanket Thunder
        0x4C95 => cast==48485, // Sahagin: Dreadwash
        0x4CA1 => cast==48563, // Siren: Song of Torment
        0x4C9E => cast==48561, // Lakhamu: Sand Tempest
        0x4CAA => cast==48620, // Guttler: Thunderbolt
        _ => false
    };
    // Leave enough life for the recorded tankbuster plus following autos.
    // A critically low player can still sacrifice a familiar to prevent immediate death.
    public static bool CanCoverCast(float playerHp,float familiarHp)
        => playerHp<=0.3f || familiarHp>=0.6f;
    public static bool PreserveCuCover(int species,bool critical)=>species!=1 || critical;
    public static bool PreserveCuTakeBack(int species,float playerHp,bool critical)=>species==1 && !critical && playerHp>0.3f;
    public static int ChallengePriority(uint oid,bool attackingFamiliar)=>!attackingFamiliar ? 0 : oid==0x4C8E ? 100 : 50;
    public static bool Cover(float playerHp,float familiarHp,bool critical)
        => critical || familiarHp>0.25f && familiarHp>=playerHp;
    public static bool TakeBack(float playerHp,float familiarHp,bool critical)
        => !critical && playerHp>familiarHp+0.2f;
}
