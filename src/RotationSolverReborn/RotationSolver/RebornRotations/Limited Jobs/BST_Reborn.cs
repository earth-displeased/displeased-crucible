using System.ComponentModel;

namespace RotationSolver.RebornRotations.Melee;

[Rotation("Reborn", CombatType.PvE, GameVersion = "7.57")]
[SourceCode(Path = "main/RebornRotations/Limited Jobs/BST_Reborn.cs")]
public sealed class BST_Reborn : BeastmasterRotation
{
	[RotationConfig(CombatType.PvE, Name = "First Horn")]
	private HornOrder FirstHorn { get; set; } = HornOrder.FirstBattlehorn;

	[RotationConfig(CombatType.PvE, Name = "Second Horn")]
	private HornOrder SecondHorn { get; set; } = HornOrder.SecondBattlehorn;

	[RotationConfig(CombatType.PvE, Name = "Third Horn")]
	private HornOrder ThirdHorn { get; set; } = HornOrder.ThirdBattlehorn;

	[RotationConfig(CombatType.PvE, Name = "What to use One with Nature on for the First Horn")]
	private OneWithNatureOrder HornNatureFirst { get; set; } = OneWithNatureOrder.Tempered;

	[RotationConfig(CombatType.PvE, Name = "What to use One with Nature on for the Second Horn")]
	private OneWithNatureOrder HornNatureSecond { get; set; } = OneWithNatureOrder.Tempered;

	[RotationConfig(CombatType.PvE, Name = "What to use One with Nature on for the Third Horn")]
	private OneWithNatureOrder HornNatureThird { get; set; } = OneWithNatureOrder.Borrow;

	[RotationConfig(CombatType.PvE, Name = "Use Snarl and Challenge based on your HP and your familiar's HP (Crucible only)",
		Tooltip = "Off: Snarl is never used.\nOn: Snarl gives your damage to the familiar and Challenge takes it back, based on the settings below. If one of you has to die, it will be the familiar.")]
	private bool FamiliarTankSwap { get; set; } = false;

	[RotationConfig(CombatType.PvE, Name = "Only use Snarl and Challenge on Last Horn", Parent = nameof(FamiliarTankSwap))]
	private bool LasthornOnly { get; set; } = true;

	[RotationConfig(CombatType.PvE, Name = "How to decide who takes damage", Parent = nameof(FamiliarTankSwap))]
	private TankSwapMode TankSwap { get; set; } = TankSwapMode.Thresholds;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Snarl when familiar HP is at or above", Parent = nameof(TankSwap), ParentValue = ThresholdsDescription)]
	private float SnarlFamiliarHp { get; set; } = 0.7f;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Challenge when familiar HP is at or below", Parent = nameof(TankSwap), ParentValue = ThresholdsDescription)]
	private float ChallengeFamiliarHp { get; set; } = 0.3f;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Challenge when your HP is this much higher than the familiar's", Parent = nameof(TankSwap), ParentValue = DynamicDescription,
		Tooltip = "The familiar takes damage while its HP is at or above yours. Once your HP is higher than the familiar's by this much, Challenge takes the damage back. Higher values mean fewer swaps.")]
	private float DynamicSwapMargin { get; set; } = 0.2f;

	[Range(0, 1, ConfigUnitType.Percent)]
	[RotationConfig(CombatType.PvE, Name = "Your HP floor: at or below this the familiar takes all damage, even if it dies", Parent = nameof(FamiliarTankSwap))]
	private float PlayerHpFloor { get; set; } = 0.3f;

	private const string ThresholdsDescription = "Familiar HP thresholds";
	private const string DynamicDescription = "Dynamic (compare your HP and the familiar's HP)";

	public enum TankSwapMode : byte
	{
		[Description(ThresholdsDescription)]
		Thresholds,

		[Description(DynamicDescription)]
		Dynamic,
	}

    private readonly TreantMeteorPolicy treantMeteor = new();
    private TreantMeteorPolicy.Decision treantMeteorDecision;
    private string treantMeteorLastStatus = string.Empty;
    private bool ObserveTreantMeteor()
    {
        var boss = FirstBoardScoreIntegration.FirstMasterTreant && InCombat
            ? ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a => a.BaseId == 0x4CCD && a.CurrentHp > 0 && a.IsTargetable) : null;
        var slugs = boss == null ? [] : ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>()
            .Where(a => a.BaseId == 0x4CCE && a.CurrentHp > 0 && a.IsTargetable).ToArray();
        var near = Player is {} player ? slugs.Count(a => Vector3.DistanceSquared(a.Position,player.Position) <= 100f) : 0;
        treantMeteorDecision = treantMeteor.Update(boss?.GameObjectId ?? 0,DateTime.UtcNow,HornReady(50),
            CurrentPet == BeastmasterPet.Behemoth,HasOneWithNature,slugs.Length,near,
            Player?.GetHealthRatio() < .35f || boss?.GetHealthRatio() < .1f);
        if(boss == null) { treantMeteorLastStatus = string.Empty; return false; }
        var status = CurrentPet == BeastmasterPet.Behemoth
            ? treantMeteorDecision.Release ? "Meteor ready for add cluster / fallback" : treantMeteorDecision.HoldDismiss ? "Holding pet for Meteor execution" : "Gathering adds before Meteor"
            : treantMeteorDecision.Summon ? "Behemoth ready for nearby adds / bounded fallback" : "Reserving Behemoth; other attacks continue";
        if(status != treantMeteorLastStatus)
        {
            ECommons.DalamudServices.Svc.Log.Information("[First Master Treant] "+status+$" (slugs {slugs.Length}, within 10y {near})");
            treantMeteorLastStatus=status;
        }
        return true;
    }
    private readonly BorgnyToxicMassPolicy borgnyMass = new();
    private BorgnyToxicMassPolicy.Decision borgnyMassDecision;
    private string borgnyMassLastStatus = string.Empty;
    /// <summary>First Master profile only: hold summons/releases for Borgny's Toxic Mass. See BorgnyToxicMassPolicy.</summary>
    private BorgnyToxicMassPolicy.Decision ObserveBorgnyMass()
    {
        var boss = FirstBoardScoreIntegration.FirstMaster && InCombat
            ? ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a => a.BaseId == BorgnyToxicMassPolicy.Borgny && a.CurrentHp > 0)
            : null;
        var massAlive = boss != null && ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>()
            .Any(a => a.BaseId == BorgnyToxicMassPolicy.ToxicMass && a.CurrentHp > 0 && a.IsTargetable);
        var playerOnMass = massAlive && CurrentTarget is { } t && t.BaseId == BorgnyToxicMassPolicy.ToxicMass && t.CurrentHp > 0;
        var familiar = Familiar;
        var familiarOnMass = familiar != null && playerOnMass && CurrentTarget is { } mt && familiar.TargetObjectId == mt.GameObjectId;
        var massTargeted = BorgnyToxicMassPolicy.MassTargeted(playerOnMass, FamiliarHasHp(familiar), familiarOnMass);
        var phlegm = boss != null && boss.IsCasting && boss.CastActionId == BorgnyToxicMassPolicy.WrigglingPhlegm;
        var emergency = Player is { } p && p.GetHealthRatio() < .3f;
        borgnyMassDecision = borgnyMass.Update(boss?.GameObjectId ?? 0, DateTime.UtcNow, phlegm, massAlive, massTargeted, emergency);
        if (boss == null) { borgnyMassLastStatus = string.Empty; return borgnyMassDecision; }
        var status = borgnyMassDecision.Burst ? "Toxic Mass targeted: summons and release allowed"
            : borgnyMassDecision.Reserve ? "Holding summon / release / Parting Blow for Toxic Mass"
            : emergency ? "Low HP: Toxic Mass reservation released" : "Normal rotation";
        if (status != borgnyMassLastStatus)
        {
            ECommons.DalamudServices.Svc.Log.Information($"[First Master Borgny] {status} (pet {CurrentPet}, mass alive {massAlive})");
            borgnyMassLastStatus = status;
        }
        return borgnyMassDecision;
    }

    private bool TryTreantAwareHorn(int horn,out IAction? act)
    {
        act=null;
        if(FirstBoardScoreIntegration.FirstMasterTreant && CurrentPets.Length > horn && CurrentPets[horn] == 50 && !treantMeteorDecision.Summon) return false;
        return horn switch {0=>FirstBattlehornPvE.CanUse(out act),1=>SecondBattlehornPvE.CanUse(out act),2=>ThirdBattlehornPvE.CanUse(out act),_=>false};
    }

    private static bool VilekinScoreActive => FirstBoardScoreIntegration.Vilekin && IsInCrucible
        && CurrentPets.Length >= 3 && CurrentPets.Contains((byte)44) && CurrentPets.Contains((byte)16) && CurrentPets.Contains((byte)10);

    private static bool BeastkinClearActive => FirstBoardScoreIntegration.BeastkinClear && IsInCrucible
        && CurrentPets.Length>=3 && CurrentPets.ToArray().All(p=>p is 0 or 1 or 26 or 33)
        && (CurrentPets.Contains((byte)1) || CurrentPets.Contains((byte)26) || CurrentPets.Contains((byte)33));
    private ulong beastkinEncounter;
    private int beastkinPendingSummon;
    private DateTime beastkinReleaseHoldUntil;
    private bool buffaloReleased;
    private bool buffaloReleaseRequested;
    private string beastkinLastDecision=string.Empty;
    private void BeastkinDecision(string decision)
    {
        if(beastkinLastDecision==decision) return;
        beastkinLastDecision=decision;
        ECommons.DalamudServices.Svc.Log.Information("[Third Board Beastkin] "+decision);
    }
    private void ObserveBeastkin()
    {
        var boss=ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a=>a.CurrentHp>0 && a.BaseId is 0x4C8E or 0x4C93 or 0x4C96 or 0x4C9B or 0x4C9E or 0x4CA1 or 0x4CA5 or 0x4CAA);
        if(boss!=null && boss.GameObjectId!=beastkinEncounter)
        { beastkinEncounter=boss.GameObjectId;beastkinPendingSummon=0;beastkinReleaseHoldUntil=default;buffaloReleased=false;buffaloReleaseRequested=false;beastkinLastDecision=string.Empty; }
        if((int)CurrentPet==beastkinPendingSummon) beastkinPendingSummon=0;
        if(CurrentPet==BeastmasterPet.Buffalo && buffaloReleaseRequested && !HasOneWithNature) buffaloReleased=true;
    }
    private static IBattleChara? YmirBody => ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a=>a.BaseId==0x4C93 && a.CurrentHp>0);
    private static bool YmirExposed => YmirBody is {} body && !body.StatusList.Any(s=>s.StatusId==2198);
    private bool HornReady(int species)
    {
        var horn=CurrentPets.IndexOf((byte)species);
        return horn switch {0=>FirstBattlehornPvE.Cooldown.HasOneCharge,1=>SecondBattlehornPvE.Cooldown.HasOneCharge,2=>ThirdBattlehornPvE.Cooldown.HasOneCharge,_=>false};
    }
    private bool SummonSpecies(int species,out IAction? act)
    {
        act=null;var horn=CurrentPets.IndexOf((byte)species);
        return horn switch {0=>FirstBattlehornPvE.CanUse(out act),1=>SecondBattlehornPvE.CanUse(out act),2=>ThirdBattlehornPvE.CanUse(out act),_=>false};
    }
    private bool TryBeastkinControl(out IAction? act)
    {
        act=null;ObserveBeastkin();
        if(BeastskinPvE.CanUse(out act)) { BeastkinDecision("Activating Beastskin");return true; }
        if(!HasBeastskin && KinshipKinType!=BeastmasterKinType.Beastkin && HasOneWithNature
            && (CurrentPet!=BeastmasterPet.Buffalo || YmirBody==null))
        {
            if(BorrowPvE_47238.CanUse(out act) || BorrowPvE.CanUse(out act)) { BeastkinDecision("Borrowing Beastkin defense");return true; }
            return false;
        }
        if(CurrentPet==BeastmasterPet.Buffalo && buffaloReleased) return false;
        var body=YmirBody;
        var closeToYmir=body!=null && FamiliarHasHp(Familiar) && Vector3.Distance(Familiar!.Position,body.Position)-body.HitboxRadius<=5f;
        var dangerous=ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().Any(a=>a.CurrentHp>0 && a.IsCasting && a.CastActionId == 48479);
        var release=ThirdBoardBeastkinPolicy.Release((int)CurrentPet,HasOneWithNature,HasBeastskin,
            body==null || YmirExposed,body==null || closeToYmir,dangerous);
        if(release && (TemperedReleasePvE_47092.CanUse(out act) || TemperedReleasePvE.CanUse(out act)))
        {
            beastkinReleaseHoldUntil=DateTime.UtcNow.AddSeconds(4); // Pet execution follows the command; do not cancel its pending hit.
            if(CurrentPet==BeastmasterPet.Buffalo) buffaloReleaseRequested=true;
            BeastkinDecision("Controlled ability: "+CurrentPet+(CurrentPet==BeastmasterPet.Buffalo ? " (stun window)":""));return true;
        }
        return false;
    }
    private bool TryBeastkinSummons(out IAction? act)
    {
        act=null;ObserveBeastkin();
        if(BeastskinPvE.CanUse(out act)) { BeastkinDecision("Activating Beastskin");return true; }
        var shell=ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>().Where(a=>a.BaseId==0x4C94 && a.CurrentHp>0 && a.IsTargetable).Select(a=>a.GetHealthRatio()).DefaultIfEmpty(1f).Min();
        var prepare=ThirdBoardBeastkinPolicy.PrepareBuffalo(YmirBody!=null,YmirExposed,shell,buffaloReleased);
        var needsBorrow=!HasBeastskin && KinshipKinType!=BeastmasterKinType.Beastkin;
        if(ActiveBattlehorn==0)
        {
            var pick=beastkinPendingSummon!=0 && HornReady(beastkinPendingSummon) ? beastkinPendingSummon
                : ThirdBoardBeastkinPolicy.Opening(needsBorrow,HornReady(1),HornReady(33),HornReady(26),prepare);
            if(pick!=0 && SummonSpecies(pick,out act)) { BeastkinDecision("Summoning species "+pick);return true; }
            return false;
        }
        // Borrow and controlled attacks do not make the familiar disposable. Keep its autos/Trick
        // until it is endangered; otherwise two early Parting Blows leave no familiar for a minute.
        var desired=ThirdBoardBeastkinPolicy.NextSwap((int)CurrentPet,
            FamiliarHasHp(Familiar) ? Familiar!.GetHealthRatio() : 0f,prepare,YmirBody!=null,
            HornReady(1),HornReady(33),HornReady(26));
        if(desired!=0 && ThirdBoardBeastkinPolicy.CanSwap(DateTime.UtcNow,beastkinReleaseHoldUntil,vilekinPetHoldUntil) && PartingBlowPvE.CanUse(out act))
        { beastkinPendingSummon=desired; BeastkinDecision("Swapping to species "+desired+"; retain current pet if no horn is ready");return true; }
        BeastkinDecision(CurrentPet==BeastmasterPet.Buffalo && !buffaloReleased ? "Holding Buffalo for exposed Ymir / dangerous cast" : "Sustained damage with "+CurrentPet);
        return false;
    }

    private DateTime vilekinPetHoldUntil;
    private byte vilekinObservedHorn;
    private bool vilekinNatureObserved;
    private void ObserveVilekinNature()
    {
        if (vilekinObservedHorn != ActiveBattlehorn)
        { vilekinObservedHorn = ActiveBattlehorn; vilekinNatureObserved = false; }
        if (HasOneWithNature) vilekinNatureObserved = true;
    }

    private bool TryVilekinRelease(out IAction? act)
    {
        act = null;
        if (!InCombat || CurrentTarget is not { } target || FirstBoardScoreIntegration.Guarded(target)) return false;
        if (FirstBoardVilekinPolicy.HoldYmirReserve(FirstBoardScoreIntegration.ThirdBoard,(int)CurrentPet,
            FirstBoardScoreIntegration.YmirPairAlive,FamiliarHasHp(Familiar)?Familiar!.GetHealthRatio():0)) return false;
        if (CurrentPet == BeastmasterPet.Wespe && FirstBoardScoreIntegration.ThirdBoard && target.BaseId==0x4C94) return false; // Reserve Final Sting for a boss, not a disposable Ymir shell.
        if (CurrentPet == BeastmasterPet.Wespe && (FirstBoardScoreIntegration.HasGuard(target) || FirstBoardScoreIntegration.HoldLinkedBossFinisher(target))) return false;
        if (!FirstBoardVilekinPolicy.Release((int)CurrentPet, InCombat, HasOneWithNature, FirstBoardScoreIntegration.Guarded(target), target.GetHealthRatio(), FirstBoardScoreIntegration.PhysicalVulnerabilityRemaining(target), FamiliarHasHp(Familiar) && Familiar!.GetHealthRatio() < 0.25f)) return false;
        return TemperedReleasePvE_47092.CanUse(out act) || TemperedReleasePvE.CanUse(out act);
    }

	#region Countdown logic
	// Defines logic for actions to take during the countdown before combat starts.
	protected override IAction? CountDownAction(float remainTime)
	{

		return base.CountDownAction(remainTime);
	}
	#endregion

    private DateTime firstMasterCoverUntil;
    private DateTime nextFirstMasterDefenseLog;
    private void LogFirstMasterDefense(string message)
    {
        if(DateTime.UtcNow < nextFirstMasterDefenseLog) return;
        nextFirstMasterDefenseLog=DateTime.UtcNow.AddSeconds(2);
        ECommons.DalamudServices.Svc.Log.Information("[First Master defense] "+message);
    }
    private ulong firstMasterCoverSource;
    private bool FirstMasterCoverHold => FirstBoardScoreIntegration.MasterBoard && DateTime.UtcNow < firstMasterCoverUntil;
    private bool TryFirstMasterCover(out IAction? act)
    {
        act=null;
        if(!FirstBoardScoreIntegration.MasterBoard || !InCombat || Player is not {} player)
        { firstMasterCoverUntil=default;firstMasterCoverSource=0;return false; }
        var threat=ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>()
            .FirstOrDefault(a=>a.CurrentHp>0 && a.IsCasting && (FirstMasterDefensePolicy.CoverCast(a.BaseId,a.CastActionId) || FirstBoardScoreIntegration.SecondMaster && SecondMasterDefensePolicy.CoverCast(a.BaseId,a.CastActionId)));
        if(threat != null && (!FamiliarHasHp(Familiar) || FirstMasterDefensePolicy.CanCoverCast(player.GetHealthRatio(),Familiar!.GetHealthRatio())))
        {
            firstMasterCoverUntil=DateTime.UtcNow.AddSeconds(Math.Max(0,threat.TotalCastTime-threat.CurrentCastTime)+FirstMasterDefensePolicy.AfterCastHold(threat.BaseId,threat.CastActionId));
            if(firstMasterCoverSource!=threat.GameObjectId)
            {
                ECommons.DalamudServices.Svc.Log.Information($"[First Master defense] Preparing pet cover for {threat.Name}, cast {threat.CastActionId}");
                firstMasterCoverSource=threat.GameObjectId;
            }
        }
        if(!FirstMasterCoverHold) {firstMasterCoverSource=0;return false;}
        if(!FamiliarHasHp(Familiar))
        {
            foreach(var species in new[]{1,33,50}) if(SummonSpecies(species,out act)) return true;
            return false;
        }
        if(!player.StatusList.Any(s=>s.StatusId==2413) && SnarlPvE.CanUse(out act,skipTargetStatusNeedCheck:true))
        {
            LogFirstMasterDefense("Snarl requested for protected cast");
            return true;
        }
        return false;
    }

	#region Emergency Logic
	protected override bool EmergencyAbility(IAction nextGCD, out IAction? act)
	{
        if(TryFirstMasterCover(out act)) return true;
        if(FirstMasterCoverHold) {act=null;return false;}

        if (InCombat && (VilekinScoreActive || BeastkinClearActive) && FirstBoardScoreIntegration.ThirdBoard && FamiliarHasHp(Familiar) && Player is {} coveredPlayer)
        {
            if (FirstBoardScoreIntegration.ThirdIncomingDamage
                && (!BeastkinClearActive || ThirdBoardDefensePolicy.CanCoverCast(coveredPlayer.GetHealthRatio(),Familiar!.GetHealthRatio())))
                vilekinPetHoldUntil=DateTime.UtcNow.AddSeconds(1);
            if (DateTime.UtcNow<vilekinPetHoldUntil)
            {
                if (!coveredPlayer.StatusList.Any(s=>s.StatusId==2413) && SnarlPvE.CanUse(out act,skipTargetStatusNeedCheck:true)) return true;
                act=null; return false; // Keep cover through cast resolution; do not immediately Challenge it away.
            }
        }
		if (InCombat && (FamiliarTankSwap || VilekinScoreActive || BeastkinClearActive || FirstBoardScoreIntegration.MasterBoard))
		{
			if (VilekinScoreActive || BeastkinClearActive || FirstBoardScoreIntegration.MasterBoard || !LasthornOnly || (LasthornOnly && OnLastHorn))
			{
				if (FamiliarTankSwapAbility(out act))
				{
					return true;
				}
			}
		}

		return base.EmergencyAbility(nextGCD, out act);
	}

	private bool FamiliarTankSwapAbility(out IAction? act)
	{
		act = null;

		var familiar = Familiar;
		if (!FamiliarHasHp(familiar) || Player == null)
		{
			return false;
		}

		var playerHp = Player.GetHealthRatio();
		var familiarHp = familiar!.GetHealthRatio();

		// at or below the floor the player never takes damage back, so the familiar dies first.
		var playerCritical = playerHp <= PlayerHpFloor;

		bool shouldSnarl;
		bool shouldChallenge;
		if(FirstBoardScoreIntegration.SecondMasterLauda)
        {
            shouldSnarl=SecondMasterDefensePolicy.LaudaCover(playerHp,familiarHp);
            shouldChallenge=SecondMasterDefensePolicy.LaudaTakeBack(playerHp,familiarHp);
        }
		else if(FirstBoardScoreIntegration.MasterBoard)
        {
            shouldSnarl=FirstMasterDefensePolicy.Cover(playerHp,familiarHp);
            shouldChallenge=FirstMasterDefensePolicy.TakeBack(playerHp,familiarHp);
        }
        else if(BeastkinClearActive)
        {
            shouldSnarl=ThirdBoardDefensePolicy.PreserveCuCover((int)CurrentPet,playerCritical)
                && ThirdBoardDefensePolicy.Cover(playerHp,familiarHp,playerCritical);
            shouldChallenge=ThirdBoardDefensePolicy.PreserveCuTakeBack((int)CurrentPet,playerHp,playerCritical)
                || ThirdBoardDefensePolicy.TakeBack(playerHp,familiarHp,playerCritical);
        }
        else if (TankSwap == TankSwapMode.Dynamic)
		{
			shouldSnarl = playerCritical || familiarHp >= playerHp;
			shouldChallenge = !playerCritical && playerHp > familiarHp + DynamicSwapMargin;
		}
		else
		{
			shouldSnarl = playerCritical || (familiarHp >= SnarlFamiliarHp && familiarHp > ChallengeFamiliarHp);
			shouldChallenge = !playerCritical && familiarHp <= ChallengeFamiliarHp;
		}

		if (shouldSnarl)
		{
			if (!Player.StatusList.Any(s=>s.StatusId==2413) && SnarlPvE.CanUse(out act, skipTargetStatusNeedCheck: true))
            {
                if(FirstBoardScoreIntegration.MasterBoard) LogFirstMasterDefense($"Sharing damage: player {playerHp:P0}, {CurrentPet} {familiarHp:P0}");
                return true;
			}
		}

		if (shouldChallenge && IsFamiliarTanking(familiar))
		{
			if (ChallengePvE.CanUse(out act))
            {
                if(FirstBoardScoreIntegration.MasterBoard) LogFirstMasterDefense($"Taking damage back: player {playerHp:P0}, {CurrentPet} {familiarHp:P0}");
                return true;
			}
		}

		return false;
	}
	#endregion

	#region oGCD Logic
	protected override bool AttackAbility(IAction nextGCD, out IAction? act)
	{
        var treantTiming = ObserveTreantMeteor();
        var borgnyHold = ObserveBorgnyMass().Reserve; // Trick, releases and Parting Blow wait for Borgny's Toxic Mass.
        if(treantTiming && CurrentPet == BeastmasterPet.Behemoth && treantMeteorDecision.Release
            && (TemperedReleasePvE_47092.CanUse(out act) || TemperedReleasePvE.CanUse(out act))) return true;

        if (CurrentTarget is { } guardedTarget && FirstBoardScoreIntegration.Guarded(guardedTarget))
        { act = null; return false; }

        // Siren: save opening charges for Summon instead of spending them as boss filler.
        if(FirstBoardScoreIntegration.SirenEncounter && ShieldChargePvE.CanUse(out act,usedUp:true))
        {
            var hits=ShieldChargePvE.Target.AffectedTargets;
            var crawling=ShieldChargePvE.Target.Target.BaseId==0x4CA4 || hits.Any(a=>a.BaseId==0x4CA4);
            var zombies=hits.Count(a=>a.BaseId==0x4CA3);
            var bound=Player?.StatusList.Any(s=>s.StatusId==5710)==true;
            if(SirenChargePolicy.Use(crawling,zombies,ShieldChargePvE.Cooldown.CurrentCharges,bound,Player?.GetHealthRatio()??1f)) return true;
            act=null;
        }
        if (BeastkinClearActive && TryBeastkinControl(out act)) return true;
        if (VilekinScoreActive) { ObserveVilekinNature(); if (TryVilekinRelease(out act)) return true; }

		var resultingTP = TPCount + RallyTPRestore;

		var shouldRally =
			(MasteredInstinct == 3 && InstinctualMasteryTrait.EnoughLevel && (HasMoonstalker || HasSunstrider)) ||
			(resultingTP >= 100 && resultingTP <= 250 && !InstinctualMasteryTrait.EnoughLevel);

		if (shouldRally)
		{
			if (RallyPvE.CanUse(out act))
			{
				return true;
			}
		}

		var resultingPetTP = PetTPCount + RallyingCheerTPRestore;

		var shouldRallyCheer = resultingPetTP >= 100 && resultingPetTP <= 250;

		if (shouldRallyCheer)
		{
			if (RallyingCheerPvE.CanUse(out act))
			{
				return true;
			}
		}

		if (BrutalRagePvE.CanUse(out act, usedUp: true))
		{
			return true;
		}
		if (HawkishTalonsPvE.CanUse(out act, usedUp: true))
		{
			return true;
		}
		if (RisenFallPvE.CanUse(out act, usedUp: true))
		{
			return true;
		}
		if (CalamityPvE.CanUse(out act, usedUp: true))
		{
			return true;
		}

		if (NaturalInstinct == 3 || MasteredInstinct < 3)
		{
			if (TPCount >= 100 && !IsLastAction(true, PartingBlowPvE))
			{
				if (!borgnyHold && TrickPvE.CanUse(out act, skipStatusNeed: true))
				{
					return true;
				}
			}

			if (AvalancheAxePvE.CanUse(out act, usedUp: true))
			{
				return true;
			}
			if (MistralAxePvE.CanUse(out act, usedUp: true))
			{
				return true;
			}
			if (SpinningAxePvE.CanUse(out act, usedUp: true))
			{
				return true;
			}
			if (GaleAxePvE.CanUse(out act, usedUp: true))
			{
				return true;
			}
		}

		if (MasteredInstinct == 3 || NaturalInstinct < 3)
		{
			if (PetTPCount >= 100)
			{
				if (GaleAxePvE.CanUse(out act, skipStatusNeed: true, usedUp: true))
				{
					return true;
				}

				if (SpinningAxePvE.CanUse(out act, skipStatusNeed: true, usedUp: true))
				{
					return true;
				}

				if (MistralAxePvE.CanUse(out act, skipStatusNeed: true, usedUp: true))
				{
					return true;
				}

				if (AvalancheAxePvE.CanUse(out act, skipStatusNeed: true, usedUp: true))
				{
					return true;
				}
			}

			if (!IsLastAction(true, PartingBlowPvE))
			{
				if (!borgnyHold && TrickPvE.CanUse(out act))
				{
					return true;
				}
			}
		}

        if (!VilekinScoreActive && !BeastkinClearActive && !borgnyHold)
        {
		if (ActiveBattlehorn == 1 && !(treantTiming && CurrentPet == BeastmasterPet.Behemoth))
		{
			if (HornNatureFirst == OneWithNatureOrder.Tempered)
			{
				if (TemperedReleasePvE.IsEnabled)
				{
					if (TemperedReleasePvE_47092.CanUse(out act))
					{
						return true;
					}
				}

				if (TemperedReleasePvE.CanUse(out act))
				{
					return true;
				}
			}

			if (HornNatureFirst == OneWithNatureOrder.Borrow)
			{
				if (BorrowPvE.CanUse(out act))
				{
					return true;
				}
			}
		}

		if (ActiveBattlehorn == 2 && !(treantTiming && CurrentPet == BeastmasterPet.Behemoth))
		{
			if (HornNatureSecond == OneWithNatureOrder.Tempered)
			{
				if (TemperedReleasePvE.IsEnabled)
				{
					if (TemperedReleasePvE_47092.CanUse(out act))
					{
						return true;
					}
				}

				if (TemperedReleasePvE.CanUse(out act))
				{
					return true;
				}
			}

			if (HornNatureSecond == OneWithNatureOrder.Borrow)
			{
				if (BorrowPvE.CanUse(out act))
				{
					return true;
				}
			}
		}

		if (ActiveBattlehorn == 3 && !(treantTiming && CurrentPet == BeastmasterPet.Behemoth))
		{
			if (HornNatureThird == OneWithNatureOrder.Tempered)
			{
				if (TemperedReleasePvE.IsEnabled)
				{
					if (TemperedReleasePvE_47092.CanUse(out act))
					{
						return true;
					}
				}

				if (TemperedReleasePvE.CanUse(out act))
				{
					return true;
				}
			}

			if (HornNatureThird == OneWithNatureOrder.Borrow)
			{
				if (BorrowPvE.CanUse(out act))
				{
					return true;
				}
			}
		}

		if (!BorrowReady && !TemperedReleaseReady)
		{
			if (!FirstMasterCoverHold && !(treantTiming && (treantMeteorDecision.HoldDismiss || !treantMeteorDecision.Summon && !HornReady(1) && !HornReady(33))) && AnyBattlehornReady && ((!IsLastAction(true, TrickPvE) && TrickPvE.Cooldown.HasOneCharge) || !TrickPvE.EnoughLevel))
			{
				if (PartingBlowPvE.CanUse(out act))
				{
					return true;
				}
			}
		}

        }

		if (BrutalRagePvE.CanUse(out act, usedUp: true, skipStatusNeed: true))
		{
			return true;
		}
		if (HawkishTalonsPvE.CanUse(out act, usedUp: true, skipStatusNeed: true))
		{
			return true;
		}
		if (RisenFallPvE.CanUse(out act, usedUp: true, skipStatusNeed: true))
		{
			return true;
		}
		if (CalamityPvE.CanUse(out act, usedUp: true, skipStatusNeed: true))
		{
			return true;
		}

		if (!FirstBoardScoreIntegration.SirenEncounter && !FirstBoardScoreIntegration.ReserveGuttlerRetreat && ShieldChargePvE.CanUse(out act, usedUp: CurrentPet != BeastmasterPet.None))
		{
			return true;
		}

		if (SoulCrushPvE.CanUse(out act))
		{
			return true;
		}

		if (SeedsowerPvE.CanUse(out act))
		{
			return true;
		}

		return base.AttackAbility(nextGCD, out act);
	}

	protected override bool GeneralAbility(IAction nextGCD, out IAction? act)
	{
        if(FirstMasterCoverHold) {act=null;return false;}

        if(ObserveTreantMeteor())
        {
            if(treantMeteorDecision.HoldDismiss) {act=null;return false;}
            if(treantMeteorDecision.Summon)
            {
                if(CurrentPet == BeastmasterPet.None) {if(SummonSpecies(50,out act)) return true;}
                else if(CurrentPet != BeastmasterPet.Behemoth && PartingBlowPvE.CanUse(out act)) return true;
            }
        }

        if (FirstBoardScoreIntegration.KeepAuto && CurrentTarget is { } guardTarget)
        {
            // Keep pet aggro for the whole guard, not merely until the player reaches its rear.
            if (FirstBoardScoreIntegration.HasGuard(guardTarget))
            {
                if (!FamiliarHasHp(Familiar))
                {
                    if (FirstBattlehornPvE.CanUse(out act)) return true;
                    if (SecondBattlehornPvE.CanUse(out act)) return true;
                    if (ThirdBattlehornPvE.CanUse(out act)) return true;
                }
                else if (!IsFamiliarTanking(Familiar) && SnarlPvE.CanUse(out act, skipTargetStatusNeedCheck: true)) return true;
                // Retain this pet throughout guard, even if its release was already used.
                act = null;
                return false;
            }
            else if (guardTarget.BaseId == 0x4B86 && FamiliarHasHp(Familiar) && IsFamiliarTanking(Familiar) && ChallengePvE.CanUse(out act)) return true;
        }

        if(BeastkinClearActive)
        {
            if(TryBeastkinSummons(out act)) return true;
            return base.GeneralAbility(nextGCD,out act);
        }
        if (VilekinScoreActive)
        {
            // Keep the damage-sharing familiar present until Deadly Hold actually resolves.
            if (CurrentTarget is { IsCasting: true, CastActionId: 48138 })
                vilekinPetHoldUntil = DateTime.UtcNow.AddSeconds(1);
            if (InCombat && DateTime.UtcNow < vilekinPetHoldUntil)
            {
                if (FamiliarHasHp(Familiar) && !IsFamiliarTanking(Familiar) && SnarlPvE.CanUse(out act, skipTargetStatusNeedCheck: true)) return true;
                act = null;
                return false;
            }
            if (InCombat && BeastskinPvE.CanUse(out act)) return true;
            ObserveVilekinNature();
            if (ActiveBattlehorn == 0)
            {
                // Species order, independent of the game's automatically registered horn slots.
                foreach (var species in new byte[] { 44, 16, 10 })
                {
                    var horn = CurrentPets.IndexOf(species);
                    if (horn == 0 && FirstBattlehornPvE.CanUse(out act)) return true;
                    if (horn == 1 && SecondBattlehornPvE.CanUse(out act)) return true;
                    if (horn == 2 && ThirdBattlehornPvE.CanUse(out act)) return true;
                }
            }
            // A successful release consumes One with Nature. Only then dismiss to advance.
            if (FirstBoardVilekinPolicy.Dismiss((int)CurrentPet, vilekinNatureObserved, HasOneWithNature))
                if (PartingBlowPvE.CanUse(out act)) return true;
            return base.GeneralAbility(nextGCD, out act);
        }

		if (InCombat)
		{
			if (BeastskinPvE.CanUse(out act))
			{
				return true;
			}
		}

		// this unbound usage is for UnnamedStatus_2552, special mech in crucible board 1 fight 1
		if (InCombat)
		{
			if (SnarlPvE.CanUse(out act))
			{
				return true;
			}
		}

		if (ActiveBattlehorn == 0 && ObserveBorgnyMass().Reserve)
		{
			return base.GeneralAbility(nextGCD, out act);
		}

		if (ActiveBattlehorn == 0)
		{
			act = null;

			if (FirstHorn switch
			{
				HornOrder.FirstBattlehorn => TryTreantAwareHorn(0, out act),
				HornOrder.SecondBattlehorn => TryTreantAwareHorn(1, out act),
				HornOrder.ThirdBattlehorn => TryTreantAwareHorn(2, out act),
				_ => false,
			})
			{
				return true;
			}

			if (SecondHorn switch
			{
				HornOrder.FirstBattlehorn => TryTreantAwareHorn(0, out act),
				HornOrder.SecondBattlehorn => TryTreantAwareHorn(1, out act),
				HornOrder.ThirdBattlehorn => TryTreantAwareHorn(2, out act),
				_ => false,
			})
			{
				return true;
			}

			if (ThirdHorn switch
			{
				HornOrder.FirstBattlehorn => TryTreantAwareHorn(0, out act),
				HornOrder.SecondBattlehorn => TryTreantAwareHorn(1, out act),
				HornOrder.ThirdBattlehorn => TryTreantAwareHorn(2, out act),
				_ => false,
			})
			{
				return true;
			}

			// Horn order fallback if the above fails for some reason
			if (TryTreantAwareHorn(0, out act))
			{
				return true;
			}

			if (TryTreantAwareHorn(1, out act))
			{
				return true;
			}

			if (TryTreantAwareHorn(2, out act))
			{
				return true;
			}
		}

		return base.GeneralAbility(nextGCD, out act);
	}

	protected override bool DefenseSingleAbility(IAction nextGCD, out IAction? act)
	{
		if (InCombat)
		{
			if (ScaleskinPvE.CanUse(out act))
			{
				return true;
			}
		}

		return base.DefenseSingleAbility(nextGCD, out act);
	}

	protected override bool DefenseAreaAbility(IAction nextGCD, out IAction? act)
	{
		if (InCombat)
		{
			if (ScaleskinPvE.CanUse(out act))
			{
				return true;
			}
		}

		return base.DefenseAreaAbility(nextGCD, out act);
	}

	protected override bool DispelAbility(IAction nextGCD, out IAction? act)
	{
		if (ScouringAshPvE.CanUse(out act))
		{
			return true;
		}

		return base.DispelAbility(nextGCD, out act);
	}

	protected override bool AntiKnockbackAbility(IAction nextGCD, out IAction? act)
	{
		if (VileskinPvE.CanUse(out act))
		{
			return true;
		}

		return base.AntiKnockbackAbility(nextGCD, out act);
	}

	protected override bool MoveForwardAbility(IAction nextGCD, out IAction? act)
	{

		return base.MoveForwardAbility(nextGCD, out act);
	}
	#endregion

	#region GCD Logic
	protected override bool MoveForwardGCD(out IAction? act)
	{

		return base.MoveForwardGCD(out act);
	}

	protected override bool GeneralGCD(out IAction? act)
	{
        if(FirstBoardScoreIntegration.FirstMaster && ECommons.DalamudServices.Svc.Objects.OfType<IBattleChara>()
            .Any(a=>a.BaseId==0x4CBF && a.CurrentHp>0 && a.IsTargetable && a.IsCasting && a.CastActionId==48689)
            && SmashAxePvE.CanUse(out act) && SmashAxePvE.Target.Target.BaseId==0x4CBF) return true;

        if (CurrentTarget is { } guardedTarget && FirstBoardScoreIntegration.Guarded(guardedTarget))
        { act = null; return false; }

		if (QuellingWavePvE.CanUse(out act))
		{
			return true;
		}

		if (ShieldsplitterPvE.CanUse(out act))
		{
			return true;
		}
		if (AxebladeBitePvE.CanUse(out act))
		{
			return true;
		}
		if (SmashAxePvE.CanUse(out act))
		{
			return true;
		}

		if (QuellingWavePvE.CanUse(out act, skipTargetStatusNeedCheck: true))
		{
			return true;
		}
		return base.GeneralGCD(out act);
	}
	#endregion
}