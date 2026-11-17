using BeastNav.Services;

var cases = new (string Prompt, bool Loot, bool Shop)[]
{
    // Exact wording transcribed from the reported English loot confirmation.
    ("You will receive:\n· 1,200 Territory Tokens\n· Fang of Wind\n· Chemist's Satchel\n· Temporal Sand\n· Crucible Eye Drops\nProceed?", true, false),
    ("  You will receive:\r\nFang of Wind\r\nProceed?  ", true, false),
    ("アイテムを全て入手します。よろしいですか?", true, false),
    ("Purchase this item for 100 territory tokens?", false, true),
    ("Finish purchasing items?", false, true),
    ("アイテム購入を終了してよろしいですか?", false, true),
    ("Proceed?", false, false),
    ("You will receive:", false, false),
    ("Discard this item?", false, false),
    ("Leave the duty?", false, false),
    ("Accept this teleport?", false, false),
    ("", false, false),
};
foreach (var (prompt, loot, shop) in cases)
{
    if (CrucibleConfirmationText.IsLootClaim(prompt) != loot
        || CrucibleConfirmationText.IsShopAction(prompt) != shop)
        throw new Exception($"Unexpected confirmation classification: {prompt}");
}
Console.WriteLine($"Passed {cases.Length} prompt recognition cases.");
