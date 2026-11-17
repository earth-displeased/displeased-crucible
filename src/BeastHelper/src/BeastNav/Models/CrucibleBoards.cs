namespace BeastNav.Models;

/// <summary>
/// Per-board 闘獣練 facts. The beast-roster capacity differs by board, so a
/// single flat "10" caps every higher board short. These maxima are
/// user-confirmed ground truth (2026-10) and are trusted over the live
/// XBMPetParty capacity AtkValue, whose fixed index has drifted and silently
/// falls back to 10.
/// </summary>
public static class CrucibleBoards
{
    public const int DefaultCapacity = 10;

    public static readonly IReadOnlyDictionary<string, int> Capacity = new Dictionary<string, int>
    {
        ["第一盤"] = 10,
        ["第二盤"] = 12,
        ["第三盤"] = 14,
        ["特一盤"] = 12,
        ["特二盤"] = 15,
    };

    public static bool IsKnown(string board) => Capacity.ContainsKey(board);

    public static int CapacityFor(string board)
        => Capacity.TryGetValue(board, out var capacity) ? capacity : DefaultCapacity;
}
