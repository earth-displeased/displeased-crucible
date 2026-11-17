namespace BossMod.Global.CrucibleOfTheUnbroken;

[ConfigDisplay(Name = "Second Master's Board - experimental encounter support", Parent = typeof(GlobalConfig))]
public sealed class SecondMasterAssistConfig : ConfigNode
{
    [PropertyDisplay("Allow unfinished Second Master encounter modules", tooltip: "Only inside territory 1343, Crucible group 1092, SecondMasterBoard modules. Experimental mechanics require recorded testing. Other duties keep the normal maturity filter.")]
    public bool Enabled;
}
public static class SecondMasterAssistPolicy
{
    public static bool Allow(bool enabled, uint territory, uint group, bool correctModule)
        => enabled && territory == 1343 && group == 1092 && correctModule;
}
