namespace RotationSolver.Basic.Helpers;
internal static class FirstBoardVilekinPolicy
{
    public static bool HoldYmirReserve(bool thirdBoard, int pet, bool bothBossesAlive, float familiarHp)
        => thirdBoard && pet==10 && bothBossesAlive && familiarHp>0.20f;

    public static bool Release(int pet, bool combat, bool nature, bool guarded, float targetHp, float vulnerabilityRemaining = 0, bool familiarAtRisk = false)
        => combat && nature && !guarded && (pet is 44 or 16 || pet == 10 && targetHp > 0 && (targetHp <= 0.30f || vulnerabilityRemaining is > 0 and <= 5f || familiarAtRisk));
    public static bool Dismiss(int pet, bool observedNature, bool nature)
        => pet is 44 or 16 && observedNature && !nature;
}
