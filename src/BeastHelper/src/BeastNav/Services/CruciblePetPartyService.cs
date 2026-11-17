using BeastNav.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace BeastNav.Services;

/// <summary>
/// Auto-forms the whistle party on 呼び笛の設定 (XBMPetParty): removes any
/// current member not in the desired team, then adds any desired member not
/// yet in the team via the notebook. The desired team is either "Leveling"
/// (lowest-ranked owned beasts first, so the whole roster climbs toward the
/// rank cap over repeated runs) or "Manual" (exactly the beasts checked off
/// in 魔獣リスト) — see <see cref="Configuration.CrucibleTeamMode"/>.
/// </summary>
/// <remarks>
/// The add-a-beast technique was the one thing three earlier attempts this
/// session couldn't crack (see LuaAutomationService's own remarks for the
/// full history: FireCallback replay alone carries no beast identity,
/// ButtonClick/ListItemClick with a real AtkEventData.ListItemData had no
/// effect, and a null AtkEventData crashed the game outright). Reading
/// erdelf/AutoDuty's own published Crucible automation (2026-09) found the
/// actual working sequence — confirmed live in this session before this
/// file was written: FireCallback(notebook, true, 5, slotOnPage) to select
/// the row, then a plain MouseDown ReceiveEvent with a non-null pointer to
/// a value-initialized (all-zero) AtkEventData, the event found by a full
/// recursive search of the notebook's node tree since it isn't on any
/// specific node's own chain. Removal uses XBMPetParty's own row-context-menu
/// FireCallback (open menu, pick the first option) — the same FireCallback-
/// only style already used safely elsewhere in this codebase.
/// </remarks>
/// <remarks>
/// Unattended UI interaction — against the FFXIV ToS, self-responsibility.
/// Off by default. Only acts while XBMPetParty is open, and only once both
/// AutoEnterCrucibleEnabled and AutoCruciblePetPartyEnabled are on.
/// </remarks>
public sealed unsafe class CruciblePetPartyService
{
    private const string PartyAddonName = "XBMPetParty";
    private const string NotebookAddonName = "XBMMonsterNotebook";
    private const string ContextMenuAddonName = "ContextMenu";

    // Party list's own AtkValues layout — the same indices the read-only
    // investigation earlier this session validated against a live 12-beast
    // board-2 roster, read here from AtkValues rather than raw agent memory.
    private const int MemberCountIndex = 5;
    private const int FirstMemberIndex = 6;
    private const int MemberStride = 77;
    private const int CapacityIndex = 1162;
    private const uint MemberIconBase = 242000;
    private const int DefaultCapacity = 10;

    // Notebook indices confirmed live this session — see BeastTamingStateService's own remarks.
    private const int NotebookPageValueIndex = 10;
    private const int NotebookNumberIndex = 229;
    private const int NotebookEntriesPerPage = 25;
    private const uint NotebookFirstEntryParam = 4;

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ActionResend = TimeSpan.FromSeconds(1);

    private enum Step
    {
        Idle,
        AwaitRemoveMenu,
        AwaitRemoved,
        AwaitBestiary,
        Adding,
    }

    private readonly Configuration configuration;
    private readonly BeastDataService beastData;
    private readonly IGameGui gameGui;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly Action saveConfiguration;

    private Step step = Step.Idle;
    private DateTime stepSince = DateTime.MinValue;
    private DateTime nextActionAt = DateTime.MinValue;
    private uint removingNumber;
    private uint addingNumber;
    private bool addSent;
    private bool notebookFilterResetAttempted;
    private List<uint> scoreValidatedIds = [];
    public bool ScoreRosterReady
    {
        get
        {
            if (this.configuration.ScoreVilekinCombo)
            {
                var actionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
                if (actionManager != null && this.configuration.ScoreTeamPetRowIds.Count == 3)
                {
                    uint[] horns = [actionManager->BeastmasterPets[0], actionManager->BeastmasterPets[1], actionManager->BeastmasterPets[2]];
                    if (WhistleSelectionPolicy.SameTrio(horns, [44u, 16u, 10u])) return true;
                }
            }
            return this.configuration.ScoreTeamPetRowIds.Count == 3
                && this.scoreValidatedIds.SequenceEqual(this.configuration.ScoreTeamPetRowIds) && this.scoreWhistlesVerified;
        }
    }


    public bool XpCombatReady
    {
        get
        {
            if(!FirstBoardScorePolicy.XpRun(this.configuration)) return false;
            var manager=FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
            return manager!=null && WhistleSelectionPolicy.SameTrio(
                [manager->BeastmasterPets[0],manager->BeastmasterPets[1],manager->BeastmasterPets[2]],[1u,26u,33u]);
        }
    }

    private bool scoreWhistlesVerified;
    private uint[]? expectedWhistles;
    private DateTime whistleAckDeadline;
    private bool whistleAssignmentFailed;
    private string lastWhistleObservation = string.Empty;
    private DateTime nextWhistleActionAt = DateTime.MinValue;
    private bool startBattleSent;

    public CruciblePetPartyService(Configuration configuration, BeastDataService beastData, IGameGui gameGui, IClientState clientState, IPluginLog log, Action saveConfiguration)
    {
        this.configuration = configuration;
        this.beastData = beastData;
        this.gameGui = gameGui;
        this.clientState = clientState;
        this.log = log;
        this.saveConfiguration = saveConfiguration;
    }

    /// <summary>Short human status for the main window.</summary>
    public string Status { get; private set; } = "off";

    /// <summary>Short human status for the main window (whistle assignment).</summary>
    public string WhistleStatus { get; private set; } = "off";

    /// <summary>
    /// Reconciles the live AgentXBMPetParty selected list with the desired slot order.
    /// The callback is a toggle, so correct selections are never clicked. Each
    /// correction must be observed before another is sent. Combat-only never starts battle.
    /// </summary>
    public void TickWhistleAssignment()
    {
        if (this.configuration.CombatOnlyEnabled
            ? !this.configuration.CombatOnlyBeastSelectionEnabled
            : !this.configuration.AutoEnterCrucibleEnabled || !this.configuration.AutoCrucibleWhistleAssignEnabled)
        {
            this.WhistleStatus = "off";
            this.expectedWhistles = null;
            this.scoreWhistlesVerified = false;
            this.startBattleSent = false;
            return;
        }
        var layout = this.FindAddon("XBMStageDetailList");
        var party = this.FindAddon(PartyAddonName);
        if (layout is null || !layout->IsVisible || party is null || !party->IsVisible)
        {
            this.WhistleStatus = "Waiting for battle start screen";
            this.expectedWhistles = null;
            this.whistleAssignmentFailed = false;
            this.startBattleSent = false;
            return;
        }
        var (team, capacity) = ReadTeam(party);
        if (team is null || team.Count == 0)
        { this.WhistleStatus = "Waiting for roster data"; this.scoreWhistlesVerified = false; return; }
        if (this.configuration.CombatOnlyEnabled || this.configuration.CrucibleTeamMode == CrucibleTeamMode.ClassMinMax)
        {
            var goal = this.ComputeGoal(team, this.ResolveCapacity(capacity));
            if (this.step != Step.Idle || goal.Count == 0 || team.Count != goal.Count || goal.Any(no => !team.Contains(no)))
            { this.WhistleStatus = "Waiting for roster selection to finish"; this.scoreWhistlesVerified = false; return; }
        }
        var picks = this.ComputeWhistlePicks(team);
        if (picks.Count == 0) { this.WhistleStatus = "No beasts to assign to whistles"; this.scoreWhistlesVerified = false; return; }
        var selected = ReadSelectedWhistles(team);
        if (selected == null || !WhistleSelectionPolicy.Valid(selected) || selected.Any(no => !team.Contains(no)))
        {
            this.WhistleStatus = "Selection read unavailable - no toggle sent. Verify whistles manually; do not start yet.";
            this.scoreWhistlesVerified = false;
            return;
        }
        if (WhistleSelectionPolicy.MayRetainPartial(FirstBoardScorePolicy.ItemsAllowed(this.configuration),selected,picks))
        {
            this.expectedWhistles=null;
            this.whistleAssignmentFailed=false;
            this.scoreWhistlesVerified=true;
            this.WhistleStatus=$"Retaining selected battlehorns ({selected.Length}/3). Missing/unavailable beasts are not toggled. Restore them manually when possible.";
            return;
        }
        var observation = string.Join(",", selected);
        if (observation != this.lastWhistleObservation)
        {
            this.lastWhistleObservation = observation;
            this.log.Information("[BeastHelper] Whistle selection observed: [{Ids}] expected [{Expected}]", observation, string.Join(",", picks));
        }
        var now = DateTime.UtcNow;
        if (this.expectedWhistles != null)
        {
            if (selected.SequenceEqual(this.expectedWhistles)) this.expectedWhistles = null;
            else
            {
                this.scoreWhistlesVerified = false;
                if (now >= this.whistleAckDeadline)
                {
                    this.whistleAssignmentFailed = true;
                    this.expectedWhistles = null;
                    this.log.Warning("[BeastHelper] Whistle toggle was not acknowledged; paused instead of retrying a toggle.");
                }
                else { this.WhistleStatus = "Waiting for whistle selection confirmation - do not start yet"; return; }
            }
        }
        if (WhistleSelectionPolicy.Ready(selected, picks) || (this.configuration.FirstBoardScoreEnabled && WhistleSelectionPolicy.SameTrio(selected, picks)))
        {
            this.scoreWhistlesVerified = true;
            this.WhistleStatus = $"Whistles VERIFIED ({selected.Length}/{picks.Count}): {string.Join(" -> ", selected.Select(this.NameOf))}";
            if (!this.configuration.CombatOnlyEnabled && !this.startBattleSent)
            { this.startBattleSent = true; FireCallback(layout, 8); }
            return; // Correct existing game assignments must never be toggled off.
        }
        this.scoreWhistlesVerified = false;
        if (this.whistleAssignmentFailed)
        { this.WhistleStatus = "Whistle confirmation failed. Reopen setup to retry; do not start yet."; return; }
        if (now < this.nextWhistleActionAt) return;
        var target = WhistleSelectionPolicy.NextToggle(selected, picks);
        var row = team.IndexOf(target);
        if (target == 0 || row < 0) { this.WhistleStatus = "Whistle selection mismatch - paused"; return; }
        this.expectedWhistles = WhistleSelectionPolicy.AfterToggle(selected, target);
        this.whistleAckDeadline = now.AddSeconds(4);
        this.nextWhistleActionAt = now.AddMilliseconds(500);
        var agent = AgentXBMPetParty.Instance();
        if (agent == null || agent->Mode is not (1 or 2))
        { this.expectedWhistles = null; this.WhistleStatus = "Not a battlehorn preview - no selection sent"; return; }
        row = -1;
        for (var i = 0; i < agent->SelectedPets.LongCount; i++)
            if (agent->SelectedPets[i].PetId == target) { row = i; break; }
        if (row < 0) { this.expectedWhistles = null; return; }
        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = AtkValueType.Int, Int = 1 };
        values[1] = new AtkValue { Type = AtkValueType.Int, Int = row };
        AtkValue result = default;
        agent->ReceiveEvent(&result, values, 2, 0);
        this.WhistleStatus = $"Correcting whistle order: {this.NameOf(target)} - do not start yet";
        this.log.Information("[BeastHelper] Whistle toggle requested: {Id}, observed [{Before}], awaiting [{After}]", target, observation, string.Join(",", this.expectedWhistles));
    }

    private static uint[]? ReadSelectedWhistles(List<uint> team)
    {
        var agent = AgentXBMPetParty.Instance();
        if (agent == null || agent->Mode is not (1 or 2) || agent->SelectedPetIds.LongCount is < 0 or > 3) return null;
        if (agent->SelectedPets.LongCount != team.Count) return null;
        var roster = new uint[team.Count];
        for (var i = 0; i < roster.Length; i++) roster[i] = agent->SelectedPets[i].PetId;
        if (!roster.Order().SequenceEqual(team.Order())) return null;
        var indices = new uint[(int)agent->SelectedPetIds.LongCount];
        for (var i = 0; i < indices.Length; i++) indices[i] = agent->SelectedPetIds[i].PetId;
        return WhistleSelectionPolicy.ResolveIndices(roster, indices);
    }

    private bool ClearNotebookFilter(AtkUnitBase* notebook)
    {
        // Filters can contain nonzero defaults even with every beast visible.
        // Never use those values to trigger repeated rebuilds, and never touch a completed roster.
        if (!RosterEditPolicy.MayResetFilters(true, this.notebookFilterResetAttempted)) return false;
        var agent = AgentXBMMonsterNotebook.Instance();
        if (notebook == null || !notebook->IsVisible || agent == null) return false;
        this.notebookFilterResetAttempted = true;
        if (agent->SearchText.IsEmpty) return false;
        agent->SearchText.SetString(string.Empty);
        agent->RequestPetListRebuild = true;
        agent->RequestNotebookRebuild = true;
        this.stepSince = DateTime.UtcNow;
        this.nextActionAt = DateTime.UtcNow.AddMilliseconds(500);
        this.Status = "Cleared bestiary search once for missing beast";
        this.log.Information("[BeastHelper] Cleared bestiary search once for missing beast; no repeated filter rebuilds.");
        return true;
    }

    private const int WhistleSize = 3;

    private List<uint> ComputeWhistlePicks(List<uint> team)
    {
        if(SecondMasterLegendaryProfile.MasterTrioActive(this.configuration))
            return FirstMasterLegendaryProfile.Whistles.All(team.Contains) ? FirstMasterLegendaryProfile.Whistles.ToList() : [];

        if (FirstBoardScorePolicy.BeastkinClear(this.configuration))
            return new uint[] { 1,26,33 }.All(team.Contains) ? [1,26,33] : [];
        if (this.configuration.FirstBoardScoreEnabled && this.configuration.ScoreVilekinCombo)
            return new uint[] { 44, 16, 10 }.All(team.Contains) ? [44, 16, 10] : [];

        if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.Leveling)
        {
            return team
                .OrderBy(no => this.beastData.FindPetByXbmRowId(no) is { } pet && this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId, out var progress) ? progress.Level : 0)
                .Take(WhistleSize)
                .ToList();
        }

        if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.Recommended)
        {
            // Battle whistles want the sturdiest of the team up front — the same
            // stability score, highest first.
            return team
                .OrderByDescending(no => this.beastData.FindPetByXbmRowId(no) is { } pet
                    ? CrucibleTeamPlanner.Score(pet, this.configuration.BeastLevelProgress)
                    : double.MinValue)
                .Take(WhistleSize)
                .ToList();
        }

        return team.Take(WhistleSize).ToList();
    }

    public void Tick()
    {
        if (this.configuration.CombatOnlyEnabled
            ? !this.configuration.CombatOnlyBeastSelectionEnabled
            : !this.configuration.AutoEnterCrucibleEnabled || !this.configuration.AutoCruciblePetPartyEnabled)
        {
            this.Status = "off";
            this.step = Step.Idle;
            return;
        }

        var party = this.FindAddon(PartyAddonName);
        if (party is null || !party->IsVisible)
        {
            this.Status = "Waiting for whistle setup";
            this.notebookFilterResetAttempted = false;
            this.step = Step.Idle;
            return;
        }

        var (team, readCapacity) = ReadTeam(party);
        if (team is null)
        {
            this.Status = "Waiting for roster data";
            return;
        }

        var capacity = this.ResolveCapacity(readCapacity);
        var goal = this.ComputeGoal(team, capacity);
        if (this.configuration.FirstBoardScoreEnabled)
            this.scoreValidatedIds = goal.Count == 3 && team.Count == 3 && goal.All(team.Contains)
                ? this.configuration.ScoreTeamPetRowIds.ToList() : [];
        else this.scoreValidatedIds.Clear();
        if (goal.Count == 0)
        {
            if(SecondMasterLegendaryProfile.MasterTrioActive(this.configuration))
            {
                string reason;
                if(FirstMasterLegendaryProfile.XpRun(this.configuration))
                    CrucibleTeamPlanner.FirstMasterXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out reason);
                else if(SecondMasterLegendaryProfile.XpRun(this.configuration))
                    CrucibleTeamPlanner.SecondMasterXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out reason);
                else FirstMasterLegendaryProfile.Team(this.OwnedPets(),out reason);
                this.Status=reason;return;
            }
            if(FirstBoardScorePolicy.XpRun(this.configuration))
            {
                CrucibleTeamPlanner.ThirdBoardXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out var reason);
                this.Status=reason;return;
            }
            if (this.configuration.FirstBoardScoreEnabled)
            {
                string scoreReason;
                if(FirstBoardScorePolicy.BeastkinClear(this.configuration)) FirstBoardScorePolicy.BeastkinTrio(this.OwnedPets(),out scoreReason);
                else FirstBoardScorePolicy.Select(this.OwnedPets(), this.configuration.BeastLevelProgress,
                    this.configuration.ScoreFamily, this.configuration.ScoreTeamPetRowIds, out scoreReason);
                this.Status = scoreReason;
                return;
            }
            if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.ClassMinMax)
            {
                CrucibleTeamPlanner.ClassMinMax(this.OwnedPets(), this.configuration.BeastLevelProgress, out var reason);
                this.Status = reason;
                return;
            }

            this.Status = this.configuration.CrucibleTeamMode == CrucibleTeamMode.Manual
                ? "魔獣リストで編成する魔獣を選択してください"
                : "所持している魔獣がいません";
            this.step = Step.Idle;
            return;
        }

        if (RosterEditPolicy.IsComplete(team, goal))
        {
            this.step = Step.Idle;
            this.Status = $"Roster ready({team.Count}/{goal.Count}) - manual entry";
            return;
        }
        this.Advance(party, team, goal, DateTime.UtcNow);
    }

    private void Advance(AtkUnitBase* party, List<uint> team, List<uint> goal, DateTime now)
    {
        var waitedTooLong = now - this.stepSince > StepTimeout;

        switch (this.step)
        {
            case Step.AwaitRemoveMenu:
            {
                var menu = this.FindAddon(ContextMenuAddonName);
                if (menu is not null && menu->IsVisible)
                {
                    FireCallback(menu, 0, 0, 0);
                    this.SetStep(Step.AwaitRemoved, now);
                }
                else if (waitedTooLong)
                {
                    this.SetStep(Step.Idle, now);
                }

                return;
            }

            case Step.AwaitRemoved:
                if (!team.Contains(this.removingNumber) || waitedTooLong)
                {
                    this.SetStep(Step.Idle, now);
                }

                return;

            case Step.AwaitBestiary:
            {
                var openingNotebook = this.FindAddon(NotebookAddonName);
                if (openingNotebook is not null && openingNotebook->IsVisible)
                {
                    this.addSent = false;
                    this.SetStep(Step.Adding, now);
                }
                else if (waitedTooLong)
                {
                    this.SetStep(Step.Idle, now);
                }
                else if (now >= this.nextActionAt)
                {
                    FireCallback(party, 5);
                    this.nextActionAt = now + ActionResend;
                }

                return;
            }

            case Step.Adding:
                if (team.Contains(this.addingNumber))
                {
                    this.SetStep(Step.Idle, now);
                    return;
                }

                this.AdvanceAdd(now, waitedTooLong);
                return;
        }

        // Idle: decide the single next action.
        var extra = team.FirstOrDefault(x => !goal.Contains(x));
        if (extra != 0)
        {
            var row = team.IndexOf(extra);
            this.removingNumber = extra;
            this.Status = $"{this.NameOf(extra)} removing…";
            FireCallback(party, 2, row);
            this.SetStep(Step.AwaitRemoveMenu, now);
            return;
        }

        var missing = goal.FirstOrDefault(x => !team.Contains(x));
        if (missing == 0)
        {
            this.Status = $"Roster ready({team.Count}/{goal.Count})";
            return;
        }

        this.addingNumber = missing;
        this.Status = $"{this.NameOf(missing)} adding…";
        var notebook = this.FindAddon(NotebookAddonName);
        if (notebook is null || !notebook->IsVisible)
        {
            FireCallback(party, 5);
            this.nextActionAt = now + ActionResend;
            this.SetStep(Step.AwaitBestiary, now);
            return;
        }

        this.addSent = false;
        this.SetStep(Step.Adding, now);
    }

    private void AdvanceAdd(DateTime now, bool waitedTooLong)
    {
        var notebook = this.FindAddon(NotebookAddonName);
        if (notebook is null || !notebook->IsVisible || notebook->AtkValuesCount <= NotebookPageValueIndex)
        {
            this.SetStep(Step.Idle, now);
            return;
        }

        if (this.ClearNotebookFilter(notebook) || now < this.nextActionAt) return;
        // Page/slot from the game's own (sorted/filtered) bestiary list - see NotebookSlotPolicy remarks.
        var located = NotebookSlotPolicy.Locate(ReadNotebookOrder(), this.addingNumber);
        if (located is null)
        {
            this.Status = $"{this.NameOf(this.addingNumber)} is hidden by the bestiary filter - clear the filter";
            if (waitedTooLong) this.SetStep(Step.Idle, now);
            return;
        }

        var (targetPage, slot) = located.Value;
        var currentPage = (int)ReadNumber(notebook->AtkValues[NotebookPageValueIndex]);

        if (currentPage != targetPage)
        {
            if (waitedTooLong)
            {
                this.SetStep(Step.Idle, now);
                return;
            }

            if (now >= this.nextActionAt)
            {
                FireCallback(notebook, 3, targetPage);
                this.nextActionAt = now + ActionResend;
                this.addSent = false;
            }

            return;
        }

        if (!this.addSent)
        {
            this.log.Information("[BeastHelper] Crucible pet party: adding {Name} (No.{No}) at bestiary page {Page} slot {Slot}.",
                this.NameOf(this.addingNumber), this.addingNumber, targetPage + 1, slot + 1);
            FireCallback(notebook, 5, slot);
            var param = NotebookFirstEntryParam + (uint)slot;
            var evt = FindMouseDownEvent(&notebook->UldManager, param, 0);
            if (evt is not null)
            {
                var data = new AtkEventData();
                notebook->ReceiveEvent(AtkEventType.MouseDown, (int)param, evt, &data);
            }
            else
            {
                this.log.Debug("[BeastHelper] Crucible pet party: no MouseDown event found for slot {Slot}.", slot);
            }

            this.addSent = true;
            this.stepSince = now;
            return;
        }

        if (waitedTooLong)
        {
            this.log.Information("[BeastHelper] Crucible pet party: {Name} didn't appear in the team after adding; will retry.", this.NameOf(this.addingNumber));
            this.SetStep(Step.Idle, now);
        }
    }

    /// <summary>Beast numbers in the order the bestiary shows them; empty if the game hasn't built the list.</summary>
    private static List<uint> ReadNotebookOrder()
    {
        var order = new List<uint>();
        var agent = AgentXBMMonsterNotebook.Instance();
        if (agent == null) return order;
        var count = agent->Entries.LongCount;
        if (count is < 1 or > 200) return order;
        for (var i = 0; i < count; i++) order.Add(agent->Entries[i].PetId);
        return order;
    }

    private void SetStep(Step next, DateTime now)
    {
        this.step = next;
        this.stepSince = now;
        this.nextActionAt = DateTime.MinValue;
    }

    /// <summary>
    /// Resolves the beast-roster capacity. The per-board maxima are
    /// user-confirmed ground truth, so for a known selected board they win over
    /// the live XBMPetParty read — whose fixed AtkValue index has drifted and
    /// silently caps every board at 10. The live read is only consulted for a
    /// board the table doesn't cover.
    /// </summary>
    private int ResolveCapacity(int readCapacity)
    {
        if(FirstMasterLegendaryProfile.XpRun(this.configuration)) return 12;
        if(SecondMasterLegendaryProfile.XpRun(this.configuration)) return SecondMasterLegendaryProfile.XpRosterCapacity;
        if(FirstBoardScorePolicy.XpRun(this.configuration)) return 14;
        if (CrucibleBoards.IsKnown(this.configuration.SelectedCrucibleBoard))
        {
            return CrucibleBoards.CapacityFor(this.configuration.SelectedCrucibleBoard);
        }

        return readCapacity > 0 ? readCapacity : CrucibleBoards.DefaultCapacity;
    }

    private List<uint> ComputeGoal(List<uint> team, int capacity)
    {
        if(SecondMasterLegendaryProfile.MasterTrioActive(this.configuration))
        {
            if(this.clientState.TerritoryType is >=1339 and <=1343) return team.ToList();
            return (FirstMasterLegendaryProfile.XpRun(this.configuration)
                ? CrucibleTeamPlanner.FirstMasterXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out _)
                : SecondMasterLegendaryProfile.XpRun(this.configuration)
                ? CrucibleTeamPlanner.SecondMasterXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out _)
                : FirstMasterLegendaryProfile.Team(this.OwnedPets(),out _)).Select(p=>p.XbmRowId).ToList();
        }

        if(FirstBoardScorePolicy.XpRun(this.configuration))
        {
            // Duty rosters cannot be expanded after entry. Preserve the entered roster
            // even if earned XP changes the leveling order or the plugin is reloaded.
            if(this.clientState.TerritoryType is >=1339 and <=1343) return team.ToList();
            return CrucibleTeamPlanner.ThirdBoardXpRun(this.OwnedPets(),this.configuration.BeastLevelProgress,out _)
                .Select(p=>p.XbmRowId).ToList();
        }

        if (this.configuration.FirstBoardScoreEnabled)
        {
            var trio = FirstBoardScorePolicy.BeastkinClear(this.configuration)
                ? FirstBoardScorePolicy.BeastkinTrio(this.OwnedPets(),out _)
                : this.configuration.ScoreVilekinCombo
                ? FirstBoardScorePolicy.VilekinTrio(this.OwnedPets(), out _)
                : FirstBoardScorePolicy.Select(this.OwnedPets(), this.configuration.BeastLevelProgress,
                this.configuration.ScoreFamily, this.configuration.ScoreTeamPetRowIds, out _);
            if (trio.Count == 3 && this.configuration.ScoreTeamPetRowIds.Count == 0)
            {
                this.configuration.ScoreTeamPetRowIds = trio.Select(p => p.PetRowId).ToList();
                this.saveConfiguration();
                this.log.Information("[BeastHelper] First Board score trio locked: {Names}", string.Join(", ", trio.Select(p => p.Name)));
            }
            return trio.Select(p => p.XbmRowId).ToList();
        }

        if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.ClassMinMax)
            return CrucibleTeamPlanner.ClassMinMax(this.OwnedPets(), this.configuration.BeastLevelProgress, out _)
                .Select(p => p.XbmRowId).ToList();

        if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.Recommended)
        {
            var owned = this.configuration.TamedPetRowIds
                .Select(rowId => this.beastData.FindPet(rowId))
                .Where(pet => pet is not null)
                .Select(pet => pet!);

            return CrucibleTeamPlanner
                .Recommend(owned, this.configuration.BeastLevelProgress, capacity)
                .Select(pet => pet.XbmRowId)
                .ToList();
        }

        if (this.configuration.CrucibleTeamMode == CrucibleTeamMode.Manual)
        {
            // Trust the user's explicit checks verbatim — do NOT intersect with
            // the 図鑑 capture mirror (TamedPetRowIds). That mirror depends on
            // the known-unreliable rank/note sync, so when it's stale or empty
            // it silently drops every checked beast, leaving an empty goal and
            // no formation at all (which looked like "manual mode does
            // nothing"). A checked beast the player doesn't actually own simply
            // won't be found in the notebook when adding, which is a visible,
            // self-correcting miss rather than a silent wipe.
            return this.configuration.CrucibleManualTeamPetRowIds
                .Select(rowId => this.beastData.FindPet(rowId)?.XbmRowId)
                .Where(no => no is > 0)
                .Select(no => no!.Value)
                .Distinct()
                .Take(capacity)
                .ToList();
        }

        return this.configuration.TamedPetRowIds
            .Select(rowId => (RowId: rowId, No: this.beastData.FindPet(rowId)?.XbmRowId))
            .Where(x => x.No is > 0)
            .OrderBy(x => this.configuration.BeastLevelProgress.TryGetValue(x.RowId, out var p) ? p.Level : 0)
            .ThenBy(x => x.No)
            .Select(x => x.No!.Value)
            .Take(capacity)
            .ToList();
    }

    private string NameOf(uint xbmRowId) => this.beastData.FindPetByXbmRowId(xbmRowId)?.Name ?? $"No.{xbmRowId}";

    private IEnumerable<BeastPet> OwnedPets() => this.configuration.TamedPetRowIds
        .Select(this.beastData.FindPet).OfType<BeastPet>();

    /// <returns>Current team member numbers in row order, and the board's own capacity — or (null, 0) if the party addon's data hasn't loaded yet.</returns>
    private static (List<uint>? Team, int Capacity) ReadTeam(AtkUnitBase* party)
    {
        if (party->AtkValuesCount <= CapacityIndex)
        {
            return (null, DefaultCapacity);
        }

        var memberCount = (int)ReadNumber(party->AtkValues[MemberCountIndex]);
        var capacity = (int)ReadNumber(party->AtkValues[CapacityIndex]);
        if (capacity <= 0)
        {
            capacity = DefaultCapacity;
        }

        var team = new List<uint>(memberCount);
        for (var position = 0; position < memberCount; position++)
        {
            var start = FirstMemberIndex + position * MemberStride;
            if (start + MemberStride > party->AtkValuesCount)
            {
                return (null, capacity);
            }

            var icon = ReadNumber(party->AtkValues[start + 1]);
            var number = ReadNumber(party->AtkValues[start + 76]);
            if (number is < 1 or > 50 || icon != MemberIconBase + number)
            {
                return (null, capacity);
            }

            team.Add(number);
        }

        return (team, capacity);
    }

    private static AtkEvent* FindMouseDownEvent(AtkUldManager* uld, uint param, int depth)
    {
        if (uld is null || depth > 6)
        {
            return null;
        }

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node is null || !node->IsVisible())
            {
                continue;
            }

            for (var evt = node->AtkEventManager.Event; evt is not null; evt = evt->NextEvent)
            {
                if (evt->State.EventType == AtkEventType.MouseDown && evt->Param == param)
                {
                    return evt;
                }
            }

            var component = node->GetComponent();
            if (component is null)
            {
                continue;
            }

            var found = FindMouseDownEvent(&component->UldManager, param, depth + 1);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static uint ReadNumber(AtkValue value) => value.Type.ToString() switch
    {
        "Int" => value.Int >= 0 ? (uint)value.Int : 0,
        "UInt" => value.UInt,
        _ => 0,
    };

    private static void FireCallback(AtkUnitBase* addon, params int[] values)
    {
        var atkValues = stackalloc AtkValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            atkValues[i] = new AtkValue();
            atkValues[i].SetInt(values[i]);
        }

        addon->FireCallback((uint)values.Length, atkValues, true);
    }

    private AtkUnitBase* FindAddon(string name)
    {
        var ptr = this.gameGui.GetAddonByName(name);
        return ptr.IsNull ? null : (AtkUnitBase*)ptr.Address;
    }
}
