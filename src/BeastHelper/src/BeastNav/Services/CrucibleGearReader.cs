using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace BeastNav.Services;
public sealed unsafe class CrucibleGearReader
{
    private DateTime nextRead;
    public bool CrimsonRibbonOwned {get;private set;}
    public string Status {get;private set;}="Gear HUD not read";
    public void Update(DateTime now)
    {
        if(now<nextRead) return;
        nextRead=now.AddSeconds(1);CrimsonRibbonOwned=false;
        var manager=RaptureAtkUnitManager.Instance();
        if(manager==null) return;
        var list=&manager->AtkUnitManager.AllLoadedUnitsList;
        for(var i=0;i<Math.Min((int)list->Count,256);i++)
        {
            var addon=list->Entries[i].Value;
            if(addon==null || !addon->IsVisible) continue;
            var text=new List<string>();var icons=new HashSet<uint>();var visited=new HashSet<nint>();
            Scan(&addon->UldManager,text,icons,visited,0);
            if(!text.Any(t=>t.Contains("Beast Gear Effects",StringComparison.OrdinalIgnoreCase))
                && !(text.Any(t=>t.Contains("CRUCIBLE ITEMS",StringComparison.OrdinalIgnoreCase))
                    && text.Any(t=>t.Equals("BEAST GEAR",StringComparison.OrdinalIgnoreCase)))) continue;
            // Only the owned-gear HUD qualifies; shop/treasure preview icons are never ownership evidence.
            CrimsonRibbonOwned=icons.Contains(243018);
            Status=CrimsonRibbonOwned ? "Crimson Ribbon detected in owned-gear HUD" : "Owned-gear HUD read: no Crimson Ribbon";
            return;
        }
        Status="Owned-gear HUD unavailable; Ribbon-only heals skipped";
    }
    private static bool Shown(AtkResNode* node)
    {
        for(var i=0;node!=null && i<64;i++,node=node->ParentNode)
            if((node->NodeFlags&NodeFlags.Visible)==0) return false;
        return true;
    }
    private static void Scan(AtkUldManager* uld,List<string> text,HashSet<uint> icons,HashSet<nint> visited,int depth)
    {
        if(depth>6 || visited.Count>3000 || uld->NodeList==null) return;
        for(var i=0;i<Math.Min((int)uld->NodeListCount,1500);i++)
        {
            var n=uld->NodeList[i];if(n==null || !visited.Add((nint)n) || !Shown(n)) continue;
            var c=n->GetComponent();
            if(c!=null)
            {
                if(c->GetComponentType()==ComponentType.Icon) icons.Add(((AtkComponentIcon*)c)->IconId);
                Scan(&c->UldManager,text,icons,visited,depth+1);
            }
            else if(n->Type==NodeType.Text) text.Add(((AtkTextNode*)n)->NodeText.ToString());
        }
    }
}
