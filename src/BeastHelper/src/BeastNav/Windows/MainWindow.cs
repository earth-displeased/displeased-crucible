using System.Globalization;
using System.Numerics;
using System.Reflection;
using BeastNav.Models;
using BeastNav.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using RinUi;

namespace BeastNav.Windows;

public sealed class MainWindow : Window
{
    private const string OfuseUrl = "https://ofuse.me/rin0617";

    private static readonly string VersionLabel =
        Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"v{v.Major}.{v.Minor}.{v.Build}"
            : "dev";

    private readonly Configuration configuration;
    private readonly BeastDataService beastData;
    private readonly BeastDestinationService destinations;
    private readonly NavmeshService navmesh;
    private readonly BeastTamingStateService tamingState;
    private readonly BossModDodgeService bossModDodge;
    private readonly CrucibleBoardService crucibleBoard;
    private readonly CrucibleReadyCheckService crucibleReadyCheck;
    private readonly CrucibleCombatService crucibleCombat;
    private readonly CrucibleRecoveryItemService crucibleRecoveryItem;
    private readonly CrucibleLootService crucibleLoot;
    private readonly CrucibleResultService crucibleResult;
    private readonly CrucibleTreasureService crucibleTreasure;
    private readonly CruciblePetPartyService cruciblePetParty;
    private readonly CrucibleAutoDutyIpcService crucibleAutoDuty;
    private readonly CrucibleShopService crucibleShop;
    private readonly CrucibleLoopService crucibleLoop;
    private readonly CrucibleEntryService crucibleEntry;
    private readonly IGameGui gameGui;
    private readonly Action<BeastDestination> teleport;
    private readonly Action saveConfiguration;
    private readonly Action syncBeastNote;
    private readonly Action<bool> setBossModDodgeEnabled;
    private readonly Action stopAutomation;
    private readonly Action resumeAutomation;
    private const string NoTreasureCategoryLabel = "(Not set)";

    // Display label -> matched keyword (see CrucibleTreasureService, which
    // just does an ordered plain-substring match against
    // Configuration.CrucibleTreasureKeywords) for the priority dropdowns
    // below. Keywords are plain substrings confirmed against real treasure
    // card text this session (e.g. "被ダメージ-7%", "追加ダメージの威力：240").
    private static readonly (string Label, string Keyword)[] TreasureCategories =
    [
        ("Maximum HP", "最大HP"),
        ("Damage dealt", "与ダメージ"),
        ("Damage reduction", "被ダメージ"),
        ("Additional damage", "追加ダメージ"),
    ];

    private string filter = string.Empty;
    private int selectedIndex;
    private int selectedSection;

    private static readonly (string Label, string Description)[] Sections =
    [
        ("Capture", "Capture list, destinations, navigation and bestiary sync"),
        ("Crucible", "Crucible automation"),
        ("Beast list", "Owned beasts and their rank / EXP"),
    ];

    private static readonly string[] SectionLabels = Sections.Select(s => s.Label).ToArray();
    private static readonly string[] SectionTooltips = Sections.Select(s => s.Description).ToArray();

    public MainWindow(
        Configuration configuration,
        BeastDataService beastData,
        BeastDestinationService destinations,
        NavmeshService navmesh,
        BeastTamingStateService tamingState,
        BossModDodgeService bossModDodge,
        CrucibleBoardService crucibleBoard,
        CrucibleReadyCheckService crucibleReadyCheck,
        CrucibleCombatService crucibleCombat,
        CrucibleRecoveryItemService crucibleRecoveryItem,
        CrucibleLootService crucibleLoot,
        CrucibleResultService crucibleResult,
        CrucibleTreasureService crucibleTreasure,
        CruciblePetPartyService cruciblePetParty,
        CrucibleAutoDutyIpcService crucibleAutoDuty,
        CrucibleShopService crucibleShop,
        CrucibleLoopService crucibleLoop,
        CrucibleEntryService crucibleEntry,
        IGameGui gameGui,
        Action<BeastDestination> teleport,
        Action saveConfiguration,
        Action syncBeastNote,
        Action<bool> setBossModDodgeEnabled, Action stopAutomation, Action resumeAutomation)
        : base($"Displeased BeastHelper {VersionLabel}###DispleasedBeastHelperMain")
    {
        this.configuration = configuration;
        this.beastData = beastData;
        this.destinations = destinations;
        this.navmesh = navmesh;
        this.tamingState = tamingState;
        this.bossModDodge = bossModDodge;
        this.crucibleBoard = crucibleBoard;
        this.crucibleReadyCheck = crucibleReadyCheck;
        this.crucibleCombat = crucibleCombat;
        this.crucibleRecoveryItem = crucibleRecoveryItem;
        this.crucibleLoot = crucibleLoot;
        this.crucibleResult = crucibleResult;
        this.crucibleTreasure = crucibleTreasure;
        this.cruciblePetParty = cruciblePetParty;
        this.crucibleAutoDuty = crucibleAutoDuty;
        this.crucibleShop = crucibleShop;
        this.crucibleLoop = crucibleLoop;
        this.crucibleEntry = crucibleEntry;
        this.gameGui = gameGui;
        this.teleport = teleport;
        this.saveConfiguration = saveConfiguration;
        this.syncBeastNote = syncBeastNote;
        this.setBossModDodgeEnabled = setBossModDodgeEnabled;
        this.stopAutomation = stopAutomation;
        this.resumeAutomation = resumeAutomation;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1000, 500),
            MaximumSize = new Vector2(1400, 1000),
        };
        this.RespectCloseHotkey = true;

        this.TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Stop,
            Click = _ => this.stopAutomation(),
            ShowTooltip = () => ImGui.SetTooltip("Stop ALL BeastHelper combat and movement. Explicit Resume required."),
        });
        this.TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => Util.OpenLink(OfuseUrl),
            ShowTooltip = () => ImGui.SetTooltip($"Support the original author on OFUSE\n{OfuseUrl}"),
        });
    }

    // Accent for in-content highlights (beast-list rank/level, section bodies).
    // Points at the shared RinUi theme so BeastHelper matches the other plugins.
    private static readonly Vector4 AccentColor = Theme.Accent;
    private static readonly Vector4 GoldColor = new(0.95f, 0.75f, 0.28f, 1f);

    private static readonly Vector4[] LevelColors =
    [
        new Vector4(0.68f, 0.68f, 0.72f, 1f),
        new Vector4(0.55f, 0.85f, 0.58f, 1f),
        new Vector4(0.45f, 0.68f, 0.95f, 1f),
        new Vector4(0.78f, 0.52f, 0.95f, 1f),
        GoldColor,
    ];

    // Beast level tops out around 25 (per the user) — bucket it into 5 bands.
    private static Vector4 ColorForLevel(int level)
        => LevelColors[Math.Clamp((level - 1) / 5, 0, LevelColors.Length - 1)];

    private static int RankForDisplay(BeastPet pet, BeastProgress progress)
    {
        var rank = progress.Level;
        if (rank is >= 1 and <= 25)
        {
            return rank;
        }

        return pet.Rank;
    }

    private static string EnglishBoardName(string board) => board switch
    {
        "第一盤" => "First Board",
        "第二盤" => "Second Board",
        "第三盤" => "Third Board",
        "特一盤" => "First Master's Board",
        "特二盤" => "Second Master's Board",
        _ => board,
    };

    private static string TeamModeLabel(CrucibleTeamMode mode) => CrucibleRunSetup.RosterModeName(mode);

    private void DrawRecommendedPreview()
    {
        var owned = this.configuration.TamedPetRowIds
            .Select(rowId => this.beastData.FindPet(rowId))
            .Where(pet => pet is not null)
            .Select(pet => pet!)
            .ToList();

        if (owned.Count == 0)
        {
            ImGui.TextDisabled("No captured beasts.");
            return;
        }

        // Preview against the selected board's real capacity (10/12/14/15),
        // matching what the auto-former will actually field.
        var capacity = CrucibleBoards.CapacityFor(this.configuration.SelectedCrucibleBoard);
        var recommended = CrucibleTeamPlanner.Recommend(owned, this.configuration.BeastLevelProgress, capacity);
        var anyLevelKnown = owned.Any(pet => this.configuration.BeastLevelProgress.ContainsKey(pet.PetRowId));

        ImGui.TextDisabled(anyLevelKnown
            ? $"Recommended roster (top {recommended.Count}, by level / tier):"
            : $"Recommended roster (top {recommended.Count}, estimated by tier; ranks unknown):");

        foreach (var pet in recommended)
        {
            this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId, out var progress);
            var rank = progress is null ? pet.Rank : RankForDisplay(pet, progress);
            ImGui.Bullet();
            ImGui.SameLine();
            using (PushColor(ImGuiCol.Text, ColorForLevel(rank)))
            {
                ImGui.TextUnformatted($"{pet.Name}");
            }

            ImGui.SameLine();
            ImGui.TextDisabled(progress is null ? $"(Tier {pet.Rank})" : $"(RANK {rank})");
        }
    }

    public override void Draw()
    {
        Theme.Push();
        try
        {
            this.selectedSection = Sidebar.Draw(
                "Displeased BeastHelper",
                VersionLabel,
                SectionLabels,
                this.selectedSection,
                OfuseUrl,
                SectionTooltips);

            if (ImGui.BeginChild("##DispleasedBeastHelperContent", Vector2.Zero, true))
            {
                var (label, _) = Sections[this.selectedSection];
                ImGui.SetWindowFontScale(1.15f);
                Theme.Heading(label);
                ImGui.SetWindowFontScale(1f);
                ImGui.Spacing();

                switch (this.selectedSection)
                {
                    case 0:
                        this.DrawCaptureSection();
                        break;
                    case 1:
                        this.DrawCrucibleTab();
                        break;
                    case 2:
                        this.DrawOwnedBeastListSection();
                        break;
                }
            }

            ImGui.EndChild();
        }
        finally
        {
            Theme.Pop();
        }
    }

    private static IDisposable PushColor(ImGuiCol col, Vector4 color, bool condition = true)
    {
        if (!condition)
        {
            return NullDisposable.Instance;
        }

        ImGui.PushStyleColor(col, color);
        return new PopStyleColorOnDispose();
    }

    private sealed class PopStyleColorOnDispose : IDisposable
    {
        public void Dispose() => ImGui.PopStyleColor();
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private void DrawCaptureSection()
    {
        this.DrawControls();
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Filter by name, No, or description", ref this.filter, 256);
        ImGui.Separator();
        this.DrawPetList();
    }

    /// <summary>View-only table of currently-owned beasts and their rank/level/exp — no move/map actions, no filter.</summary>
    private void DrawOwnedBeastListSection()
    {
        var owned = this.beastData.Pets
            .Where(pet => this.configuration.TamedPetRowIds.Contains(pet.PetRowId))
            .OrderBy(pet => pet.XbmRowId)
            .ToArray();

        ImGui.TextDisabled($"Owned: {owned.Length}/{this.beastData.Pets.Count}");

        var autoSyncLevel = this.configuration.AutoSyncBeastLevelEnabled;
        if (ImGui.Checkbox("Sync rank / EXP when bestiary opens", ref autoSyncLevel))
        {
            this.configuration.AutoSyncBeastLevelEnabled = autoSyncLevel;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Automatically page through the bestiary and read rank / EXP for all 50 beasts. Closing the bestiary interrupts the sync.");
        }

        ImGui.SameLine();
        ImGui.TextDisabled(this.tamingState.NotebookSyncStatus);

        ImGui.Spacing();

        if (owned.Length == 0)
        {
            ImGui.TextUnformatted("No captured beasts yet.");
            return;
        }

        if (ImGui.BeginTable("##beasthelperOwnedPets", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Roster", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("No", ImGuiTableColumnFlags.WidthFixed, 52);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("RANK", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("EXP", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableHeadersRow();

            foreach (var pet in owned)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                var inManualTeam = this.configuration.CrucibleManualTeamPetRowIds.Contains(pet.PetRowId);
                if (ImGui.Checkbox($"##team{pet.PetRowId}", ref inManualTeam))
                {
                    if (inManualTeam)
                    {
                        this.configuration.CrucibleManualTeamPetRowIds.Add(pet.PetRowId);
                    }
                    else
                    {
                        this.configuration.CrucibleManualTeamPetRowIds.Remove(pet.PetRowId);
                    }

                    this.saveConfiguration();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When Manual roster mode is selected in Crucible, use the beasts checked here.");
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(pet.XbmRowId.ToString());

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(pet.Name);

                ImGui.TableSetColumnIndex(3);
                this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId, out var progress);
                if (progress is null)
                {
                    ImGui.TextDisabled("-");
                    ImGui.TableSetColumnIndex(4);
                    ImGui.TextDisabled("-");
                    continue;
                }

                var rank = RankForDisplay(pet, progress);
                if (rank <= 0)
                {
                    ImGui.TextDisabled("-");
                }
                else
                {
                    using (PushColor(ImGuiCol.Text, ColorForLevel(rank)))
                    {
                        ImGui.TextUnformatted(rank.ToString());
                    }
                }

                ImGui.TableSetColumnIndex(4);
                if (progress.MaxExp <= 0)
                {
                    ImGui.TextDisabled("-");
                    continue;
                }

                var fraction = Math.Clamp(progress.Exp / (float)progress.MaxExp, 0f, 1f);
                using (PushColor(ImGuiCol.PlotHistogram, GoldColor))
                {
                    ImGui.ProgressBar(fraction, new Vector2(120, 16), $"{progress.Exp}/{progress.MaxExp}");
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawControls()
    {
        if (ImGui.Button("Reload"))
        {
            this.beastData.Reload();
            this.destinations.Reload();
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            this.navmesh.Stop();
        }

        ImGui.SameLine();
        if (ImGui.Button("Sync bestiary"))
        {
            this.syncBeastNote();
        }

        ImGui.SameLine();
        var autoSync = this.configuration.AutoSyncBeastNote;
        if (ImGui.Checkbox("Auto-sync bestiary", ref autoSync))
        {
            this.configuration.AutoSyncBeastNote = autoSync;
            this.saveConfiguration();
        }

        ImGui.SameLine();
        var hideCompleted = this.configuration.HideCompletedPets;
        if (ImGui.Checkbox("Hide captured / not needed", ref hideCompleted))
        {
            this.configuration.HideCompletedPets = hideCompleted;
            this.saveConfiguration();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(
            $"Captured: {this.configuration.TamedPetRowIds.Count}/{this.beastData.Pets.Count}");

        var lastSync = this.tamingState.LastResult;
        if (lastSync != BeastTamingSyncResult.NotRun && !string.IsNullOrWhiteSpace(lastSync.Message))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({lastSync.Message})");
        }

        var fly = this.configuration.Fly;
        if (ImGui.Checkbox("Fly", ref fly))
        {
            this.configuration.Fly = fly;
            this.saveConfiguration();
        }

        ImGui.SameLine();
        var range = this.configuration.StopDistance;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat("Stop distance", ref range, 1f, 30f, "%.1f"))
        {
            this.configuration.StopDistance = range;
            this.saveConfiguration();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(this.navmesh.IsReady() ? "vnavmesh: ready" : "vnavmesh: unavailable");
    }

    /// <summary>Board picked in the Board menu before a run is chosen for it; null = show the active run's board.</summary>
    private CrucibleBoard? viewBoard;

    /// <summary>Applies a board run (CrucibleRunSetup) plus the BMR side effects the old preset buttons performed.</summary>
    private void ApplyRun(CrucibleBoard board, CrucibleRun run)
    {
        if (CrucibleRunSetup.Apply(this.configuration, board, run)) this.setBossModDodgeEnabled(true);
        if (board == CrucibleBoard.SecondMaster && run is CrucibleRun.Legendary or CrucibleRun.XpRun
            && this.bossModDodge.SecondMasterExperimental == false)
            this.bossModDodge.SetSecondMasterExperimental(true);
        this.viewBoard = null;
        this.saveConfiguration();
    }

    /// <summary>Options for the Score Run, Vilekin Score Run and Beastkin Clear runs (all use the three-beast score mode).</summary>
    private void DrawScoreModeControls()
    {
        ImGui.TextWrapped(this.crucibleCombat.ScoreSetupStatus is { Length: > 0 } setup ? setup : "Plugin setup: one RSR and one BMR loaded.");
        if (this.configuration.ScoreBoardTerritory == 1340)
            ImGui.TextWrapped("Second Board: initial support; six encounter modules, including paired Tablitaur target balancing. Full-run validation is still needed. Keep replay recording enabled.");
        ImGui.TextWrapped(FirstBoardScorePolicy.ItemsAllowed(this.configuration)
            ? "Exactly three beasts, fixed for the run. Select First Degree at entry. Entries, battle starts, tile movement, rewards and shops remain manual. Recovery potions and attack items follow Duty items below. BMR AI Off; BMR dodge On; RSR Auto."
            : "Exactly three beasts from one family, fixed for the run. No item use, automatic rewards, shopping or AutoDuty. Enter and start battles manually; decline loot/gear, feed and campsites. BMR AI Off; RSR Auto.");
        var vilekin = this.configuration.ScoreVilekinCombo;
        var beastkin = FirstBoardScorePolicy.BeastkinClear(this.configuration);
        if (beastkin)
        {
            ImGui.TextWrapped("Opening Borrow for Beastskin; Coeurl sustained damage; Buffalo War Cry for exposed Ymir. Exactly three Beastkin. Aim for rank 15. Whistle order may vary.");
            ImGui.TextWrapped(this.crucibleCombat.BeastkinRsrSupported ? "RSR Beastkin clear: connected" : "RSR Beastkin clear: unavailable - load the paired RSR build.");
        }
        if (vilekin)
        {
            ImGui.TextWrapped("Venom, Eerie Soundwave, then Final Sting near the end of physical vulnerability or at low enemy HP. The game may register these three in any whistle order; the rotation follows species, not slot numbers.");
            ImGui.TextWrapped(this.crucibleCombat.VilekinRsrSupported ? "RSR Vilekin strategy: connected" : "RSR Vilekin strategy: unavailable - load the local First Board Assist RSR build before testing.");
        }
        ImGui.BeginDisabled(vilekin || beastkin || this.configuration.ScoreTeamPetRowIds.Count != 0 || this.crucibleCombat.InCrucible);
        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Family", this.configuration.ScoreFamily == 0 ? "Automatic (estimated strength)" : BeastClassifications.Name(this.configuration.ScoreFamily)))
        {
            for (byte family = 0; family <= 8; family++)
                if (ImGui.Selectable(family == 0 ? "Automatic (estimated strength)" : BeastClassifications.Name(family), family == this.configuration.ScoreFamily))
                { this.configuration.ScoreFamily = family; this.saveConfiguration(); }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        var scan = this.configuration.ClassMinMaxRankScanEnabled;
        if (ImGui.Checkbox("Read ranks from the open bestiary##score", ref scan))
        { this.configuration.ClassMinMaxRankScanEnabled = scan; this.saveConfiguration(); }
        ImGui.TextWrapped(this.tamingState.NotebookSyncStatus);
        string reason;
        var owned = this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>();
        var trio = beastkin ? FirstBoardScorePolicy.BeastkinTrio(owned, out reason)
            : vilekin ? FirstBoardScorePolicy.VilekinTrio(owned, out reason)
            : FirstBoardScorePolicy.Select(owned, this.configuration.BeastLevelProgress, this.configuration.ScoreFamily, this.configuration.ScoreTeamPetRowIds, out reason);
        if (trio.Count != 3) ImGui.TextWrapped(reason);
        else
        {
            ImGui.TextWrapped($"{(this.configuration.ScoreTeamPetRowIds.Count == 3 ? "Locked trio" : "Proposed trio")}: {BeastClassifications.Name(trio[0].Classification)} - {string.Join(", ", trio.Select(p => p.Name))}");
            ImGui.TextWrapped(beastkin ? "Fixed Beastkin team; rank sync 15. The trio locks when roster setup begins." : "Selection estimates strength at First Board's rank-5 cap; it does not calculate the best final score or rank beast abilities. The trio locks when roster setup begins.");
        }
        ImGui.BeginDisabled(this.crucibleCombat.InCrucible);
        if (ImGui.Button("Choose a new trio for the next run"))
        { this.configuration.ScoreTeamPetRowIds.Clear(); this.saveConfiguration(); }
        ImGui.EndDisabled();
    }

    private void DrawCrucibleTab()
    {
        if (ImGui.Button("STOP ALL AUTOMATION")) this.stopAutomation();
        ImGui.SameLine();
        if (this.configuration.AutomationStopped)
        {
            if (ImGui.Button("Resume saved settings")) this.resumeAutomation();
            ImGui.TextWrapped("STOPPED. Nothing restarts until you press Resume or use /dbh resume.");
        }
        else ImGui.TextUnformatted("Emergency stop: /dbh stop");
        ImGui.Spacing();

        // === Board and run ======================================================
        ImGui.TextUnformatted("Board and run");
        ImGui.Separator();
        var (board, run) = CrucibleRunSetup.Detect(this.configuration);
        var shownBoard = this.viewBoard ?? board;
        ImGui.BeginDisabled(this.crucibleCombat.InCrucible);
        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Board##runBoard", CrucibleRunSetup.BoardName(shownBoard)))
        {
            foreach (var b in CrucibleRunSetup.Boards)
                if (ImGui.Selectable(CrucibleRunSetup.BoardName(b), b == shownBoard))
                    this.viewBoard = b == board ? null : b;
            ImGui.EndCombo();
        }

        var pending = shownBoard != board;
        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Run##runType", pending ? "Choose a run..." : CrucibleRunSetup.RunName(run)))
        {
            foreach (var r in CrucibleRunSetup.Runs(shownBoard))
            {
                if (ImGui.Selectable(CrucibleRunSetup.RunName(r), !pending && r == run)) this.ApplyRun(shownBoard, r);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(CrucibleRunSetup.Description(shownBoard, r));
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (this.crucibleCombat.InCrucible) ImGui.TextDisabled("Board and run can't be changed inside the Crucible.");

        (board, run) = CrucibleRunSetup.Detect(this.configuration);
        using (PushColor(ImGuiCol.Text, GoldColor))
            ImGui.TextWrapped($"Active: {CrucibleRunSetup.Name(board, run)}");
        if (this.viewBoard is { } next)
            ImGui.TextWrapped($"Choose a run to switch to {CrucibleRunSetup.BoardName(next)}.");
        ImGui.TextWrapped(CrucibleRunSetup.Description(board, run));
        ImGui.Spacing();

        // === Roster =============================================================
        ImGui.TextUnformatted("Roster");
        ImGui.Separator();
        switch (run)
        {
            case CrucibleRun.ScoreRun or CrucibleRun.VilekinScoreRun or CrucibleRun.BeastkinClear:
                this.DrawScoreModeControls();
                break;
            case CrucibleRun.Custom:
                this.DrawCustomRoster();
                break;
            case CrucibleRun.Clear:
                this.DrawRecommendedPreview();
                break;
            default:
                this.DrawClassMinMaxPreview();
                break;
        }

        if (board == CrucibleBoard.SecondMaster && run is CrucibleRun.Legendary or CrucibleRun.XpRun)
        {
            if (this.bossModDodge.SecondMasterExperimental is bool experimental)
            {
                if (ImGui.Checkbox("BMR: unfinished Second Master encounter modules", ref experimental))
                    this.bossModDodge.SetSecondMasterExperimental(experimental);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Includes the WIP Medusa, Chimera and Lauda modules only on Second Master's Board. Coverage is not yet verified by a full-board clear.");
            }
            else ImGui.TextWrapped("Second Master encounter modules need the paired Crucible Assist BMR build.");
        }

        ImGui.TextWrapped($"Roster: {this.cruciblePetParty.Status}");
        ImGui.TextWrapped($"Whistles: {this.cruciblePetParty.WhistleStatus}");
        ImGui.Spacing();

        // === Combat =============================================================
        ImGui.TextUnformatted("Combat");
        ImGui.Separator();
        var combat = this.configuration.AutoCombatEnabled;
        if (ImGui.Checkbox("Automatic combat (RSR)", ref combat))
        {
            this.configuration.AutoCombatEnabled = combat;
            if (!combat) this.stopAutomation();
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Approach one nearby enemy and enable RotationSolverReborn Auto mode in range. RSR chooses all combat actions. Turns off when no enemies remain.");
        ImGui.SameLine();
        ImGui.TextDisabled($"{this.crucibleCombat.Status} / RSR: {(this.crucibleCombat.RsrReportsActive ? "active" : "inactive")}");

        var dodge = this.configuration.BossModDodgeEnabled;
        if (ImGui.Checkbox("BMR dodge integration", ref dodge))
        {
            this.configuration.BossModDodgeEnabled = dodge;
            this.setBossModDodgeEnabled(dodge);
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Controls BossModReborn movement only. Does not choose combat abilities. Does nothing if the encounter has no BMR module.");
        ImGui.SameLine();
        ImGui.TextDisabled($"BMR module: {(this.bossModDodge.HasActiveModule ? "yes" : "no")} / {this.bossModDodge.EncounterName} / {this.bossModDodge.Status}");
        if (this.bossModDodge.MovementWarning.Length != 0) ImGui.TextWrapped(this.bossModDodge.MovementWarning);
        this.DrawBmrMovementControls();
        this.DrawAutomationDiagnostics();
        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Duty items"))
            this.DrawCombatItemActions();

        if (run == CrucibleRun.Custom && ImGui.CollapsingHeader("Full automation and experimental options"))
            this.DrawFullAutomation();
    }

    /// <summary>Custom run: generic roster modes and the roster/whistle switch.</summary>
    private void DrawCustomRoster()
    {
        var beasts = this.configuration.CombatOnlyEnabled
            ? this.configuration.CombatOnlyBeastSelectionEnabled
            : this.configuration.AutoCruciblePetPartyEnabled && this.configuration.AutoCrucibleWhistleAssignEnabled;
        if (ImGui.Checkbox("Automatic roster and whistles", ref beasts))
        {
            this.configuration.CombatOnlyBeastSelectionEnabled = beasts;
            this.configuration.AutoCruciblePetPartyEnabled = beasts;
            this.configuration.AutoCrucibleWhistleAssignEnabled = beasts;
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("While the roster screen is open, remove beasts outside the target roster, add missing ones through the bestiary, then assign whistles.");

        var mode = this.configuration.CrucibleTeamMode;
        ImGui.SetNextItemWidth(320);
        if (ImGui.BeginCombo("Roster mode", CrucibleRunSetup.RosterModeName(mode)))
        {
            foreach (var m in CrucibleRunSetup.CustomRosterModes(this.configuration.CombatOnlyEnabled))
                if (ImGui.Selectable(CrucibleRunSetup.RosterModeName(m), m == mode))
                {
                    this.configuration.CrucibleTeamMode = m;
                    this.saveConfiguration();
                }
            ImGui.EndCombo();
        }

        if (mode == CrucibleTeamMode.Manual) ImGui.TextDisabled("Choose the beasts in Beast list.");
        else if (mode == CrucibleTeamMode.Recommended) this.DrawRecommendedPreview();
        else this.DrawClassMinMaxPreview();
        ImGui.TextWrapped("Open the roster / whistle screen for selection. Press Start Battle yourself after assignment finishes.");
    }

    /// <summary>Custom run only: auto-entry, AutoDuty and menu automation (all off for the named runs).</summary>
    private void DrawFullAutomation()
    {
        var full = !this.configuration.CombatOnlyEnabled;
        if (ImGui.Checkbox("Full automation (entry, menus and rewards)", ref full))
        {
            this.configuration.CombatOnlyEnabled = !full;
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off (default): combat only - you handle entry, Start Battle, rewards, shops and prompts. On: the switches below run as well.");
        if (!full)
        {
            ImGui.TextDisabled("Off: combat only. Entry, Start Battle, rewards, shops and prompts are yours.");
            return;
        }

        ImGui.TextWrapped("Move between board tiles manually. Tile-to-tile movement is not automated.");
        var autoEnter = this.configuration.AutoEnterCrucibleEnabled;
        if (ImGui.Checkbox("Auto-enter", ref autoEnter))
        {
            this.configuration.AutoEnterCrucibleEnabled = autoEnter;
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Approach the entrance NPC and handle entry, treasure, results and loot screens. Choose routes manually.");

        var loopCount = this.configuration.CrucibleLoopCount;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Run count (0 = unlimited)", ref loopCount, 1, 5))
        {
            this.configuration.CrucibleLoopCount = Math.Max(0, loopCount);
            this.saveConfiguration();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("A run is counted when the entrance NPC is detected again. Auto-entry stops at the configured count. The counter resets when auto-entry is manually enabled.");
        ImGui.SameLine();
        ImGui.TextDisabled(this.crucibleLoop.Status);

        var autoSelectBoard = this.configuration.AutoSelectBoardEnabled;
        using (PushColor(ImGuiCol.Text, GoldColor))
        {
            if (ImGui.Checkbox("Auto-select board (experimental)", ref autoSelectBoard))
            {
                this.configuration.AutoSelectBoardEnabled = autoSelectBoard;
                this.saveConfiguration();
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Select the board chosen above after a two-second delay. This feature was implicated in game crashes; the delay is a mitigation, not a proven fix.");

        var autoDutyMode = this.configuration.CrucibleAutoDutyModeEnabled;
        using (PushColor(ImGuiCol.Text, GoldColor))
        {
            if (ImGui.Checkbox("Delegate entire board to AutoDuty (experimental)", ref autoDutyMode))
            {
                this.configuration.CrucibleAutoDutyModeEnabled = autoDutyMode;
                this.saveConfiguration();
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Requires AutoDuty and a prepared Crucible path. BeastHelper's own Crucible automation stops to avoid conflicts; bestiary sync and BMR dodging remain available.");
        if (autoDutyMode) ImGui.TextDisabled($"{this.crucibleAutoDuty.Status} - roster, whistle, treasure, item and board settings are bypassed.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Treasure preferences (priority 1 is highest)");
        var treasureSlots = new string?[TreasureCategories.Length];
        for (var i = 0; i < treasureSlots.Length; i++)
            treasureSlots[i] = i < this.configuration.CrucibleTreasureKeywords.Count ? this.configuration.CrucibleTreasureKeywords[i] : null;

        var treasureChanged = false;
        for (var slot = 0; slot < treasureSlots.Length; slot++)
        {
            var currentLabel = LabelForTreasureKeyword(treasureSlots[slot]);
            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo($"Priority {slot + 1}##treasurePriority{slot}", currentLabel))
            {
                if (ImGui.Selectable(NoTreasureCategoryLabel, currentLabel == NoTreasureCategoryLabel))
                {
                    treasureSlots[slot] = null;
                    treasureChanged = true;
                }

                foreach (var (label, keyword) in TreasureCategories)
                {
                    if (ImGui.Selectable(label, currentLabel == label))
                    {
                        treasureSlots[slot] = keyword;
                        treasureChanged = true;
                    }
                }

                ImGui.EndCombo();
            }
        }

        if (treasureChanged)
        {
            this.configuration.CrucibleTreasureKeywords = treasureSlots.Where(k => k is not null).Select(k => k!).ToList();
            this.saveConfiguration();
        }

        ImGui.TextDisabled("Shops: automation is switched off in this version; buy and leave shops yourself.");
    }

    private void DrawBmrMovementControls()
    {
        ImGui.TextWrapped($"BMR active preset: {this.bossModDodge.ActivePreset}");
        if (this.configuration.BossModDodgeEnabled && ImGui.Button("Refresh BMR movement setup"))
        {
            this.setBossModDodgeEnabled(false);
            this.setBossModDodgeEnabled(true);
        }

        ImGui.TextWrapped("BMR movement prioritizes its safe destination over staying in attack range. The vnavmesh path indicator is approach movement only; idle there does not mean BMR has stopped. A loaded module alone is not proof that every mechanic is handled.");
    }

    private void DrawCombatItemActions()
    {
        if (this.configuration.FirstBoardScoreEnabled && !FirstBoardScorePolicy.ItemsAllowed(this.configuration))
        {
            ImGui.TextWrapped("Crucible score mode: all duty-item actions are locked OFF, including custom rules. Rewards and shops are manual; decline all offered items and gear. Do not use feed or campsites. RSR HP/MP potions must remain off too.");
            return;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Automatic duty-item actions");
        ImGui.TextWrapped("Each switch below is independent. Your previously saved item switches are retained. Recovery and attack items are used during combat. Thief's Eye is prepared before the automated approach.");
        var prepareRecovery=this.configuration.PrepareRecoveryBeforePull;
        if(ImGui.Checkbox("Prepare available recovery before automated approach",ref prepareRecovery))
        {
            this.configuration.PrepareRecoveryBeforePull=prepareRecovery;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("After you start a battle, checks your recovery threshold before approaching. Waits for requested items; continues if none is usable. Does not start a battle, choose rewards, or shop.");
        var emergencyMitigation=this.configuration.AutoEmergencyMitigationEnabled;
        if(ImGui.Checkbox("Use defensive items when healing is unavailable",ref emergencyMitigation))
        {
            this.configuration.AutoEmergencyMitigationEnabled=emergencyMitigation;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("At the enabled player or familiar recovery threshold, try healing first, then held Tannin, Blink, Reflect, Tempered Evasion, Tempered Constitution, or self-only Breathtaking Swiftness, then G2/G1 Reraiser as a last resort. Skips active buffs; shares item cooldown. Does not use reset sand, elemental gambles, or potions that inflict ailments.");
        var conditionalRecovery=this.configuration.AutoConditionalRecoveryEnabled;
        if(ImGui.Checkbox("Use cure items / Crimson Ribbon serums for healing",ref conditionalRecovery))
        {
            this.configuration.AutoConditionalRecoveryEnabled=conditionalRecovery;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("At recovery HP thresholds: cures require a matching debuff and a usable cure action; serums require Crimson Ribbon detected in your owned-gear HUD. Applies to you and the enabled familiar recovery option.");
        ImGui.TextWrapped(this.crucibleRecoveryItem.ConditionalHealingStatus);
        var familiarRecovery=this.configuration.AutoFamiliarRecoveryEnabled;
        if(ImGui.Checkbox(SecondMasterLegendaryProfile.MasterTrioActive(this.configuration) ? "Heal active Cu Sith / Behemoth / Coeurl with potions" : "Heal active Cu Sith / Buffalo / Coeurl with potions",ref familiarRecovery))
        {
            this.configuration.AutoFamiliarRecoveryEnabled=familiarRecovery;
            this.saveConfiguration();
        }
        var familiarThreshold=this.configuration.FamiliarRecoveryHpThreshold;
        if(ImGui.SliderFloat("Familiar potion HP threshold (%)",ref familiarThreshold,1f,90f))
        {
            this.configuration.FamiliarRecoveryHpThreshold=familiarThreshold;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("First Master profiles and Third Board Beastkin modes. Uses G1-G4 Beast Potions on your active living familiar. Player recovery takes priority; cannot revive a dead beast.");
        var autoLootBoost=this.configuration.AutoLootBoostEnabled;
        if(ImGui.Checkbox("Automatically use Thief's Eye before combat",ref autoLootBoost))
        {
            this.configuration.AutoLootBoostEnabled=autoLootBoost;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("Uses a held loot booster before approaching enemies; skips an existing buff. No-item score modes still block these boosts.");
        var autoMerchantEye=this.configuration.AutoMerchantEyeEnabled;
        if(ImGui.Checkbox("Automatically use Merchant's Eye before combat",ref autoMerchantEye))
        {
            this.configuration.AutoMerchantEyeEnabled=autoMerchantEye;
            this.saveConfiguration();
        }
        ImGui.TextWrapped("Merchant's Eye increases territory tokens. Both eyes are checked independently and never refreshed while their buff is active.");
        var autoRecoveryItem = this.configuration.AutoRecoveryItemEnabled;
        if (ImGui.Checkbox("Automatically use recovery potions", ref autoRecoveryItem))
        {
            this.configuration.AutoRecoveryItemEnabled = autoRecoveryItem;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("During combat, use the best available Crucible recovery item below this HP threshold. These are duty items, not ordinary potions. Disabled if the native function cannot be found.");
        }

        var recoveryThreshold = this.configuration.AutoRecoveryItemHpThreshold;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat("HP threshold (%)##recoveryItem", ref recoveryThreshold, 1f, 100f, "%.0f"))
        {
            this.configuration.AutoRecoveryItemHpThreshold = Math.Clamp(recoveryThreshold, 1f, 100f);
            this.saveConfiguration();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(this.crucibleRecoveryItem.Status);

        ImGui.BeginDisabled(this.configuration.FirstBoardScoreEnabled);
        var autoSelfBuffItems = this.configuration.AutoSelfBuffItemsEnabled;
        if (ImGui.Checkbox("Automatically use self-buff items", ref autoSelfBuffItems))
        {
            this.configuration.AutoSelfBuffItemsEnabled = autoSelfBuffItems;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use self-buff items as soon as their shared cooldown permits. No timing prediction. Recovery potions take priority below the HP threshold.");
        }

        ImGui.EndDisabled();
        var autoOffensiveItems = this.configuration.AutoOffensiveItemsEnabled;
        if (ImGui.Checkbox("Automatically use attack items (fangs)", ref autoOffensiveItems))
        {
            this.configuration.AutoOffensiveItemsEnabled = autoOffensiveItems;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use attack items against the current hostile target in fixed priority order. Does not match fang elements to enemy weaknesses.");
        }

        ImGui.Spacing();

        ImGui.BeginDisabled(this.configuration.FirstBoardScoreEnabled);
        var autoCrucibleItemRules = this.configuration.AutoCrucibleItemRulesEnabled;
        if (ImGui.Checkbox("Enable custom item-use rules", ref autoCrucibleItemRules))
        {
            this.configuration.AutoCrucibleItemRulesEnabled = autoCrucibleItemRules;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use the specified item when its conditions match. Rules are evaluated top to bottom before the built-in item options; unavailable items are skipped.");
        }

        this.DrawCrucibleItemRules();
        ImGui.EndDisabled();
        if (FirstBoardScorePolicy.ItemsAllowed(this.configuration)) ImGui.TextWrapped("Third Board clear mode permits healing and attack items. Self-buff items and custom rules remain off.");
    }

    private void DrawClassMinMaxPreview()
    {
        if (this.configuration.FirstBoardScoreEnabled) return;
        if(FirstMasterLegendaryProfile.XpRun(this.configuration))
        {
            ImGui.TextWrapped("First Master's Board / Normal XP run: Cu Sith, Behemoth and Coeurl handle combat; the other nine slots go to your lowest-ranked captured beasts. Select Normal yourself. Entry, battle starts, routes and camps remain manual.");
            var boardMenus = this.configuration.FirstMasterXpBoardMenusEnabled;
            if (ImGui.Checkbox("Auto chests, results and loot (gear: mitigation first, then damage; shops are manual)", ref boardMenus))
            {
                this.configuration.FirstMasterXpBoardMenusEnabled = boardMenus;
                this.saveConfiguration();
            }
            var masterXpRoster=CrucibleTeamPlanner.FirstMasterXpRun(this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>(),this.configuration.BeastLevelProgress,out var masterXpReason);
            if(this.crucibleCombat.InCrucible) ImGui.TextWrapped("Roster is fixed after entry; XP passengers can only be changed before the next run.");
            if(masterXpReason.Length>0) ImGui.TextWrapped(masterXpReason);
            else
            {
                ImGui.TextUnformatted($"Next entry roster: {masterXpRoster.Count}/12");
                foreach(var pet in masterXpRoster) ImGui.BulletText($"{pet.Name} - rank {CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,pet)?.ToString() ?? "unknown"}");
                if(masterXpRoster.Skip(3).Any(p=>CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,p) is null))
                    ImGui.TextWrapped("Some ranks are unknown; those beasts are treated as low rank. Ranks are learned as beasts appear on the whistle screen.");
            }
            ImGui.TextWrapped("Only the original three are assigned to battlehorns. Preserves current combat/item tuning and Treant Meteor timing. This full roster does not retain the three-beast score bonus.");
            return;
        }
        if(SecondMasterLegendaryProfile.XpRun(this.configuration))
        {
            ImGui.TextWrapped("Second Master's Board XP run: Cu Sith, Behemoth and Coeurl fight with the Second Master Legendary strategies and item settings; the other 12 slots go to your lowest-ranked captured beasts. Select the difficulty yourself (Standard recommended). Entry, battle starts, routes, camps, rewards and shops remain manual.");
            var secondXpRoster=CrucibleTeamPlanner.SecondMasterXpRun(this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>(),this.configuration.BeastLevelProgress,out var secondXpReason);
            if(this.crucibleCombat.InCrucible) ImGui.TextWrapped("Roster is fixed after entry; XP passengers can only be changed before the next run.");
            if(secondXpReason.Length>0) ImGui.TextWrapped(secondXpReason);
            else
            {
                ImGui.TextUnformatted($"Next entry roster: {secondXpRoster.Count}/{SecondMasterLegendaryProfile.XpRosterCapacity}");
                foreach(var pet in secondXpRoster) ImGui.BulletText($"{pet.Name} - rank {CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,pet)?.ToString() ?? "unknown"}");
                if(secondXpRoster.Skip(3).Any(p=>CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,p) is null))
                    ImGui.TextWrapped("Some ranks are unknown; those beasts are treated as low rank. Ranks are learned as beasts appear on the whistle screen.");
            }
            ImGui.TextWrapped("Only the original three are assigned to battlehorns. A full roster loses Beast Squad III (4,250), so expect a lower score than the three-beast Legendary preset.");
            return;
        }
        if(SecondMasterLegendaryProfile.Active(this.configuration))
        {
            ImGui.TextWrapped("Second Master's Board / Legendary. Exactly Cu Sith, Behemoth and Coeurl (Beast Squad III + Beastkin Gambit). Select First Degree in the game yourself.");
            var secondTeam=FirstMasterLegendaryProfile.Team(this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>(),out var secondReason);
            if(secondReason.Length>0) ImGui.TextWrapped(secondReason);
            foreach(var pet in secondTeam) ImGui.BulletText($"{pet.Name} - rank {(this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId,out var rank)?rank.Level.ToString():"unknown")}");
            ImGui.TextWrapped("Same item and recovery settings as First Master Legendary (player below 55%, familiars below 45%, emergency items, fangs). BMR movement is limited to territory 1343 and the unfinished Medusa / Chimera / Lauda modules are switched on.");
            ImGui.TextWrapped("Encounter-specific covers and priorities are not built yet: record every attempt with BMR. Shops, loot, camps, routes and entry remain manual.");
            return;
        }
        if(FirstMasterLegendaryProfile.Active(this.configuration))
        {
            ImGui.TextWrapped("First Master's Board / Legendary initial test. Exactly Cu Sith, Behemoth and Coeurl. Select First Degree in the game yourself. Target rank 20; this is not an XP roster.");
            var masterTeam=FirstMasterLegendaryProfile.Team(this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>(),out var masterReason);
            if(masterReason.Length>0) ImGui.TextWrapped(masterReason);
            foreach(var pet in masterTeam) ImGui.BulletText($"{pet.Name} - rank {(this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId,out var rank)?rank.Level.ToString():"unknown")}");
            ImGui.TextWrapped("Player recovery below 55%, familiar recovery below 45%, pre-pull recovery and emergency defensive items enabled. Shops, loot, camps and entry remain manual. Prioritize poison protection for Borgny.");
            ImGui.TextWrapped("Uses existing RSR rotation and BMR encounter modules. Third Board's custom Beastkin rotation, Behemoth-specific tanking, interrupts, and First Master add/charge priorities are not yet validated. Record the first attempt; manual intervention may be needed.");
            return;
        }
        if(FirstBoardScorePolicy.XpRun(this.configuration))
        {
            ImGui.TextWrapped("Standard-difficulty XP roster: Cu Sith, Buffalo and Coeurl fight normally; the other 11 places go to your lowest-ranked captured beasts. Entry, fight starts, rewards and shops remain manual. Select Standard yourself in the game.");
            var roster=CrucibleTeamPlanner.ThirdBoardXpRun(this.configuration.TamedPetRowIds.Select(this.beastData.FindPet).OfType<BeastPet>(),this.configuration.BeastLevelProgress,out var xpReason);
            if(this.crucibleCombat.InCrucible) ImGui.TextWrapped("Roster is fixed after entry. Extra XP beasts can only be added before starting the next board run.");
            if(xpReason.Length>0) ImGui.TextWrapped(xpReason);
            else
            {
                ImGui.TextUnformatted($"Next entry roster: {roster.Count}/14");
                foreach(var pet in roster) ImGui.BulletText($"{pet.Name} - rank {CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,pet)?.ToString() ?? "unknown"}");
                if(roster.Skip(3).Any(p=>CrucibleTeamPlanner.KnownRank(this.configuration.BeastLevelProgress,p) is null))
                    ImGui.TextWrapped("Some ranks are unknown; those beasts are treated as low rank. Ranks are learned as beasts appear on the whistle screen.");
            }
            return;
        }
        if (this.configuration.CrucibleTeamMode != CrucibleTeamMode.ClassMinMax) return;
        ImGui.TextWrapped("Exactly three captured beasts of one classification. Lowest combined synced rank wins; ties prefer the lower highest rank, then classification ID. Unknown ranks pause selection. The roster updates when rank data changes.");
        var scan = this.configuration.ClassMinMaxRankScanEnabled;
        if (ImGui.Checkbox("Read all ranks while bestiary is open", ref scan))
        {
            this.configuration.ClassMinMaxRankScanEnabled = scan;
            this.saveConfiguration();
        }

        ImGui.TextWrapped("To refresh ranks, enable this and open the bestiary yourself. It pages through beast details; it never opens rewards or shops.");
        ImGui.TextWrapped(this.tamingState.NotebookSyncStatus);
        var trio = CrucibleTeamPlanner.ClassMinMax(this.configuration.TamedPetRowIds
            .Select(this.beastData.FindPet).OfType<BeastPet>(), this.configuration.BeastLevelProgress, out var reason);
        if (trio.Count != 3) { ImGui.TextWrapped(reason); return; }
        ImGui.TextWrapped($"Selected: {BeastClassifications.Name(trio[0].Classification)} | total rank {trio.Sum(p => this.configuration.BeastLevelProgress[p.PetRowId].Level)}");
        foreach (var beast in trio)
            ImGui.TextWrapped($"- {beast.Name}: rank {this.configuration.BeastLevelProgress[beast.PetRowId].Level}");
    }

    private void DrawAutomationDiagnostics()
    {
        var enabled = this.configuration.ShowAutomationDiagnostics;
        if (ImGui.Checkbox("Live automation status and activity log", ref enabled))
        {
            this.configuration.ShowAutomationDiagnostics = enabled;
            this.saveConfiguration();
        }

        if (!enabled) return;
        var recover = this.configuration.RecoverStoppedRsrEnabled;
        if (ImGui.Checkbox("Recover stopped RSR during battle (max 3 retries)", ref recover))
        {
            this.configuration.RecoverStoppedRsrEnabled = recover;
            this.saveConfiguration();
        }

        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Retry Auto after RSR reports inactive for five seconds in a live battle, at most three times. No recovery while dead, zoning or in events. To stop attacks manually, turn off Automatic combat in BeastHelper.");
        var age = (DateTime.UtcNow - this.crucibleCombat.LastTickUtc).TotalSeconds;
        var healthy = age < 3;
        ImGui.TextColored(healthy ? AccentColor : GoldColor,
            healthy ? $"RUNNING {new[] { "|", "/", "-", "\\" }[(int)(ImGui.GetTime() * 3) % 4]} - {this.crucibleCombat.Status}"
                : "UPDATE LOOP STALE - no combat update in the last 3 seconds");
        ImGui.TextWrapped(this.crucibleCombat.DiagnosticSummary);
        if (this.crucibleCombat.Warning.Length > 0)
        {
            using (PushColor(ImGuiCol.Text, GoldColor)) ImGui.TextWrapped(this.crucibleCombat.Warning);
        }
        ImGui.TextWrapped("Running means the update loop is alive, not proof that RSR is using abilities. Observed casts show action IDs; instant abilities may not appear.");
        if (ImGui.CollapsingHeader("Recent automation activity (last 150 changes)"))
        {
            if (ImGui.Button("Copy log"))
                ImGui.SetClipboardText(this.crucibleCombat.DiagnosticSummary + "\n" + string.Join("\n", this.crucibleCombat.Activity));
            ImGui.SameLine();
            if (ImGui.Button("Clear log")) this.crucibleCombat.ClearActivity();
            ImGui.BeginChild("##automationActivity", new Vector2(0, 180), true);
            foreach (var entry in this.crucibleCombat.Activity.Reverse()) ImGui.TextWrapped(entry);
            ImGui.EndChild();
        }

        ImGui.Separator();
    }

    private static readonly CrucibleItemRuleConditionType[] ConditionTypeValues =
    [
        CrucibleItemRuleConditionType.SelfStatus,
        CrucibleItemRuleConditionType.TargetStatus,
        CrucibleItemRuleConditionType.SelfHp,
        CrucibleItemRuleConditionType.TargetHp,
        CrucibleItemRuleConditionType.TargetDataId,
    ];

    private static readonly string[] ConditionTypeLabels =
        ["Player status", "Target status", "Player HP", "Target HP", "Target monster ID"];

    private void DrawCrucibleItemRules()
    {
        var rules = this.configuration.CrucibleItemRules;
        ImGui.Indent();

        CrucibleItemRule? ruleToRemove = null;
        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            ImGui.PushID(ruleIndex);

            var itemLabel = rule.ItemId == 0 ? "(Not selected)" : CrucibleRecoveryItemService.GetItemName(rule.ItemId);
            var headerLabel = $"{(rule.Enabled ? string.Empty : "[Disabled] ")}{rule.Name} → {itemLabel}###ruleHeader";
            if (ImGui.CollapsingHeader(headerLabel))
            {
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("Enabled##ruleEnabled", ref enabled))
                {
                    rule.Enabled = enabled;
                    this.saveConfiguration();
                }

                var name = rule.Name;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputText("Rule name##ruleName", ref name, 64))
                {
                    rule.Name = name;
                    this.saveConfiguration();
                }

                ImGui.SetNextItemWidth(200);
                if (ImGui.BeginCombo("Item to use##ruleItem", itemLabel))
                {
                    foreach (var candidateId in CrucibleRecoveryItemService.KnownItemIds)
                    {
                        var candidateLabel = CrucibleRecoveryItemService.GetItemName(candidateId);
                        if (ImGui.Selectable($"{candidateLabel}##item{candidateId}", rule.ItemId == candidateId))
                        {
                            rule.ItemId = candidateId;
                            this.saveConfiguration();
                        }
                    }

                    ImGui.EndCombo();
                }

                var joinAll = rule.ConditionJoinMode == CrucibleItemRuleConditionJoinMode.All;
                ImGui.SetNextItemWidth(160);
                if (ImGui.BeginCombo("Combine conditions##ruleJoin", joinAll ? "All conditions (AND)" : "Any condition (OR)"))
                {
                    if (ImGui.Selectable("All conditions (AND)", joinAll))
                    {
                        rule.ConditionJoinMode = CrucibleItemRuleConditionJoinMode.All;
                        this.saveConfiguration();
                    }

                    if (ImGui.Selectable("Any condition (OR)", !joinAll))
                    {
                        rule.ConditionJoinMode = CrucibleItemRuleConditionJoinMode.Any;
                        this.saveConfiguration();
                    }

                    ImGui.EndCombo();
                }

                ImGui.TextUnformatted("Conditions:");
                CrucibleItemRuleCondition? conditionToRemove = null;
                for (var conditionIndex = 0; conditionIndex < rule.Conditions.Count; conditionIndex++)
                {
                    ImGui.PushID(conditionIndex);
                    this.DrawCrucibleItemRuleCondition(rule.Conditions[conditionIndex]);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove##conditionRemove"))
                    {
                        conditionToRemove = rule.Conditions[conditionIndex];
                    }

                    ImGui.PopID();
                }

                if (conditionToRemove is not null)
                {
                    rule.Conditions.Remove(conditionToRemove);
                    this.saveConfiguration();
                }

                if (ImGui.SmallButton("Add condition##addCondition"))
                {
                    rule.Conditions.Add(new CrucibleItemRuleCondition());
                    this.saveConfiguration();
                }

                ImGui.Spacing();
                if (ImGui.SmallButton("Delete this rule##removeRule"))
                {
                    ruleToRemove = rule;
                }
            }

            ImGui.PopID();
        }

        if (ruleToRemove is not null)
        {
            rules.Remove(ruleToRemove);
            this.saveConfiguration();
        }

        if (ImGui.SmallButton("+ Add rule"))
        {
            rules.Add(new CrucibleItemRule());
            this.saveConfiguration();
        }

        ImGui.Unindent();
    }

    private void DrawCrucibleItemRuleCondition(CrucibleItemRuleCondition condition)
    {
        var typeIndex = Array.IndexOf(ConditionTypeValues, condition.Type);
        var typeLabel = typeIndex >= 0 ? ConditionTypeLabels[typeIndex] : ConditionTypeLabels[0];
        ImGui.SetNextItemWidth(170);
        if (ImGui.BeginCombo("Type##conditionType", typeLabel))
        {
            for (var i = 0; i < ConditionTypeValues.Length; i++)
            {
                if (ImGui.Selectable(ConditionTypeLabels[i], condition.Type == ConditionTypeValues[i]))
                {
                    condition.Type = ConditionTypeValues[i];
                    this.saveConfiguration();
                }
            }

            ImGui.EndCombo();
        }

        switch (condition.Type)
        {
            case CrucibleItemRuleConditionType.SelfStatus:
            case CrucibleItemRuleConditionType.TargetStatus:
            {
                ImGui.SameLine();
                var present = condition.StatusCondition == CrucibleItemRuleStatusCondition.Present;
                ImGui.SetNextItemWidth(90);
                if (ImGui.BeginCombo("State##conditionStatusCond", present ? "Present" : "Missing"))
                {
                    if (ImGui.Selectable("Present", present))
                    {
                        condition.StatusCondition = CrucibleItemRuleStatusCondition.Present;
                        this.saveConfiguration();
                    }

                    if (ImGui.Selectable("Missing", !present))
                    {
                        condition.StatusCondition = CrucibleItemRuleStatusCondition.Missing;
                        this.saveConfiguration();
                    }

                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                var statusId = (int)condition.StatusId;
                ImGui.SetNextItemWidth(90);
                if (ImGui.InputInt("Status ID##conditionStatusId", ref statusId))
                {
                    condition.StatusId = (uint)Math.Max(0, statusId);
                    this.saveConfiguration();
                }

                break;
            }

            case CrucibleItemRuleConditionType.SelfHp:
            case CrucibleItemRuleConditionType.TargetHp:
            {
                ImGui.SameLine();
                var above = condition.HpCondition == CrucibleItemRuleHpCondition.Above;
                ImGui.SetNextItemWidth(80);
                if (ImGui.BeginCombo("##conditionHpCond", above ? "At least" : "Below"))
                {
                    if (ImGui.Selectable("At least", above))
                    {
                        condition.HpCondition = CrucibleItemRuleHpCondition.Above;
                        this.saveConfiguration();
                    }

                    if (ImGui.Selectable("Below", !above))
                    {
                        condition.HpCondition = CrucibleItemRuleHpCondition.Below;
                        this.saveConfiguration();
                    }

                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                var threshold = condition.HpThreshold;
                ImGui.SetNextItemWidth(110);
                if (ImGui.SliderFloat("HP%##conditionHpThreshold", ref threshold, 1f, 100f, "%.0f"))
                {
                    condition.HpThreshold = Math.Clamp(threshold, 1f, 100f);
                    this.saveConfiguration();
                }

                break;
            }

            case CrucibleItemRuleConditionType.TargetDataId:
            {
                ImGui.SameLine();
                var dataId = (int)condition.DataId;
                ImGui.SetNextItemWidth(120);
                if (ImGui.InputInt("Monster ID##conditionDataId", ref dataId))
                {
                    condition.DataId = (uint)Math.Max(0, dataId);
                    this.saveConfiguration();
                }

                break;
            }
        }
    }

    private static string LabelForTreasureKeyword(string? keyword)
    {
        if (keyword is null)
        {
            return NoTreasureCategoryLabel;
        }

        foreach (var (label, candidateKeyword) in TreasureCategories)
        {
            if (candidateKeyword == keyword)
            {
                return label;
            }
        }

        return NoTreasureCategoryLabel;
    }

    private void DrawPetList()
    {
        var rows = this.FilteredPets().ToArray();
        if (rows.Length == 0)
        {
            ImGui.TextUnformatted("No XBMPet rows loaded.");
            return;
        }

        if (this.selectedIndex >= rows.Length)
        {
            this.selectedIndex = rows.Length - 1;
        }

        if (ImGui.BeginTable("##beasthelperPets", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("No", ImGuiTableColumnFlags.WidthFixed, 52);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.9f);
            ImGui.TableSetupColumn("RANK", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Dest", ImGuiTableColumnFlags.WidthStretch, 1.1f);
            ImGui.TableSetupColumn("Stats", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableHeadersRow();

            for (var i = 0; i < rows.Length; i++)
            {
                var pet = rows[i];
                var destination = this.destinations.Find(pet.PetRowId);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(pet.XbmRowId.ToString());

                ImGui.TableSetColumnIndex(1);
                var captured = this.configuration.TamedPetRowIds.Contains(pet.PetRowId);
                if (captured)
                {
                    using (PushColor(ImGuiCol.Text, AccentColor))
                    {
                        ImGui.TextUnformatted("[Captured]");
                    }

                    ImGui.SameLine();
                }

                if (ImGui.Selectable($"{pet.Name}##{pet.PetRowId}", i == this.selectedIndex))
                {
                    this.selectedIndex = i;
                }

                if (captured && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Registered in bestiary (captured)");
                }

                if (ImGui.BeginPopupContextItem($"##petmenu{pet.PetRowId}"))
                {
                    using (var disabled = ImRaiiDisabled(destination is null))
                    {
                        if (ImGui.MenuItem("Move"))
                        {
                            this.MoveTo(pet, destination);
                        }

                        if (ImGui.MenuItem("Open map"))
                        {
                            this.OpenMap(destination);
                        }
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableSetColumnIndex(2);
                this.configuration.BeastLevelProgress.TryGetValue(pet.PetRowId, out var progress);
                if (progress is null)
                {
                    ImGui.TextDisabled("-");
                }
                else
                {
                    var rank = RankForDisplay(pet, progress);
                    if (rank <= 0)
                    {
                        ImGui.TextDisabled("-");
                    }
                    else
                    {
                        using (PushColor(ImGuiCol.Text, ColorForLevel(rank)))
                        {
                            ImGui.TextUnformatted(rank.ToString());
                        }
                    }

                    if (progress.MaxExp > 0)
                    {
                        var fraction = Math.Clamp(progress.Exp / (float)progress.MaxExp, 0f, 1f);
                        using (PushColor(ImGuiCol.PlotHistogram, GoldColor))
                        {
                            ImGui.ProgressBar(fraction, new Vector2(56, 6), string.Empty);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"EXP {progress.Exp}/{progress.MaxExp}\n(Read when bestiary details were opened)");
                        }
                    }
                }

                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(destination is null ? "-" : DestinationLabel(destination));

                ImGui.TableSetColumnIndex(4);
                ImGui.TextUnformatted($"{pet.Stat1}/{pet.Stat2}/{pet.Stat3}/{pet.Stat4}/{pet.Stat5}");

                ImGui.TableSetColumnIndex(5);
                {
                    using var disabled = ImRaiiDisabled(destination is null || !CanNavigate(destination));
                    if (ImGui.SmallButton($"Move##move{pet.PetRowId}"))
                    {
                        this.MoveTo(pet, destination);
                    }

                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Map##map{pet.PetRowId}"))
                    {
                        this.OpenMap(destination);
                    }
                }

                ImGui.SameLine();
                var marked = this.configuration.MarkedPetRowIds.Contains(pet.PetRowId);
                if (ImGui.SmallButton(marked ? $"Show##pet{pet.PetRowId}" : $"Pet##pet{pet.PetRowId}"))
                {
                    if (marked)
                    {
                        this.configuration.MarkedPetRowIds.Remove(pet.PetRowId);
                    }
                    else
                    {
                        this.configuration.MarkedPetRowIds.Add(pet.PetRowId);
                    }

                    this.saveConfiguration();
                }
            }

            ImGui.EndTable();
        }

        this.DrawSelected(rows[this.selectedIndex]);
    }


    private void DrawSelected(BeastPet pet)
    {
        ImGui.Separator();
        ImGui.TextUnformatted($"{pet.Name}  No:{pet.XbmRowId}  Icon:{pet.IconId}");
        var destination = this.destinations.Find(pet.PetRowId);
        if (destination is not null)
        {
            ImGui.TextWrapped($"Dest: {DestinationLabel(destination)}");
        }

        if (!string.IsNullOrWhiteSpace(pet.Description))
        {
            ImGui.TextWrapped(pet.Description);
        }
    }

    private IEnumerable<BeastPet> FilteredPets()
    {
        var needle = this.filter.Trim();
        return this.beastData.Pets.Where(pet =>
            (!this.configuration.HideCompletedPets || !this.IsCompleted(pet)) &&
            (needle.Length == 0 ||
             pet.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
             pet.Description.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
             pet.XbmRowId.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase)));
    }

    private bool IsCompleted(BeastPet pet)
        => this.configuration.TamedPetRowIds.Contains(pet.PetRowId)
           || this.configuration.MarkedPetRowIds.Contains(pet.PetRowId);

    private void MoveTo(BeastPet pet, BeastDestination? destination)
    {
        if (destination is null || !CanNavigate(destination))
        {
            return;
        }

        // The plugin decides whether this needs a teleport first or just a move
        // in the current zone; either way it runs the same mount + ready gate.
        this.teleport(destination);
    }

    private void OpenMap(BeastDestination? destination)
    {
        if (destination is null || !CanNavigate(destination))
        {
            return;
        }

        this.gameGui.OpenMapWithMapLink(destination.TerritoryId, destination.MapId, destination.Position);
    }

    private static string DestinationLabel(BeastDestination destination)
    {
        if (!string.IsNullOrWhiteSpace(destination.Label))
        {
            return destination.Label;
        }

        if (destination.MapX is not null && destination.MapY is not null)
        {
            return $"Map {destination.MapX:0.0}, {destination.MapY:0.0}";
        }

        return $"T:{destination.TerritoryId} ({destination.X:0.0}, {destination.Y:0.0}, {destination.Z:0.0})";
    }

    private static bool CanNavigate(BeastDestination destination)
        => destination.TerritoryId != 0 && destination.MapId != 0;

    private static IDisposable ImRaiiDisabled(bool disabled)
    {
        if (disabled)
        {
            ImGui.BeginDisabled();
            return new ActionDisposable(ImGui.EndDisabled);
        }

        return ActionDisposable.Empty;
    }

    private sealed class ActionDisposable : IDisposable
    {
        public static readonly ActionDisposable Empty = new(null);
        private readonly Action? action;

        public ActionDisposable(Action? action)
        {
            this.action = action;
        }

        public void Dispose()
            => this.action?.Invoke();
    }
}
