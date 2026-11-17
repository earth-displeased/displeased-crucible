namespace BossMod.Global.CrucibleOfTheUnbroken;
[ConfigDisplay(Name = "Second Board - experimental encounter support", Parent = typeof(GlobalConfig))]
public sealed class SecondBoardAssistConfig : ConfigNode
{
    [PropertyDisplay("Allow unfinished Second Board encounter modules (Loosefrox and Taurus)")]
    public bool Enabled;
}
