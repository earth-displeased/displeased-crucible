namespace BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard;
public static class ThirdBoardTargetPolicy
{
    public static bool PreferUrgentPrimary(bool friendly,uint action,int priority)
        => !friendly && action is not 46750 and not 46751 && priority>=2;

    // Armored Ymir can harmlessly absorb splash while its shell is attacked.
    // Spikes and eggs are mechanically dangerous collateral, not merely protected targets.
    public static bool DangerousCollateral(uint oid, bool protectedTarget) => oid is 0x4C99 or 0x4C9A || oid==0x4C95 && protectedTarget;

    public static int Priority(uint oid, bool protectedTarget=false) => protectedTarget ? -1 : oid switch
    {
        0x4C90 => 2, // Cavalier's bishop
        0x4C95 => 3, 0x4C94 => 2, 0x4C93 => 4, // unspiked Sahagin, shell, exposed Ymir (burst before it re-shells)
        0x4C9A => -1, 0x4C99 => -1, // egg identifiers are also forbidden by Zu's own module
        0x4C98 => 3, 0x4C97 => 2, 0x4C9F => 2,
        0x4CA4 => 3, 0x4CA3 => 2,
        0x4CA7 => 4, 0x4CA9 => 3, 0x4CA8 => 2,
        0x4CAD => 3,
        _ => 1
    };
}
