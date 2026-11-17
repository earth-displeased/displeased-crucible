using BeastNav.Models;

namespace BeastNav.Services;

/// <summary>
/// Picks a "recommended" 闘獣練 team: the owned beasts most likely to survive a
/// farming run, rather than the lowest-ranked ones the Leveling mode favors.
/// </summary>
/// <remarks>
/// The score is deliberately level-dominant and self-contained — it reads only
/// data BeastHelper already holds with confidence (capture state, the XBMPet
/// sheet's static tier/stats, and whatever per-beast level has been synced into
/// <see cref="Configuration.BeastLevelProgress"/>). It never needs a fresh rank
/// read to produce a sensible answer: when a beast's current level is unknown
/// the static tier stands in for it, so the recommendation degrades gracefully
/// to "use your strongest beasts" instead of breaking — which matters while the
/// rank sync is still imperfect.
///
/// Weighting: current level (1-25) dominates because a high-level beast is by
/// far the strongest stability signal; the static tier is next; the beast's
/// defensive stats (physical resistance + constitution + magical resistance)
/// then its offensive stats act only as minor tiebreakers, so an error in the
/// sheet's stat columns can shuffle near-ties but never overturns the level/
/// tier ordering.
/// </remarks>
public static class CrucibleTeamPlanner
{
    public static IReadOnlyList<BeastPet> ThirdBoardXpRun(IEnumerable<BeastPet> ownedPets,
        IReadOnlyDictionary<uint,BeastProgress> progress,out string reason)
        => XpRoster(ownedPets,progress,[1,26,33],14,"Cu Sith, Buffalo and Coeurl",out reason,allowUnknownRanks:true);

    public static IReadOnlyList<BeastPet> FirstMasterXpRun(IEnumerable<BeastPet> ownedPets,
        IReadOnlyDictionary<uint,BeastProgress> progress,out string reason)
        => XpRoster(ownedPets,progress,[1,50,33],12,"Cu Sith, Behemoth and Coeurl",out reason,allowUnknownRanks:true);

    public static IReadOnlyList<BeastPet> SecondMasterXpRun(IEnumerable<BeastPet> ownedPets,
        IReadOnlyDictionary<uint,BeastProgress> progress,out string reason)
        => XpRoster(ownedPets,progress,[1,50,33],15,"Cu Sith, Behemoth and Coeurl",out reason,allowUnknownRanks:true); // = SecondMasterLegendaryProfile.XpRosterCapacity

    /// <summary>Rank used for ordering; null when no valid (1-25) rank is synced.</summary>
    public static int? KnownRank(IReadOnlyDictionary<uint,BeastProgress> progress,BeastPet pet)
        => progress.TryGetValue(pet.PetRowId,out var rank) && rank.Level is >=1 and <=25 ? rank.Level : null;

    private static IReadOnlyList<BeastPet> XpRoster(IEnumerable<BeastPet> ownedPets,
        IReadOnlyDictionary<uint,BeastProgress> progress,uint[] core,int capacity,string coreNames,out string reason,
        bool allowUnknownRanks=false)
    {
        var owned=ownedPets.DistinctBy(p=>p.PetRowId).ToArray();
        var main=core.Select(no=>owned.FirstOrDefault(p=>p.XbmRowId==no)).OfType<BeastPet>().ToArray();
        if(main.Length!=3) { reason=$"Capture {coreNames} before preparing an XP roster.";return []; }
        var extras=owned.Where(p=>!core.Contains(p.XbmRowId)).ToArray();
        var missing=extras.Count(p=>KnownRank(progress,p) is null);
        if(missing>0 && !allowUnknownRanks) { reason=$"Waiting for synced ranks for {missing} XP candidate(s). Open the bestiary to scan.";return []; }
        // Second Master XP run (2026-11-14): the bestiary scan left 46 of 47 candidates at rank 0
        // (config read; dalamud.log 15:05:54 "Waiting for synced ranks for 46"), so the roster never
        // filled. Unknown ranks are ordered as rank 1 (never-used captures), after known rank-1 beasts.
        // First Master XP run hit the same block right after (user report, 0.2.41), so all three XP runs allow it.
        reason=string.Empty;
        return main.Concat(extras.OrderBy(p=>KnownRank(progress,p) ?? 1).ThenBy(p=>KnownRank(progress,p) is null)
            .ThenBy(p=>p.XbmRowId).Take(capacity-core.Length)).ToArray();
    }

    public static IReadOnlyList<BeastPet> ClassMinMax(
        IEnumerable<BeastPet> ownedPets, IReadOnlyDictionary<uint, BeastProgress> progress,
        out string reason)
    {
        var owned = ownedPets.DistinctBy(p => p.PetRowId).ToArray();
        reason = string.Empty;
        if (owned.Length == 0) { reason = "No captured beasts available."; return []; }
        var unknown = owned.Count(p => p.Classification is < 1 or > 8
            || !progress.TryGetValue(p.PetRowId, out var rank) || rank.Level is < 1 or > 25);
        if (unknown > 0)
        {
            reason = $"Waiting for synced ranks / classification for {unknown} captured beast(s).";
            return [];
        }

        var result = owned.GroupBy(p => p.Classification)
            .Where(g => g.Count() >= 3)
            .Select(g => g.OrderBy(p => progress[p.PetRowId].Level).ThenBy(p => p.XbmRowId).Take(3).ToArray())
            .OrderBy(trio => trio.Sum(p => progress[p.PetRowId].Level))
            .ThenBy(trio => trio.Max(p => progress[p.PetRowId].Level))
            .ThenBy(trio => trio[0].Classification)
            .FirstOrDefault();
        if (result is null) reason = "No classification has three captured beasts.";
        return result ?? [];
    }

    /// <summary>The recommended team, strongest first, capped at <paramref name="capacity"/>.</summary>
    public static IReadOnlyList<BeastPet> Recommend(
        IEnumerable<BeastPet> ownedPets,
        IReadOnlyDictionary<uint, BeastProgress> progress,
        int capacity)
        => ownedPets
            .OrderByDescending(pet => Score(pet, progress))
            .ThenBy(pet => pet.XbmRowId)
            .Take(Math.Max(0, capacity))
            .ToList();

    /// <summary>Stability score for a single beast. Higher is more stable.</summary>
    public static double Score(BeastPet pet, IReadOnlyDictionary<uint, BeastProgress> progress)
    {
        var level = progress.TryGetValue(pet.PetRowId, out var p) && p.Level is >= 1 and <= 25
            ? p.Level
            : pet.Rank; // no synced level yet — fall back to the static tier.

        // Stat1..5 follow the detail panel's own order: Strength, Physical
        // Resistance, Constitution, Intelligence, Magical Resistance.
        var defense = pet.Stat2 + pet.Stat3 + pet.Stat5;
        var offense = pet.Stat1 + pet.Stat4;

        return (level * 10.0) + (pet.Rank * 3.0) + (defense * 0.5) + (offense * 0.25);
    }
}
