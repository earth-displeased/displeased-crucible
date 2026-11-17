namespace BossMod.Global.CrucibleOfTheUnbroken;
public static class SecondBoardAssistPolicy
{
    public static bool Allow(bool enabled, uint territory, uint group, bool correctModule)
        => enabled && territory == 1340 && group == 1089 && correctModule;
}
