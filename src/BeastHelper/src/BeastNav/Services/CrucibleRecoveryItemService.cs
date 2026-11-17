using BeastNav.Models;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using HotbarSlot = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlot;
using HotbarSlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace BeastNav.Services;

/// <summary>
/// Auto-uses the Crucible's own in-duty items — the recovery potion
/// ("魔獣回復薬" and its variants) on the player below
/// <see cref="Configuration.AutoRecoveryItemHpThreshold"/>, self-buff items
/// (回避の書/反射の書/時の砂/魔獣剛力薬) on the player as soon as they're off
/// cooldown, and offensive items (吸血鬼の牙 and the six elemental 牙) on the
/// current hostile target as soon as they're off cooldown.
/// </summary>
/// <remarks>
/// None of these are normal inventory potions — they're distributed inside
/// the duty and can only be used through the instance's own hotbar-slot
/// mapping (a dedicated agent, AgentId 497, remaps inventory slot to hotbar
/// display slot every time it's refreshed), not a plain ActionManager.UseAction
/// call. Ported from BOCXIV/Beastmaster-JP's BeastmasterCrucibleItemService,
/// whose 2026-09-17 update widened its own item list from just the 3 recovery
/// tiers to the full set below — the item ids, the shared 2-second cooldown
/// across all of them, and the "no CanUseOnSelf gate on non-recovery items"
/// behavior are all carried over as-is. The two native offsets it needs are
/// found by signature scan; if either fails to resolve (e.g. a future game
/// patch changes the pattern) the feature quietly disables itself instead of
/// acting on a stale address.
/// </remarks>
/// <remarks>
/// Self-buff and offensive items use a simple "use it the instant it's
/// available" policy — no attempt is made to time a buff against an
/// incoming attack, or to match an elemental 牙 against the target's actual
/// weakness (unlike GluttonyCombo's BST_CrucibleAdvisor, which does score
/// targets by elemental weakness offline; that data isn't wired up here).
/// </remarks>
/// <remarks>
/// Unattended item use — against the FFXIV ToS, self-responsibility. Off by
/// default. Only acts inside the Crucible arena territories, only in
/// combat, and only against the local player (recovery/self-buff) or the
/// player's own current hostile target (offensive); never anyone else.
/// </remarks>
public sealed unsafe class CrucibleRecoveryItemService
{
    private const string UseStatusSignature = "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 41 8B F8 48 8B D9 83 FA 0A 0F 83 ?? ?? ?? ?? 8B C2 48 8D 14 40 48 8D 34 91 0F B7 86 84 23 00 00";
    private const string RefreshMappingSignature = "48 89 5C 24 18 57 48 83 EC 30 48 8B D9 E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 0F 84 ?? ?? ?? ?? 48 89 6C 24 40 33 ED";
    private const int SlotCount = 10;
    private const int MappingOffset = 80;
    private const int MappingStride = 8;
    private const int InventoryOffset = 9092;
    private const int InventoryStride = 12;
    private const uint FirstRecoveryActionId = 46959;
    private const ushort FirstRecoveryItemId = 76;
    private static readonly TimeSpan UseInterval = TimeSpan.FromSeconds(2);

    // 1級/2級/3級/4級魔獣回復薬、魔獣回復薬セット、1〜3級魔獣薬粉、魔獣吸血薬 — best (most valuable) first.
    private static readonly ushort[] RecoveryItemPriority = [140, 79, 78, 77, 76, 82, 81, 80, 135];

    // 回避の書、反射の書、時の砂、魔獣剛力薬 — self-targeted, no HP condition, just "use when available".
    private static readonly ushort[] SelfBuffItemPriority = [137, 136, 138, 104];

    // 吸血鬼の牙、風/土/雷/水/氷/火の牙 — target-facing; tried in a fixed order, not weakness-matched.
    private static readonly ushort[] OffensiveItemPriority = [134, 133, 132, 131, 130, 129, 128];

    /// <summary>Every Crucible item id BeastHelper knows the name of — for building a rule-editor dropdown.</summary>
    public static readonly ushort[] KnownItemIds =
        [..RecoveryItemPriority, ..SelfBuffItemPriority, ..OffensiveItemPriority];

    // 第一盤〜特二盤 (ported from IsArenaTerritory in the source repo).
    private const uint ArenaTerritoryMin = 1339;
    private const uint ArenaTerritoryMax = 1343;

    private readonly CrucibleGearReader gearReader=new();
    public string ConditionalHealingStatus=>this.gearReader.Status;
    private readonly LootBoostPreparationPolicy lootPreparation=new();
    private readonly LootBoostPreparationPolicy merchantPreparation=new();
    public string LootBoostStatus { get; private set; }="Waiting for next battle";
    private readonly Configuration configuration;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly ISigScanner sigScanner;
    private readonly IPluginLog log;

    private delegate* unmanaged<byte*, uint, byte, uint> getUseStatus;
    private delegate* unmanaged<byte*, void> refreshMapping;
    private bool nativeInitializationAttempted;
    private bool nativeAvailable;
    private DateTime nextRecoveryDiagnosticUtc;
    private ulong recoveryPreparationEncounter;
    private DateTime recoveryPreparationDeadline;
    private DateTime nextUseUtc = DateTime.MinValue;
    private DateTime pendingHealUntil;
    private uint pendingHealHp;
    private ulong antipoisonUsedForBorgny;
    private readonly Dictionary<ushort, DateTime> uncheckedSelfItemAttempts = [];

    private void MarkHealPending(IPlayerCharacter player, DateTime now)
    {
        this.pendingHealUntil = now.AddSeconds(CrucibleItemTimingPolicy.PendingHealSeconds);
        this.pendingHealHp = player.CurrentHp;
    }

    private bool HealPending(IPlayerCharacter player, DateTime now)
        => CrucibleItemTimingPolicy.HealStillPending(now, this.pendingHealUntil, this.pendingHealHp, player.CurrentHp,
            player.MaxHp == 0 ? 100f : player.CurrentHp * 100f / player.MaxHp);

    private int OffensiveSlotsHeld() => this.SlotsHeld(OffensiveItemPriority);

    private int SlotsHeld(ushort[] items)
    {
        if (!this.nativeAvailable) return 0;
        var framework = EventFramework.Instance();
        var director = framework == null ? null : framework->GetInstanceContentDirector();
        if (director == null || (int)director->InstanceContentType != 22) return 0;
        var held = 0;
        for (uint slot = 0; slot < SlotCount; slot++)
        {
            var id = *(ushort*)((byte*)director + InventoryOffset + slot * InventoryStride);
            if (id != 0 && items.Contains(id)) held++;
        }
        return held;
    }

    public CrucibleRecoveryItemService(Configuration configuration, IObjectTable objectTable, ITargetManager targetManager, ICondition condition, IClientState clientState, ISigScanner sigScanner, IPluginLog log)
    {
        this.configuration = configuration;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.condition = condition;
        this.clientState = clientState;
        this.sigScanner = sigScanner;
        this.log = log;

    }

    /// <summary>Short human status for the main window.</summary>
    public string Status { get; private set; } = "off";

    // Called only after combat service has identified a live enemy and passed its
    // event/zoning/stop guards, before enabling RSR or approaching the enemy.
    public bool PrepareLootBoost(ulong encounterId)
    {
        var allowed=!this.configuration.AutomationStopped
            && this.configuration.AutoCombatEnabled && IsArenaTerritory(this.clientState.TerritoryType)
            && (!this.configuration.FirstBoardScoreEnabled || FirstBoardScorePolicy.ItemsAllowed(this.configuration)
                && FirstBoardScorePolicy.AllowsCombat(this.configuration,this.clientState.TerritoryType));
        if(!allowed || this.condition[ConditionFlag.InCombat]) return true;
        var player=this.objectTable.LocalPlayer;
        if(player is null || player.IsDead) return true;
        var prepareNow=DateTime.UtcNow;
        if(this.recoveryPreparationEncounter!=encounterId)
        {
            this.recoveryPreparationEncounter=encounterId;
            this.recoveryPreparationDeadline=prepareNow.AddSeconds(8);
        }
        if(prepareNow<this.recoveryPreparationDeadline && this.configuration.PrepareRecoveryBeforePull && this.configuration.AutoRecoveryItemEnabled
            && player.MaxHp>0 && player.CurrentHp*100f/player.MaxHp<this.configuration.AutoRecoveryItemHpThreshold)
        {
            var now=DateTime.UtcNow;
            if(now<this.nextUseUtc || this.HealPending(player,now)) return false; // Wait for our prior request's effect before pulling.
            if(this.TryUseConditionalRecovery(player,player,now,out var item)
                || this.TryUseItem(RecoveryItemPriority,player,null,now,true,out item)
                || this.TryUseEmergencyMitigation(player,player,now,out item))
            {
                this.MarkHealPending(player,now);
                this.LootBoostStatus="Preparing recovery before approach: "+GetItemName(item);
                this.log.Information("[BeastHelper] Pre-pull recovery/defense requested: {Item} at HP {Hp:0.#}%.",GetItemName(item),player.CurrentHp*100f/player.MaxHp);
                return false;
            }
        }
        return this.PrepareEye(encounterId,player,100,4855,this.configuration.AutoLootBoostEnabled,this.lootPreparation)
            && this.PrepareEye(encounterId,player,101,4856,this.configuration.AutoMerchantEyeEnabled,this.merchantPreparation);
    }

    private bool PrepareEye(ulong encounterId,IPlayerCharacter player,ushort itemId,uint statusId,bool enabled,LootBoostPreparationPolicy preparation)
    {
        if(!enabled) return true;
        var name=GetItemName(itemId);
        var now=DateTime.UtcNow;
        var active=player.StatusList.Any(s=>s.StatusId==statusId);
        if(active) { this.LootBoostStatus=name+" buff already active";return true; }
        if(!this.InitializeNative()) { this.LootBoostStatus="Loot boost unavailable: item connection not ready";return true; }
        var framework=EventFramework.Instance();
        var director=framework==null?null:framework->GetInstanceContentDirector();
        var agents=AgentModule.Instance();
        var agent=agents==null?null:(byte*)agents->GetAgentByInternalId((AgentId)497);
        if(director==null || (int)director->InstanceContentType!=22 || agent==null) return true;
        this.refreshMapping(agent);
        var owned=FindDisplaySlot(agent,(byte*)director,itemId,out var inventorySlot)>=0;
        var usable=owned && this.getUseStatus((byte*)director,inventorySlot,0)==0 && now>=this.nextUseUtc;
        var step=preparation.Evaluate(encounterId,now,true,active,owned,usable);
        if(step==LootBoostStep.Ready) { this.LootBoostStatus="Enabled eye buffs checked; ready for combat";return true; }
        if(step==LootBoostStep.TimedOut)
        {
            this.LootBoostStatus=name+" not confirmed after 6 seconds; continuing combat";
            this.log.Warning("[BeastHelper] {Status}",this.LootBoostStatus);
            return true;
        }
        if(step==LootBoostStep.Use && this.TryUseItem([itemId],player,null,now,requireActionRangeCheck:true,out _))
        {
            preparation.Requested(now);
            this.log.Information("[BeastHelper] Pre-combat {Item} use requested; waiting for buff confirmation.",name);
        }
        this.LootBoostStatus="Preparing "+name+"; waiting for buff confirmation";
        return false;
    }

    public void Tick()
    {
        if (this.configuration.FirstBoardScoreEnabled && (!FirstBoardScorePolicy.ItemsAllowed(this.configuration) || !FirstBoardScorePolicy.AllowsCombat(this.configuration, this.clientState.TerritoryType)))
        {
            this.Status = "Blocked by Crucible score mode: no items";
            return;
        }

        if ((!this.configuration.CombatOnlyEnabled && !this.configuration.AutoEnterCrucibleEnabled)
            || (!this.configuration.AutoRecoveryItemEnabled && !this.configuration.AutoSelfBuffItemsEnabled
                && !this.configuration.AutoOffensiveItemsEnabled && !this.configuration.AutoCrucibleItemRulesEnabled
                && !this.configuration.AutoLootBoostEnabled && !this.configuration.AutoMerchantEyeEnabled
                && !this.configuration.AutoFamiliarRecoveryEnabled))
        {
            this.Status = "off";
            return;
        }

        if (!IsArenaTerritory(this.clientState.TerritoryType))
        {
            this.Status = "Outside supported zone";
            return;
        }

        if (!this.condition[ConditionFlag.InCombat])
        {
            this.Status = (this.configuration.AutoLootBoostEnabled || this.configuration.AutoMerchantEyeEnabled) ? this.LootBoostStatus : "Waiting (outside combat)";
            return;
        }

        var player = this.objectTable.LocalPlayer;
        if (player is null || player.IsDead || player.CurrentHp == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var recoveryUnavailable=false;
        var healPending=false;

        if (this.configuration.AutoCrucibleItemRulesEnabled && this.TryUseRules(player, now, out var ruleItemId))
        {
            this.Status = $"{GetItemName(ruleItemId)} used (custom rule)";
            this.log.Information("[BeastHelper] Crucible item: used item {ItemId} via rule.", ruleItemId);
            return;
        }

        if (this.configuration.AutoRecoveryItemEnabled)
        {
            var hpPercent = player.MaxHp == 0 ? 100f : player.CurrentHp * 100f / player.MaxHp;
            var secondMaster = SecondMasterLegendaryProfile.Active(this.configuration);
            var atomicRay = secondMaster && this.objectTable.OfType<IBattleChara>()
                .Any(a => a.BaseId == CrucibleItemTimingPolicy.Durga && a.IsCasting && a.CastActionId == CrucibleItemTimingPolicy.AtomicRay);
            var hpThreshold = CrucibleItemTimingPolicy.RecoveryThreshold(this.configuration.AutoRecoveryItemHpThreshold, secondMaster, this.clientState.TerritoryType, atomicRay, player.MaxHp);
            healPending = hpPercent < hpThreshold && this.HealPending(player, now);
            if (hpPercent < hpThreshold && !healPending)
            {
                if (this.TryUseConditionalRecovery(player,player,now,out var itemId)
                    || this.TryUseItem(RecoveryItemPriority, player, null, now, requireActionRangeCheck: true, out itemId))
                {
                    this.MarkHealPending(player, now);
                    this.Status = $"{GetItemName(itemId)} used (HP {hpPercent:0.#}%)";
                    this.log.Information("[BeastHelper] Crucible item: used recovery item {ItemId} at HP {Hp:0.#}%.", itemId, hpPercent);
                    return;
                }

                recoveryUnavailable=true;
                if(now>=this.nextRecoveryDiagnosticUtc)
                {
                    this.nextRecoveryDiagnosticUtc=now.AddSeconds(10);
                    this.LogRecoveryInventory();
                }
                if(CrucibleItemTimingPolicy.AntidoteAsLastResortHeal(secondMaster,this.clientState.TerritoryType)
                    && this.TryUseItem([CrucibleItemTimingPolicy.CrucibleAntidote],player,null,now,requireActionRangeCheck:true,out var antidote))
                {
                    this.MarkHealPending(player, now);
                    this.Status=$"{GetItemName(antidote)} used as a heal (HP {hpPercent:0.#}%)";
                    this.log.Information("[Second Master] Used {Item} as a last-resort heal at HP {Hp:0.#}% (no other healing item usable).",GetItemName(antidote),hpPercent);
                    return;
                }
                if(this.TryUseEmergencyMitigation(player,player,now,out var mitigationItem))
                {
                    this.MarkHealPending(player, now);
                    this.Status=$"{GetItemName(mitigationItem)} requested: healing unavailable (HP {hpPercent:0.#}%)";
                    this.log.Information("[BeastHelper] Emergency mitigation: {Item} requested on player at HP {Hp:0.#}%; no healing item usable.",GetItemName(mitigationItem),hpPercent);
                    return;
                }
                this.Status = $"No recovery potion available (HP {hpPercent:0.#}%)";
            }
        }

        // First Master: G2 Antipoison serum at Borgny's pull (300s, 100% poison resistance). Heals above take priority.
        if (this.configuration.AutoRecoveryItemEnabled && FirstMasterLegendaryProfile.Active(this.configuration))
        {
            var borgnyId = this.objectTable.OfType<IBattleChara>()
                .FirstOrDefault(a => a.BaseId == CrucibleItemTimingPolicy.Borgny && a.CurrentHp > 0)?.GameObjectId ?? 0;
            if (CrucibleItemTimingPolicy.UseBorgnyAntipoison(true, this.clientState.TerritoryType, borgnyId, this.antipoisonUsedForBorgny)
                && this.TryUseItem([CrucibleItemTimingPolicy.G2AntipoisonSerum], player, null, now, requireActionRangeCheck: true, out var serumItem))
            {
                this.antipoisonUsedForBorgny = borgnyId;
                this.Status = $"{GetItemName(serumItem)} used for Borgny";
                this.log.Information("[First Master Borgny] Used {Item} at the pull (poison resistance).", GetItemName(serumItem));
                return;
            }
        }

        var familiar=this.objectTable.OfType<IBattleChara>().FirstOrDefault(a=>a.OwnerId==player.GameObjectId
            && FamiliarRecoveryPolicy.SupportedPet(a.BaseId,SecondMasterLegendaryProfile.MasterTrioActive(this.configuration),FirstBoardScorePolicy.BeastkinClear(this.configuration)) && !a.IsDead && a.MaxHp>0 && a.CurrentHp>0);
        var familiarHp=familiar is null ? 100f : familiar.CurrentHp*100f/familiar.MaxHp;
        var playerNeedsRecovery=healPending || player.MaxHp>0 && player.CurrentHp*100f/player.MaxHp<this.configuration.AutoRecoveryItemHpThreshold;
        if(FamiliarRecoveryPolicy.ShouldHeal(this.configuration.AutoFamiliarRecoveryEnabled,playerNeedsRecovery,
            SecondMasterLegendaryProfile.MasterTrioActive(this.configuration) || FirstBoardScorePolicy.BeastkinClear(this.configuration),familiarHp,this.configuration.FamiliarRecoveryHpThreshold)
            && CrucibleItemTimingPolicy.FamiliarHealAllowed(SecondMasterLegendaryProfile.MasterTrioActive(this.configuration),this.SlotsHeld(RecoveryItemPriority))
            && CrucibleItemTimingPolicy.FamiliarHealAllowedAtSecondMasterBoss(SecondMasterLegendaryProfile.Active(this.configuration),this.clientState.TerritoryType,
                this.objectTable.OfType<IBattleChara>().Any(a=>a.BaseId==CrucibleItemTimingPolicy.Lauda && a.CurrentHp>0)))
        {
            if(this.TryUseConditionalRecovery(player,familiar!,now,out var familiarItem)
                || this.TryUseItem([79,78,77,76],player,familiar,now,requireActionRangeCheck:true,out familiarItem))
            {
                this.Status=$"{GetItemName(familiarItem)} requested for {familiar!.Name} (HP {familiarHp:0.#}%)";
                this.log.Information("[BeastHelper] Crucible familiar recovery: item {Item} requested on {Familiar} at HP {Hp:0.#}%.",familiarItem,familiar.Name,familiarHp);
                return;
            }
            if(this.TryUseEmergencyMitigation(player,familiar!,now,out var mitigationItem))
            {
                this.Status=$"{GetItemName(mitigationItem)} requested for {familiar!.Name}: healing unavailable";
                this.log.Information("[BeastHelper] Emergency mitigation: {Item} requested on {Familiar} at HP {Hp:0.#}%; no healing item usable.",GetItemName(mitigationItem),familiar.Name,familiarHp);
                return;
            }
        }

        if (this.configuration.AutoSelfBuffItemsEnabled
            && this.TryUseItem(SelfBuffItemPriority, player, null, now, requireActionRangeCheck: false, out var buffItemId))
        {
            this.Status = $"{GetItemName(buffItemId)} used";
            this.log.Information("[BeastHelper] Crucible item: used self-buff item {ItemId}.", buffItemId);
            return;
        }

        // Fangs have area effects; do not trigger Zu's eggs while trying to damage the boss.
        var eggsPresent = this.clientState.TerritoryType == 1341 && this.objectTable.OfType<IBattleChara>()
            .Any(a => a.BaseId is 0x4C99 or 0x4C9A && a.IsTargetable && a.CurrentHp > 0);
        if (this.configuration.AutoOffensiveItemsEnabled && !eggsPresent)
        {
            var target = this.targetManager.Target;
            var firstMaster = FirstMasterLegendaryProfile.Active(this.configuration);
            var borgnyPresent = firstMaster && this.objectTable.OfType<IBattleChara>()
                .Any(a => a.BaseId == CrucibleItemTimingPolicy.Borgny && a.CurrentHp > 0);
            if (target is IBattleChara { IsDead: false, IsTargetable: true } enemy && enemy.ObjectKind == DalamudObjectKind.BattleNpc
                && !(this.clientState.TerritoryType == 1341 && ((enemy.BaseId == 0x4C95 && enemy.StatusList.Any(s => s.StatusId == 5434)) || (enemy.BaseId == 0x4C93 && enemy.StatusList.Any(s => s.StatusId == 2198))))
                && CrucibleItemTimingPolicy.AllowOffensive(firstMaster, this.clientState.TerritoryType, borgnyPresent, enemy.BaseId,
                    firstMaster ? this.OffensiveSlotsHeld() : 0))
            {
                if (this.TryUseItem(OffensiveItemPriority, player, enemy, now, requireActionRangeCheck: false, out var offensiveItemId))
                {
                    this.Status = $"{GetItemName(offensiveItemId)} used on {enemy.Name}";
                    this.log.Information("[BeastHelper] Crucible item: used offensive item {ItemId} on {Target}.", offensiveItemId, enemy.Name);
                    return;
                }
            }
        }

        if(recoveryUnavailable && now>=this.nextRecoveryDiagnosticUtc)
        {
            this.nextRecoveryDiagnosticUtc=now.AddSeconds(10);
            this.LogRecoveryInventory();
        }
        this.Status = healPending ? "Waiting for the previous heal to land"
            : recoveryUnavailable ? "Recovery needed; no healing item usable now (check duty inventory / cooldown)" : "Waiting";
    }

    private void LogRecoveryInventory()
    {
        if(!this.nativeAvailable) return;
        var framework=EventFramework.Instance();var director=framework==null?null:framework->GetInstanceContentDirector();
        if(director==null || (int)director->InstanceContentType!=22) return;
        var items=new List<string>();
        for(uint slot=0;slot<SlotCount;slot++)
        {
            var id=*(ushort*)((byte*)director+InventoryOffset+slot*InventoryStride);
            if(id==0) continue;
            items.Add($"{GetItemName(id)} (id {id}, native readiness {this.getUseStatus((byte*)director,slot,0)})");
        }
        this.log.Information("[BeastHelper] Recovery inventory diagnostic: {Items}. Conditional healing enabled={Enabled}; {Ribbon}. Readiness 0 means native item check passed; target/range/animation/shared cooldown may still block use.",
            items.Count==0 ? "no consumable items held" : string.Join("; ",items),this.configuration.AutoConditionalRecoveryEnabled,this.gearReader.Status);
    }

    private bool TryUseEmergencyMitigation(IPlayerCharacter player,IBattleChara target,DateTime now,out ushort itemId)
    {
        var candidates=EmergencyMitigationPolicy.Candidates(this.configuration.AutoEmergencyMitigationEnabled,true,true,
            target.StatusList.Select(s=>s.StatusId).ToHashSet(),
            target.StatusList.Select(s=>s.GameData.Value.Name.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase),
            target.GameObjectId!=player.GameObjectId);
        if(this.TryUseItem(candidates,player,target.GameObjectId==player.GameObjectId?null:target,now,true,out itemId)) return true;
        if(target.GameObjectId!=player.GameObjectId) return false;
        // Self-only items: BeastHelper's range/target check may reject an item the game would accept (Tome of the
        // Impervious was never used). Retry each candidate without it, at most once per 20s.
        foreach(var candidate in candidates)
        {
            this.uncheckedSelfItemAttempts.TryGetValue(candidate,out var last);
            if(!CrucibleItemTimingPolicy.AllowUncheckedSelfItem(now,last)) continue;
            if(this.TryUseItem([candidate],player,null,now,false,out itemId))
            {
                this.uncheckedSelfItemAttempts[candidate]=now;
                this.log.Information("[BeastHelper] Emergency mitigation: {Item} requested without BeastHelper's range check (unverified; check the effect).",GetItemName(itemId));
                return true;
            }
        }
        return false;
    }

    private bool TryUseConditionalRecovery(IPlayerCharacter player,IBattleChara target,DateTime now,out ushort itemId)
    {
        itemId=0;
        if(!this.configuration.AutoConditionalRecoveryEnabled || target.IsDead || target.CurrentHp==0) return false;
        this.gearReader.Update(now);
        bool HasCurable(string name)=>target.StatusList.Any(s=>s.GameData.Value.Name.ToString().Equals(name,StringComparison.OrdinalIgnoreCase));
        var familiar=target.GameObjectId!=player.GameObjectId;
        var candidates=ConditionalRecoveryPolicy.Candidates(this.gearReader.CrimsonRibbonOwned,
            HasCurable("Poison"),HasCurable("Blind"),HasCurable("Petrification"),familiar);
        var firstMaster=FirstMasterLegendaryProfile.Active(this.configuration);
        var borgnyPresent=firstMaster && this.objectTable.OfType<IBattleChara>().Any(a=>a.BaseId==CrucibleItemTimingPolicy.Borgny && a.CurrentHp>0);
        foreach(var candidate in candidates)
        {
            if(CrucibleItemTimingPolicy.ReserveForBorgny(candidate,firstMaster,this.clientState.TerritoryType,borgnyPresent)) continue;
            if(ConditionalRecoveryPolicy.AreaSerum(candidate))
            {
                if(familiar && System.Numerics.Vector3.Distance(player.Position,target.Position)>14f) continue;
                if(this.TryUseItem([candidate],player,null,now,true,out itemId)) return true;
            }
            else if(this.TryUseItem([candidate],player,familiar?target:null,now,true,out itemId)) return true;
        }
        return false;
    }

    /// <summary>
    /// Tries each item in <paramref name="priority"/>, in order, against
    /// <paramref name="target"/> (or the player themself when null), using
    /// the shared cooldown and the same hotbar-slot-mapping mechanism for
    /// all item kinds. <paramref name="requireActionRangeCheck"/> mirrors
    /// upstream's own behavior: only the recovery-item path additionally
    /// checks <see cref="CanUseOnTarget"/> before committing.
    /// </summary>
    private bool TryUseItem(ushort[] priority, IPlayerCharacter player, IBattleChara? target, DateTime now, bool requireActionRangeCheck, out ushort itemId)
    {
        itemId = 0;
        if (now < this.nextUseUtc || !this.InitializeNative())
        {
            return false;
        }

        var actionManager = ActionManager.Instance();
        var hotbar = RaptureHotbarModule.Instance();
        var targets = TargetSystem.Instance();
        var agentModule = AgentModule.Instance();
        var agent = agentModule == null ? null : (byte*)agentModule->GetAgentByInternalId((AgentId)497);
        var eventFramework = EventFramework.Instance();
        var director = eventFramework == null ? null : eventFramework->GetInstanceContentDirector();
        if (actionManager == null || actionManager->AnimationLock > 0f
            || hotbar == null || targets == null || agent == null || director == null
            || (int)director->InstanceContentType != 22)
        {
            return false;
        }

        var self = (GameObject*)player.Address;
        var effectiveTarget = target is null ? self : (GameObject*)target.Address;

        this.refreshMapping(agent);
        foreach (var candidateItemId in priority)
        {
            var displaySlot = FindDisplaySlot(agent, (byte*)director, candidateItemId, out var inventorySlot);
            if (displaySlot < 0 || this.getUseStatus((byte*)director, inventorySlot, 0) != 0)
            {
                continue;
            }

            if (requireActionRangeCheck && !CanUseOnTarget(candidateItemId, self,effectiveTarget))
            {
                continue;
            }

            var slot = new HotbarSlot
            {
                CommandType = (HotbarSlotType)36,
                CommandId = (uint)displaySlot,
            };
            var previousSoftTarget = targets->SoftTarget;
            try
            {
                targets->SoftTarget = effectiveTarget;
                hotbar->ExecuteSlot(&slot);
                itemId = candidateItemId;
                this.nextUseUtc = now.Add(UseInterval);
                return true;
            }
            finally
            {
                targets->SoftTarget = previousSoftTarget;
            }
        }

        return false;
    }

    /// <summary>
    /// Tries each enabled rule in list order; the first whose conditions are
    /// satisfied against the current player/target and whose item is
    /// actually usable wins. Rules for an item in <see cref="OffensiveItemPriority"/>
    /// require a live, targetable hostile target; everything else targets
    /// the player. Shares the same cooldown as every other item use.
    /// </summary>
    private bool TryUseRules(IPlayerCharacter player, DateTime now, out ushort itemId)
    {
        itemId = 0;
        if (this.configuration.CrucibleItemRules.Count == 0)
        {
            return false;
        }

        var target = this.targetManager.Target as IBattleChara;
        foreach (var rule in this.configuration.CrucibleItemRules)
        {
            if (!rule.Enabled || rule.ItemId == 0 || rule.Conditions.Count == 0 || !EvaluateRule(rule, player, target))
            {
                continue;
            }

            var offensive = Array.IndexOf(OffensiveItemPriority, rule.ItemId) >= 0;
            if (offensive && (target is null || target.IsDead))
            {
                continue;
            }

            if (this.TryUseItem([rule.ItemId], player, offensive ? target : null, now, requireActionRangeCheck: false, out itemId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateRule(CrucibleItemRule rule, IPlayerCharacter player, IBattleChara? target)
    {
        var all = rule.ConditionJoinMode == CrucibleItemRuleConditionJoinMode.All;
        foreach (var condition in rule.Conditions)
        {
            var result = EvaluateCondition(condition, player, target);
            if (all && !result)
            {
                return false;
            }

            if (!all && result)
            {
                return true;
            }
        }

        return all;
    }

    private static bool EvaluateCondition(CrucibleItemRuleCondition condition, IPlayerCharacter player, IBattleChara? target)
        => condition.Type switch
        {
            CrucibleItemRuleConditionType.SelfStatus => EvaluateStatus(player, condition),
            CrucibleItemRuleConditionType.TargetStatus => target is not null && EvaluateStatus(target, condition),
            CrucibleItemRuleConditionType.SelfHp => EvaluateHp(player.CurrentHp, player.MaxHp, condition),
            CrucibleItemRuleConditionType.TargetHp => target is not null && EvaluateHp(target.CurrentHp, target.MaxHp, condition),
            CrucibleItemRuleConditionType.TargetDataId => target is not null && target.BaseId == condition.DataId,
            _ => false,
        };

    private static bool EvaluateStatus(IBattleChara character, CrucibleItemRuleCondition condition)
    {
        var present = false;
        foreach (var status in character.StatusList)
        {
            if (status.StatusId == condition.StatusId)
            {
                present = true;
                break;
            }
        }

        return condition.StatusCondition == CrucibleItemRuleStatusCondition.Present ? present : !present;
    }

    private static bool EvaluateHp(uint currentHp, uint maxHp, CrucibleItemRuleCondition condition)
    {
        var percent = maxHp == 0 ? 100f : currentHp * 100f / maxHp;
        return condition.HpCondition == CrucibleItemRuleHpCondition.Above ? percent > condition.HpThreshold : percent < condition.HpThreshold;
    }

    private static int FindDisplaySlot(byte* agent, byte* director, ushort wantedItemId, out uint inventorySlot)
    {
        inventorySlot = SlotCount;
        for (var displaySlot = 0; displaySlot < SlotCount; displaySlot++)
        {
            var entry = agent + MappingOffset + displaySlot * MappingStride;
            var candidateInventorySlot = *(uint*)entry;
            var itemId = ((ushort*)entry)[2];
            if (candidateInventorySlot < SlotCount
                && itemId == wantedItemId
                && *(ushort*)(director + InventoryOffset + candidateInventorySlot * InventoryStride) == itemId)
            {
                inventorySlot = candidateInventorySlot;
                return displaySlot;
            }
        }

        return -1;
    }

    private bool InitializeNative()
    {
        if (this.nativeAvailable)
        {
            return true;
        }

        if (this.nativeInitializationAttempted)
        {
            return false;
        }

        this.nativeInitializationAttempted = true;
        if (!this.sigScanner.TryScanText(UseStatusSignature, out var statusAddress)
            || !this.sigScanner.TryScanText(RefreshMappingSignature, out var refreshAddress))
        {
            this.log.Warning("[BeastHelper] Crucible item: native signature scan failed; disabling auto-use.");
            return false;
        }

        this.getUseStatus = (delegate* unmanaged<byte*, uint, byte, uint>)statusAddress;
        this.refreshMapping = (delegate* unmanaged<byte*, void>)refreshAddress;
        this.nativeAvailable = true;
        return true;
    }

    private static bool CanUseOnTarget(ushort itemId, GameObject* self,GameObject* target)
    {
        var actionId = FirstRecoveryActionId + itemId - FirstRecoveryItemId;
        return ActionManager.CanUseActionOnTarget(actionId, target)
            && ActionManager.GetActionInRangeOrLoS(actionId, self, target) == 0;
    }

    private static bool IsArenaTerritory(uint territoryId)
        => territoryId is >= ArenaTerritoryMin and <= ArenaTerritoryMax;

    public static string GetItemName(ushort itemId) => itemId switch
    {
        83 => "Crucible Antidote",
        84 => "Crucible Needle",
        85 => "Crucible Eye Drops",
        86 => "G1 Antipoison Soul Serum",
        87 => "G2 Antipoison Soul Serum",
        88 => "G1 Antiparalysis Soul Serum",
        89 => "G2 Antiparalysis Soul Serum",
        90 => "G1 Antiblind Soul Serum",
        91 => "G2 Antiblind Soul Serum",
        92 => "G1 Antipetrification Soul Serum",
        93 => "G2 Antipetrification Soul Serum",
        94 => "G1 Antisleep Soul Serum",
        95 => "G2 Antisleep Soul Serum",
        98 => "G1 Beastmaster Reraiser",
        99 => "G2 Beastmaster Reraiser",
        100 => "Thief's Eye",
        101 => "Merchant's Eye",
        76 => "Beast recovery potion (grade 1)",
        77 => "Beast recovery potion (grade 2)",
        78 => "Beast recovery potion (grade 3)",
        79 => "Beast recovery potion (grade 4)",
        80 => "Beast recovery powder (grade 1)",
        81 => "Beast recovery powder (grade 2)",
        82 => "Beast recovery powder (grade 3)",
        102 => "Crucible Tannin",
        110 => "Potion of Tempered Evasion",
        112 => "Potion of Tempered Constitution",
        114 => "Potion of Breathtaking Swiftness",
        104 => "Beast strength potion",
        128 => "Fire fang",
        129 => "Ice fang",
        130 => "Water fang",
        131 => "Lightning fang",
        132 => "Earth fang",
        133 => "Wind fang",
        134 => "Vampire fang",
        135 => "Beast lifesteal potion",
        136 => "Reflection tome",
        137 => "Tome of the Impervious",
        138 => "Temporal sand",
        140 => "Beast potion kit",
        _ => $"Item {itemId}",
    };
}
