namespace RotationSolver.Basic.Helpers;
internal static class SirenChargePolicy
{
    public static bool Use(bool crawlingHit,int zombieHits,int charges,bool bound,float playerHp)
    {
        if(charges<=0) return false;
        // Keep the final charge until the binder actually prevents escape, or survival is urgent.
        if(crawlingHit) return charges>1 || bound || playerHp<=0.4f;
        // Zombie cleave must hit at least two adds; do not dump the reserve into the boss.
        return zombieHits>=2 && (charges>1 || playerHp<=0.4f);
    }
}
