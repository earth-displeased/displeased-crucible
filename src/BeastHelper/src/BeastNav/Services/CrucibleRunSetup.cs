using BeastNav.Models;

namespace BeastNav.Services;

public enum CrucibleBoard { FirstBoard, SecondBoard, ThirdBoard, FirstMaster, SecondMaster }

public enum CrucibleRun { Clear, ScoreRun, VilekinScoreRun, BeastkinClear, Legendary, XpRun, Custom }

/// <summary>
/// One place that names every board run and maps it onto the existing profile settings (2026-11-15 UI cleanup: the old
/// window had separate "Prepare ..." buttons, a three-beast checkbox and profile entries mixed into the roster-mode list).
/// Each Apply calls the same preset code as before, so a run behaves exactly as its old button / roster mode did.
/// </summary>
public static class CrucibleRunSetup
{
    public static readonly CrucibleBoard[] Boards =
        [CrucibleBoard.FirstBoard, CrucibleBoard.SecondBoard, CrucibleBoard.ThirdBoard, CrucibleBoard.FirstMaster, CrucibleBoard.SecondMaster];

    public static string BoardName(CrucibleBoard b) => b switch
    {
        CrucibleBoard.FirstBoard => "First Board",
        CrucibleBoard.SecondBoard => "Second Board",
        CrucibleBoard.ThirdBoard => "Third Board",
        CrucibleBoard.FirstMaster => "First Master's Board",
        _ => "Second Master's Board",
    };

    /// <summary>Key used by Configuration.SelectedCrucibleBoard and CrucibleBoards.Capacity.</summary>
    public static string BoardKey(CrucibleBoard b) => b switch
    {
        CrucibleBoard.FirstBoard => "第一盤",
        CrucibleBoard.SecondBoard => "第二盤",
        CrucibleBoard.ThirdBoard => "第三盤",
        CrucibleBoard.FirstMaster => "特一盤",
        _ => "特二盤",
    };

    /// <summary>Territories 1339-1343 in board order.</summary>
    public static uint Territory(CrucibleBoard b) => 1339u + (uint)b;

    public static CrucibleBoard? FromKey(string key)
    {
        foreach (var b in Boards)
            if (BoardKey(b) == key) return b;
        return null;
    }

    public static string RunName(CrucibleRun r) => r switch
    {
        CrucibleRun.Clear => "Clear",
        CrucibleRun.ScoreRun => "Score Run",
        CrucibleRun.VilekinScoreRun => "Score Run - Vilekin combo",
        CrucibleRun.BeastkinClear => "Beastkin Clear",
        CrucibleRun.Legendary => "Legendary",
        CrucibleRun.XpRun => "XP Run",
        _ => "Custom",
    };

    public static string Name(CrucibleBoard b, CrucibleRun r) => $"{BoardName(b)} - {RunName(r)}";

    /// <summary>Runs offered for each board, in menu order.</summary>
    public static CrucibleRun[] Runs(CrucibleBoard b) => b switch
    {
        CrucibleBoard.FirstBoard or CrucibleBoard.SecondBoard =>
            [CrucibleRun.Clear, CrucibleRun.ScoreRun, CrucibleRun.VilekinScoreRun, CrucibleRun.Custom],
        CrucibleBoard.ThirdBoard =>
            [CrucibleRun.Clear, CrucibleRun.BeastkinClear, CrucibleRun.XpRun, CrucibleRun.ScoreRun, CrucibleRun.VilekinScoreRun, CrucibleRun.Custom],
        _ => [CrucibleRun.Clear, CrucibleRun.Legendary, CrucibleRun.XpRun, CrucibleRun.Custom],
    };

    public static string Description(CrucibleBoard b, CrucibleRun r) => r switch
    {
        CrucibleRun.Clear => "Recommended roster (strongest beasts) and whistles, RSR combat, BMR movement, recovery potions below 40%. You enter, pick the difficulty, start battles and move between tiles.",
        CrucibleRun.ScoreRun => b == CrucibleBoard.ThirdBoard
            ? "Exactly three beasts of one family, fixed for the run. Healing and attack items allowed; no rewards, shops, feed or campsites."
            : "Exactly three beasts of one family, fixed for the run. No items, rewards, shops, feed or campsites.",
        CrucibleRun.VilekinScoreRun => "Score Run with Damselfly -> Mantis -> Wespe. Needs the paired RSR build.",
        CrucibleRun.BeastkinClear => "Cu Sith / Buffalo / Coeurl, First Degree. Recovery potions below 60% and attack items. Aim for rank 15.",
        CrucibleRun.Legendary => b == CrucibleBoard.FirstMaster
            ? "Exactly Cu Sith / Behemoth / Coeurl, First Degree. Player potions below 55%, familiar potions below 45%, emergency items, fangs, Thief's and Merchant's Eye."
            : "Exactly Cu Sith / Behemoth / Coeurl (Beast Squad III + Beastkin Gambit). First Master item settings; BMR's Second Master modules switched on.",
        CrucibleRun.XpRun => b switch
        {
            CrucibleBoard.ThirdBoard => "Cu Sith / Buffalo / Coeurl fight; the other 11 places go to your lowest-ranked beasts. Select Standard yourself.",
            CrucibleBoard.FirstMaster => "Cu Sith / Behemoth / Coeurl fight; the other 9 places go to your lowest-ranked beasts. Chests, results and loot are automatic; shops stay manual.",
            _ => "Cu Sith / Behemoth / Coeurl fight with the Legendary settings; the other 12 places go to your lowest-ranked beasts.",
        },
        _ => "Your own roster mode and automation switches (below).",
    };

    /// <summary>
    /// Roster modes offered under Custom. Recommended with combat-only is the Clear run, so it is only listed for
    /// Custom when full automation (combat-only off) is in use.
    /// </summary>
    public static CrucibleTeamMode[] CustomRosterModes(bool combatOnly) => combatOnly
        ? [CrucibleTeamMode.Leveling, CrucibleTeamMode.ClassMinMax, CrucibleTeamMode.Manual]
        : [CrucibleTeamMode.Recommended, CrucibleTeamMode.Leveling, CrucibleTeamMode.ClassMinMax, CrucibleTeamMode.Manual];

    public static string RosterModeName(CrucibleTeamMode m) => m switch
    {
        CrucibleTeamMode.Recommended => "Recommended - strongest beasts",
        CrucibleTeamMode.Leveling => "Leveling - lowest ranks first",
        CrucibleTeamMode.ClassMinMax => "Leveling - lowest-rank trio of one family",
        CrucibleTeamMode.Manual => "Manual - beasts checked in Beast list",
        CrucibleTeamMode.ThirdBoardXpRun => Name(CrucibleBoard.ThirdBoard, CrucibleRun.XpRun),
        CrucibleTeamMode.FirstMasterLegendary => Name(CrucibleBoard.FirstMaster, CrucibleRun.Legendary),
        CrucibleTeamMode.FirstMasterXpRun => Name(CrucibleBoard.FirstMaster, CrucibleRun.XpRun),
        CrucibleTeamMode.SecondMasterLegendary => Name(CrucibleBoard.SecondMaster, CrucibleRun.Legendary),
        CrucibleTeamMode.SecondMasterXpRun => Name(CrucibleBoard.SecondMaster, CrucibleRun.XpRun),
        _ => m.ToString(),
    };

    /// <summary>The run the saved settings currently describe.</summary>
    public static (CrucibleBoard Board, CrucibleRun Run) Detect(Configuration c)
    {
        if (c.FirstBoardScoreEnabled)
        {
            var board = c.ScoreBoardTerritory switch { 1340 => CrucibleBoard.SecondBoard, 1341 => CrucibleBoard.ThirdBoard, _ => CrucibleBoard.FirstBoard };
            if (board == CrucibleBoard.ThirdBoard && c.ThirdBoardBeastkinClear) return (board, CrucibleRun.BeastkinClear);
            return (board, c.ScoreVilekinCombo ? CrucibleRun.VilekinScoreRun : CrucibleRun.ScoreRun);
        }

        switch (c.CrucibleTeamMode)
        {
            case CrucibleTeamMode.ThirdBoardXpRun: return (CrucibleBoard.ThirdBoard, CrucibleRun.XpRun);
            case CrucibleTeamMode.FirstMasterLegendary: return (CrucibleBoard.FirstMaster, CrucibleRun.Legendary);
            case CrucibleTeamMode.FirstMasterXpRun: return (CrucibleBoard.FirstMaster, CrucibleRun.XpRun);
            case CrucibleTeamMode.SecondMasterLegendary: return (CrucibleBoard.SecondMaster, CrucibleRun.Legendary);
            case CrucibleTeamMode.SecondMasterXpRun: return (CrucibleBoard.SecondMaster, CrucibleRun.XpRun);
        }

        var selected = FromKey(c.SelectedCrucibleBoard) ?? CrucibleBoard.FirstBoard;
        var clear = c.CombatOnlyEnabled && c.CrucibleTeamMode == CrucibleTeamMode.Recommended;
        return (selected, clear ? CrucibleRun.Clear : CrucibleRun.Custom);
    }

    /// <summary>
    /// Applies a run's settings. Returns true when BMR dodging should be switched on by the caller (it is an IPC call);
    /// for Second Master Legendary/XP Run the caller also enables BMR's Second Master modules, as the old preset did.
    /// </summary>
    public static bool Apply(Configuration c, CrucibleBoard b, CrucibleRun r)
    {
        switch (r)
        {
            case CrucibleRun.Clear:
                // Old "Prepare Second Master's Board - reliable clears" button, now offered for every board.
                c.FirstBoardScoreEnabled = false;
                c.CombatOnlyEnabled = true;
                c.CombatOnlyBeastSelectionEnabled = true;
                c.AutoCruciblePetPartyEnabled = true;
                c.AutoCrucibleWhistleAssignEnabled = true;
                c.CrucibleTeamMode = CrucibleTeamMode.Recommended;
                c.SelectedCrucibleBoard = BoardKey(b);
                c.AutoCombatEnabled = true;
                c.RecoverStoppedRsrEnabled = true;
                c.ShowAutomationDiagnostics = true;
                c.AutoRecoveryItemEnabled = true;
                c.AutoRecoveryItemHpThreshold = 40;
                return true;

            case CrucibleRun.ScoreRun:
            case CrucibleRun.VilekinScoreRun:
            case CrucibleRun.BeastkinClear:
                // Old three-beast checkbox + board combo, the Vilekin checkbox, and the "Prepare Third Board - Beastkin" button.
                var familyWasPinned = c.ScoreVilekinCombo || c.ThirdBoardBeastkinClear;
                c.FirstBoardScoreEnabled = true;
                c.ScoreBoardTerritory = Territory(b);
                c.ScoreVilekinCombo = r == CrucibleRun.VilekinScoreRun;
                c.ThirdBoardBeastkinClear = r == CrucibleRun.BeastkinClear;
                if (r == CrucibleRun.VilekinScoreRun) c.ScoreFamily = 2;
                else if (r == CrucibleRun.BeastkinClear) c.ScoreFamily = 1;
                else if (familyWasPinned) c.ScoreFamily = 0;
                c.ScoreTeamPetRowIds.Clear();
                if (r == CrucibleRun.BeastkinClear)
                {
                    c.AutoRecoveryItemEnabled = true;
                    c.AutoRecoveryItemHpThreshold = 60f;
                    c.AutoOffensiveItemsEnabled = true;
                }
                c.AutoCombatEnabled = true;
                c.RecoverStoppedRsrEnabled = true;
                c.ShowAutomationDiagnostics = true;
                FirstBoardScorePolicy.Enforce(c);
                return true;

            case CrucibleRun.XpRun when b == CrucibleBoard.ThirdBoard:
                FirstBoardScorePolicy.SelectXpRun(c);
                return true;
            case CrucibleRun.XpRun when b == CrucibleBoard.FirstMaster:
                FirstMasterLegendaryProfile.ApplyXpRun(c);
                return true;
            case CrucibleRun.XpRun when b == CrucibleBoard.SecondMaster:
                SecondMasterLegendaryProfile.ApplyXpRun(c);
                return true;
            case CrucibleRun.Legendary when b == CrucibleBoard.FirstMaster:
                FirstMasterLegendaryProfile.Apply(c);
                return true;
            case CrucibleRun.Legendary when b == CrucibleBoard.SecondMaster:
                SecondMasterLegendaryProfile.Apply(c);
                return true;

            default:
                // Custom: leave the named profiles, keep every automation switch as it is.
                c.FirstBoardScoreEnabled = false;
                if (!CustomRosterModes(c.CombatOnlyEnabled).Contains(c.CrucibleTeamMode)) c.CrucibleTeamMode = CrucibleTeamMode.Leveling;
                c.SelectedCrucibleBoard = BoardKey(b);
                return false;
        }
    }
}
