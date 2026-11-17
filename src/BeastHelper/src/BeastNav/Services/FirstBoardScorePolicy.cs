using BeastNav.Models;

namespace BeastNav.Services;

public static class FirstBoardScorePolicy
{
    public const uint Territory = 1339;
    public static uint TerritoryFor(Configuration c) => c.ScoreBoardTerritory is 1339 or 1340 or 1341 ? c.ScoreBoardTerritory : 0;
    public static bool ItemsAllowed(Configuration c) => c.FirstBoardScoreEnabled && TerritoryFor(c) == 1341;
    public static bool AllowsCombat(uint territory) => territory == Territory;
    public static bool AllowsCombat(Configuration c, uint territory) => TerritoryFor(c) != 0 && territory == TerritoryFor(c);

    public static void Enforce(Configuration c)
    {
        if (!c.FirstBoardScoreEnabled || c.AutomationStopped) return;
        c.CombatOnlyEnabled = true;
        c.CombatOnlyBeastSelectionEnabled = true;
        c.AutoCruciblePetPartyEnabled = true;
        c.AutoCrucibleWhistleAssignEnabled = true;
        c.SelectedCrucibleBoard = TerritoryFor(c) switch { 1341 => "第三盤", 1340 => "第二盤", _ => "第一盤" };
        c.AutoEnterCrucibleEnabled = false;
        c.AutoSelectBoardEnabled = false;
        c.CrucibleAutoDutyModeEnabled = false;
        c.AutoCrucibleShopBuyEnabled = false;
        if (!ItemsAllowed(c)) c.AutoRecoveryItemEnabled = false;
        c.AutoSelfBuffItemsEnabled = false;
        if (!ItemsAllowed(c)) c.AutoOffensiveItemsEnabled = false;
        c.AutoCrucibleItemRulesEnabled = false;
    }

    public static IReadOnlyList<BeastPet> VilekinTrio(IEnumerable<BeastPet> pets, out string reason)
    {
        uint[] order = [44, 16, 10]; // Damselfly -> Mantis -> Wespe (bestiary numbers, not pet row IDs).
        var owned = pets.DistinctBy(p => p.PetRowId).ToArray();
        var trio = order.Select(row => owned.FirstOrDefault(p => p.XbmRowId == row && p.Classification == 2)).OfType<BeastPet>().ToArray();
        reason = trio.Length == 3 ? string.Empty : "Vilekin combo requires captured Damselfly, Mantis and Wespe. No substitutions.";
        return trio.Length == 3 ? trio : [];
    }

    public static bool XpRun(Configuration c) => !c.FirstBoardScoreEnabled && c.CrucibleTeamMode==CrucibleTeamMode.ThirdBoardXpRun;
    public static bool BeastkinClear(Configuration c) => XpRun(c) || c.FirstBoardScoreEnabled && c.ScoreBoardTerritory==1341 && c.ThirdBoardBeastkinClear;
    public static void SelectXpRun(Configuration c)
    {
        c.CrucibleTeamMode=CrucibleTeamMode.ThirdBoardXpRun;
        c.FirstBoardScoreEnabled=false;
        c.SelectedCrucibleBoard="第三盤";
        c.CombatOnlyEnabled=true;
        c.CombatOnlyBeastSelectionEnabled=true;
        c.AutoCruciblePetPartyEnabled=true;
        c.AutoCrucibleWhistleAssignEnabled=true;
        c.ClassMinMaxRankScanEnabled=true;
        c.AutoEnterCrucibleEnabled=false;
        c.AutoSelectBoardEnabled=false;
        c.CrucibleAutoDutyModeEnabled=false;
        c.AutoCrucibleShopBuyEnabled=false;
    }
    public static IReadOnlyList<BeastPet> BeastkinTrio(IEnumerable<BeastPet> pets,out string reason)
    {
        uint[] order=[1,26,33]; // Cu Sith, Buffalo, Coeurl; species, never assumed horn slots.
        var owned=pets.DistinctBy(p=>p.PetRowId).ToArray();
        var trio=order.Select(n=>owned.FirstOrDefault(p=>p.XbmRowId==n && p.Classification==1)).OfType<BeastPet>().ToArray();
        reason=trio.Length==3 ? string.Empty : "Third Board Beastkin clear requires captured Cu Sith, Buffalo and Coeurl. No substitutions. Aim for rank 15.";
        return trio.Length==3 ? trio : [];
    }

    // This estimates combat strength at First Board's rank cap, not the game's final score.
    public static IReadOnlyList<BeastPet> Select(IEnumerable<BeastPet> pets,
        IReadOnlyDictionary<uint, BeastProgress> progress, byte family, IReadOnlyList<uint> locked, out string reason)
    {
        var owned = pets.DistinctBy(p => p.PetRowId).Where(p => p.Classification is >= 1 and <= 8).ToArray();
        reason = string.Empty;
        if (locked.Count != 0)
        {
            var team = locked.Select(id => owned.FirstOrDefault(p => p.PetRowId == id)).OfType<BeastPet>().ToArray();
            if (locked.Count != 3 || locked.Distinct().Count() != 3 || team.Length != 3
                || team.Select(p => p.Classification).Distinct().Count() != 1
                || (family != 0 && team[0].Classification != family))
            {
                reason = "Saved score trio is unavailable or invalid. Choose a new trio outside the duty.";
                return [];
            }
            return team; // Never substitute a fourth beast or rotate on rank gains.
        }
        double Strength(BeastPet p) => Math.Min(progress[p.PetRowId].Level, 5) * 10
            + p.Rank * 3 + (p.Stat2 + p.Stat3 + p.Stat5) * 0.5 + (p.Stat1 + p.Stat4) * 0.25;
        var result = owned.Where(p => (family == 0 || p.Classification == family)
                && progress.TryGetValue(p.PetRowId, out var rank) && rank.Level is >= 1 and <= 25)
            .GroupBy(p => p.Classification).Where(g => g.Count() >= 3)
            .Select(g => g.OrderByDescending(Strength).ThenBy(p => p.XbmRowId).Take(3).ToArray())
            .OrderByDescending(trio => trio.Sum(Strength)).ThenBy(trio => trio[0].Classification).FirstOrDefault();
        if (result == null) reason = "Need three captured beasts of one family with synced ranks. Open the bestiary and sync ranks first.";
        return result ?? [];
    }
}
