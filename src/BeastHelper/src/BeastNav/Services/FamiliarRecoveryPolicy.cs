namespace BeastNav.Services;
public static class FamiliarRecoveryPolicy
{
    public static bool SupportedPet(uint oid,bool firstMaster,bool thirdBoard)
        => firstMaster && oid is 0x49E4 or 0x4A15 or 0x4A04
            || thirdBoard && oid is 0x49E4 or 0x49FD or 0x4A04;
    public static bool ShouldHeal(bool enabled,bool playerNeedsRecovery,bool allowedMode,float familiarHp,float threshold)
        => enabled && !playerNeedsRecovery && allowedMode && familiarHp>0 && familiarHp<threshold;
}
