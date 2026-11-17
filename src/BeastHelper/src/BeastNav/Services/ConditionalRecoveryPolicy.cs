namespace BeastNav.Services;
public static class ConditionalRecoveryPolicy
{
    public static ushort[] Candidates(bool ribbon,bool poison,bool blind,bool petrified,bool familiar)
    {
        var items=new List<ushort>();
        if(poison) items.Add(83);
        if(blind) items.Add(85);
        if(familiar && petrified) items.Add(84);
        // Prefer other serums before spending antiparalysis reserves needed for Guttler.
        if(ribbon) items.AddRange(new ushort[]{90,91,86,87,94,95,92,93,88,89});
        return items.ToArray();
    }
    public static bool AreaSerum(ushort item)=>item is 87 or 89 or 91 or 93 or 95;
}
