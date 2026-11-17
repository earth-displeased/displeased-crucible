namespace BeastNav.Services;

/// <summary>
/// Ranks Crucible beast gear (XBMItem rows 1-75) for the First Master XP run: damage mitigation first,
/// then damage increase (user request 2026-11-14).
/// </summary>
/// <remarks>
/// Scores are read off the XBMItem sheet effect text (English client, extracted with Lumina 2026-11-14):
/// Mitigation = Damage Taken -x as x, Physical Vulnerability -x as 0.6x, Magic Vulnerability -x as 0.4x,
/// Maximum HP +x as 0.5x, Evasion +x as 0.5x, block-only and proc effects as small flat values.
/// Damage = Damage Dealt +x as x, Physical Damage Dealt +x as 0.9x, Magic Damage Dealt +x as 0.3x,
/// Critical Hit Rate +x as 0.5x, Haste +x as 0.8x, Rapid Recast +x as 0.2x, auto-attack/counter procs flat.
/// Gear with a self-harming drawback (Thunder Axe self-Stun, Flame Knife self-Chains, Demonic Helm self-Doom)
/// and Coward's Knife (skips battles, forfeiting loot and XP) are never chosen.
/// Gear with Mitigation of at least 3 is the mitigation tier, ordered by Mitigation then Damage; the rest
/// is ordered by Damage then Mitigation; gear scoring below 3 on both is not worth buying.
/// </remarks>
public static class CrucibleGearPolicy
{
    public readonly record struct Gear(uint Row, string Name, double Mitigation, double Damage, bool Excluded = false);

    public const double MitigationTier = 3;
    public const double Worthwhile = 3;

    public static readonly IReadOnlyDictionary<uint, Gear> Table = new Gear[]
    {
        new(1, "Belt of Constitution", 10, 0),
        new(2, "Ring of Sacrifice", 12, 0),
        new(3, "Ring of Curing", 8, 0),
        new(4, "Chemist's Satchel", 4, 0),
        new(5, "Hexed Hat", 4, 0),
        new(6, "Steel Armor", 9, 0),
        new(7, "Crown of the Wild", 0, 12),
        new(8, "Staff of the Wise", 0, 6),
        new(9, "Power Armlet", 1.5, 11),
        new(10, "Twisted Headband", 3, 4.5),
        new(11, "Green Beret", 3.5, 6),
        new(12, "Mystic Boots", 6, 0),
        new(13, "Ring of Protection", 13.5, 0),
        new(14, "Ice Shield", 30, -5),
        new(15, "Earth Shield", 3, 0),
        new(16, "Water Shield", 3, 0),
        new(17, "Force Shield", 6, 0),
        new(18, "Crimson Ribbon", 5, 0),
        new(19, "Genji Gloves", 1, 12),
        new(20, "Genji Greatshield", 6.5, 0),
        new(21, "Genji Armor", 35, 0),
        new(22, "Silver Specs", 0, 7.5),
        new(23, "Angel Robe", 10, 0),
        new(24, "Master Shield", 48, 0),
        new(25, "Mystic Veil", 8, 5),
        new(26, "Black Cowl", 7, 0),
        new(27, "Hero's Crown", 17, 20),
        new(28, "Mirage Vest", 6, 0),
        new(29, "Soulreaper's Armor", 10, 0),
        new(30, "Briar Armor", 0, 4),
        new(31, "Coward's Knife", 0, 1, Excluded: true),
        new(32, "Umbral Mantle", 6, 2),
        new(33, "Umbral Band", 5.4, 1),
        new(34, "Umbral Wristlet", 8.5, 0),
        new(35, "Astral Mantle", 14.5, 5),
        new(36, "Astral Band", 4.8, 3),
        new(37, "Astral Wristlet", 3.2, 3),
        new(38, "Flame-wreathed Axe", 0, 6),
        new(39, "Icebitten Axe", 0, 6),
        new(40, "Thunderstruck Axe", 0, 6),
        new(41, "Earthcrushed Axe", 0, 6),
        new(42, "Deepdrowned Axe", 0, 6),
        new(43, "Windblown Axe", 0, 6),
        new(44, "Ninja Suit", 12.5, 4),
        new(45, "Ninja Gloves", 0, 4.5),
        new(46, "Ninja Eyepatch", 0, 6.5),
        new(47, "Wind Shield", 5, 0),
        new(48, "Wind Mask", 5, 5),
        new(49, "Wind Armor", 20, 6),
        new(50, "Heavy Axe", 0, 23),
        new(51, "Mythril Armor", 22.5, 0),
        new(52, "Mythril Gloves", 7.5, 4.5),
        new(53, "Mythril Greaves", 12.5, 0),
        new(54, "Beast Mask", 6, 7.5),
        new(55, "Beastly Knife", 0, 8),
        new(56, "Beast Earring", 1, 7.5),
        new(57, "Thief's Knife", 0, 1),
        new(58, "Thief's Garb", 17, 0),
        new(59, "Thief's Gloves", 0, 1.6),
        new(60, "Thief's Boots", 1, 0),
        new(61, "Thunder Axe", 0, 30, Excluded: true),
        new(62, "Thunder Armor", 3, 4),
        new(63, "Warded Shield", 3, 0),
        new(64, "Gold Hairpin", 0, 3),
        new(65, "Flame Knife", 0, 28, Excluded: true),
        new(66, "Flame Shield", 3, 3),
        new(67, "Merchant's Cap", 0, 0),
        new(68, "Merchant's Garb", 6, 0),
        new(69, "Merchant's Shoes", 8, 0),
        new(70, "Spirited Ring", 1.5, 3),
        new(71, "Demonic Helm", 0, 24, Excluded: true),
        new(72, "Demonic Armor", 5.5, 6),
        new(73, "Empress Hairpin", 1, 0),
        new(74, "Haste Belt", 0, 5),
        new(75, "Warrior's Buckler", 6, 0),
    }.ToDictionary(g => g.Row);

    public static bool IsGear(uint row) => Table.ContainsKey(row);

    /// <summary>Worth taking at all: known, not excluded, and useful for mitigation or damage.</summary>
    public static bool Wanted(uint row)
        => Table.TryGetValue(row, out var g) && !g.Excluded && (g.Mitigation >= Worthwhile || g.Damage >= Worthwhile);

    /// <summary>Sort key, larger is better: mitigation tier first, then by mitigation/damage.</summary>
    public static (int Tier, double First, double Second) Rank(uint row)
    {
        if (!Table.TryGetValue(row, out var g) || g.Excluded) return (-1, 0, 0);
        return g.Mitigation >= MitigationTier ? (1, g.Mitigation, g.Damage) : (0, g.Damage, g.Mitigation);
    }

    /// <summary>The best wanted gear among the offered rows, or null.</summary>
    public static uint? Best(IEnumerable<uint> rows)
        => rows.Where(Wanted).OrderByDescending(Rank).ThenBy(r => r).Select(r => (uint?)r).FirstOrDefault();

    /// <summary>Finds the gear named in a treasure card's text (longest name wins, so "Thunder Armor" never reads as "Thunder Axe").</summary>
    public static uint? FromCardText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var g in Table.Values.OrderByDescending(g => g.Name.Length))
            if (text.Contains(g.Name, StringComparison.OrdinalIgnoreCase))
                return g.Row;
        return null;
    }
}
