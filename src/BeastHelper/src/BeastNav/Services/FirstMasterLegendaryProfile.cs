using BeastNav.Models;
namespace BeastNav.Services;

/// <summary>Opt-in preparation profile. Does not claim encounter-specific rotation coverage.</summary>
public static class FirstMasterLegendaryProfile
{
    public static bool Active(Configuration c) => !c.FirstBoardScoreEnabled && c.CrucibleTeamMode is CrucibleTeamMode.FirstMasterLegendary or CrucibleTeamMode.FirstMasterXpRun;
    public static bool XpRun(Configuration c) => !c.FirstBoardScoreEnabled && c.CrucibleTeamMode == CrucibleTeamMode.FirstMasterXpRun;
    public static void ApplyXpRun(Configuration c)
    {
        c.CrucibleTeamMode=CrucibleTeamMode.FirstMasterXpRun;
        c.FirstBoardScoreEnabled=false;c.SelectedCrucibleBoard="特一盤";
        c.CombatOnlyEnabled=true;c.CombatOnlyBeastSelectionEnabled=true;
        c.AutoCruciblePetPartyEnabled=true;c.AutoCrucibleWhistleAssignEnabled=true;
        c.AutoCombatEnabled=true;c.BossModDodgeEnabled=true;c.ClassMinMaxRankScanEnabled=true;
        c.AutoEnterCrucibleEnabled=false;c.AutoSelectBoardEnabled=false;
        c.CrucibleAutoDutyModeEnabled=false;c.AutoCrucibleShopBuyEnabled=false;
        // User request 2026-11-14: XP run handles chests, shops, results and loot automatically
        // (gear by CrucibleGearPolicy: mitigation, then damage). Entry stays manual.
        c.FirstMasterXpBoardMenusEnabled=true;
        c.CrucibleShopBuyHealing=true;c.CrucibleShopBuyGear=true;
    }
    /// <summary>Chest/shop/result/loot screens are automated for the First Master XP run.</summary>
    public static bool BoardMenus(Configuration c) => XpRun(c) && c.FirstMasterXpBoardMenusEnabled;
    /// <summary>The menu services run when full auto-entry is on, or for the First Master XP run.</summary>
    public static bool MenusAllowed(Configuration c) => c.AutoEnterCrucibleEnabled || BoardMenus(c);
    public static uint[] Whistles => [1,50,33];
    public static IReadOnlyList<BeastPet> Team(IEnumerable<BeastPet> pets,out string reason)
    {
        var owned=pets.DistinctBy(p=>p.PetRowId).ToArray();
        var trio=Whistles.Select(no=>owned.FirstOrDefault(p=>p.XbmRowId==no && p.Classification==1)).OfType<BeastPet>().ToArray();
        reason=trio.Length==3 ? string.Empty : "Capture Cu Sith, Behemoth and Coeurl before preparing this roster. No substitutions.";
        return trio.Length==3 ? trio : [];
    }
    public static void Apply(Configuration c)
    {
        c.CrucibleTeamMode=CrucibleTeamMode.FirstMasterLegendary;
        c.FirstBoardScoreEnabled=false;
        c.SelectedCrucibleBoard="特一盤";
        c.CombatOnlyEnabled=true;c.CombatOnlyBeastSelectionEnabled=true;
        c.AutoCruciblePetPartyEnabled=true;c.AutoCrucibleWhistleAssignEnabled=true;
        c.AutoCombatEnabled=true;c.BossModDodgeEnabled=true;
        c.RecoverStoppedRsrEnabled=true;c.ShowAutomationDiagnostics=true;
        c.AutoRecoveryItemEnabled=true;c.AutoRecoveryItemHpThreshold=55;
        c.PrepareRecoveryBeforePull=true;c.AutoEmergencyMitigationEnabled=true;c.AutoConditionalRecoveryEnabled=true;
        c.AutoOffensiveItemsEnabled=true;c.AutoLootBoostEnabled=true;c.AutoMerchantEyeEnabled=true;
        c.AutoSelfBuffItemsEnabled=false;c.AutoCrucibleItemRulesEnabled=false;
        c.AutoEnterCrucibleEnabled=false;c.AutoSelectBoardEnabled=false;
        c.CrucibleAutoDutyModeEnabled=false;c.AutoCrucibleShopBuyEnabled=false;
        c.AutoFamiliarRecoveryEnabled=true;c.FamiliarRecoveryHpThreshold=45;
        // Player recovery still takes priority; shops and campsite healing remain manual.
    }
}
