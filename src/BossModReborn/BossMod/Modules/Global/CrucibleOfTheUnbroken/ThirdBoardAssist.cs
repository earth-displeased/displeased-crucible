namespace BossMod.Global.CrucibleOfTheUnbroken;
[ConfigDisplay(Name = "Third Board - three-beast clear support", Parent = typeof(GlobalConfig))]
public sealed class ThirdBoardAssistConfig : ConfigNode
{
    [PropertyDisplay("Enable Third Board targeting and unfinished alternate-fork module")]
    public bool Enabled;
}
public static class ThirdBoardAssistPolicy
{
    public static bool Allow(bool enabled,uint territory,uint group,bool correctModule)
        => enabled && territory==1341 && group==1090 && correctModule;
}
