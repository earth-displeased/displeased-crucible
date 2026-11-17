namespace BossMod.Global.MaskedCarnivale.Stage32;

// Kept independent of game APIs so competing mechanic decisions can be replay-tested.
public enum GoldorTask { Idle, Interrupt, Slime, Gravity, Inhale, Dolls, Rush, Knockback, Guard, Heal, Cleanse, Mana, Crystal, Fireball, Attack }

public readonly record struct GoldorSituation(
    bool Enabled, bool Alive, bool InCombat, bool Ready, bool Protected,
    bool Interrupt, bool Slime, bool Gravity, bool Dolls, bool Rush,
    bool Guard, float HP, bool Cleanse, uint MP, bool Crystal, bool Fireball, bool Inhale = false, float HealThreshold = 0.8f, bool Knockback = false);

public static class GoldorPolicy
{
    public static GoldorTask Decide(in GoldorSituation s)
    {
        if (!s.Enabled || !s.Alive || !s.InCombat || !s.Ready || s.Protected)
            return GoldorTask.Idle;
        if (s.Interrupt) return GoldorTask.Interrupt;
        if (s.Slime) return GoldorTask.Slime;
        if (s.Gravity) return GoldorTask.Gravity;
        if (s.Inhale) return GoldorTask.Inhale;
        if (s.Dolls) return GoldorTask.Dolls;
        if (s.Rush) return GoldorTask.Rush;
        if (s.Knockback) return GoldorTask.Knockback;
        if (s.HP < 0.4f) return GoldorTask.Heal;
        if (!s.Guard) return GoldorTask.Guard;
        // A replay showed a fatal orb surviving by 1533 HP after MP recovery
        // consumed the finishing GCD. Only critical healing may pre-empt the kill.
        if (s.Fireball) return GoldorTask.Fireball;
        if (s.Cleanse) return GoldorTask.Cleanse;
        if (s.HP < s.HealThreshold) return GoldorTask.Heal;
        if (s.MP < 4500) return GoldorTask.Mana;
        if (s.Crystal) return GoldorTask.Crystal;
        return GoldorTask.Attack;
    }

    public static bool SlimeInRange(float centerDistance, float slimeRadius) => centerDistance <= 5.5f + slimeRadius;
    public static bool CanFinish(float castTime, float gcdRemaining, float deadline) => castTime + gcdRemaining + 0.35f < deadline;
    public static bool CanTankDolls(int cones, bool shielded) => cones <= (shielded ? 2 : 0);
    public static bool ShouldFreeze(bool frozen, float cooldown) => !frozen && cooldown < 0.2f;
    public static bool ReserveLoom(bool heavy, uint bossCast) => heavy || bossCast == 34451;
    public static bool ConfirmsStatus(ulong effect, uint status) => (byte)effect == 14 && (effect >> 48) == status;
    public static ulong LockFireball(ulong selected, ulong nearest) => selected != 0 ? selected : nearest;
    public static bool ConfirmFireGap(ulong selected, ulong observed, uint hp, bool dead, bool burnResolved)
        => selected != 0 && selected == observed && hp == 0 && dead && !burnResolved;
    public static bool ApplyDot(bool magicImmune, bool present, float remaining, bool permanent)
        => !magicImmune && (!present || !permanent && remaining <= 6);
}

// Quake and marionette casts overlap. Marionettes can start on the very frame
// the inner Quake resolves; only a NEW inner Quake may reset this progress.
public sealed class GoldorQuakeProgress
{
    public bool InnerResolved { get; private set; }
    public void Started(uint action) { if (action == 34457) InnerResolved = false; }
    public void Finished(uint action) { if (action == 34457) InnerResolved = true; }
}

// Cast-end is not pull completion: reserve actions across the animation gap.
public sealed class GoldorCyclopsProgress
{
    public ulong ActorID { get; private set; }
    public void Started(ulong actor, uint action) { if (action is 34466 or 34467) ActorID = actor; }
    public void Resolved(ulong actor, uint action) { if (ActorID == actor && action == 34467) ActorID = 0; }
}
