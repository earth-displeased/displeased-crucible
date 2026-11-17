namespace BeastNav.Services;

/// <summary>Prompt recognition only. Callers must also check their own action and screen context.</summary>
internal static class CrucibleConfirmationText
{
    public static bool IsLootClaim(string prompt)
    {
        var text = prompt.Trim();
        return text.Contains("入手します", StringComparison.Ordinal)
            || (text.StartsWith("You will receive:", StringComparison.OrdinalIgnoreCase)
                && text.EndsWith("Proceed?", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A number the shop shows as text: "1,500" -> 1500, "52 (-50%)" -> 52; unreadable -> 0.</summary>
    public static uint ParseShownNumber(string text)
    {
        var digits = new string(text.Trim().TakeWhile(ch => char.IsDigit(ch) || ch == ',' || ch == '.').Where(char.IsDigit).ToArray());
        return uint.TryParse(digits, out var n) ? n : 0;
    }

    public static bool IsShopAction(string prompt)
    {
        var text = prompt.Trim();
        // English leave prompt (windowdump 2026-11-14 18:23): "Conclude purchasing and leave the shop?"
        // followed by "You cannot return to the shop after leaving." - so the "?" is not at the end.
        return text.Contains("アイテム購入を終了", StringComparison.Ordinal)
            || text.Contains("購入", StringComparison.Ordinal)
            || text.Contains("leave the shop", StringComparison.OrdinalIgnoreCase)
            || (text.Contains('?')
                && (text.Contains("purchase", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("purchasing", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("buy ", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("shopping", StringComparison.OrdinalIgnoreCase)));
    }
}
