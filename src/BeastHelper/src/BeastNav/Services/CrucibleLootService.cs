using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastNav.Services;

/// <summary>
/// Auto-clicks "すべて入手" (claim all) on the post-battle Crucible loot
/// screen (<c>XBMContentsBooty</c>, node id 46 — a Button Component Node),
/// then confirms the "を全て入手します。よろしいですか?" Yes/No prompt that
/// follows it by clicking the shared <c>SelectYesno</c> addon's own
/// <c>YesButton</c> field directly.
/// </summary>
/// <remarks>
/// The claim button is its own dedicated addon (XBMContentsBooty) that only
/// ever appears for this one purpose, so it's safe to click directly. The
/// Yes/No prompt afterward is the game's generic, shared SelectYesno addon —
/// used for hundreds of unrelated confirmations — so it's only ever confirmed
    /// when its prompt text matches a supported loot confirmation.
/// </remarks>
/// <remarks>
/// Unattended UI interaction — against the FFXIV ToS, self-responsibility.
/// Off by default. Never picks which items to keep individually.
/// </remarks>
public sealed unsafe class CrucibleLootService
{
    private const string LootAddonName = "XBMContentsBooty";
    private const uint ClaimAllNodeId = 46;
    private const string ConfirmAddonName = "SelectYesno";
    private static readonly TimeSpan ClaimThrottle = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ConfirmThrottle = TimeSpan.FromSeconds(1.5);

    private readonly Configuration configuration;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private DateTime nextClaimAttempt = DateTime.MinValue;
    private DateTime nextConfirmAttempt = DateTime.MinValue;

    // The confirm is only answered right after this service clicked its own
    // button, so a SelectYesno with similar text from anything else is never touched.
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);
    private DateTime lastOwnClick = DateTime.MinValue;

    public CrucibleLootService(Configuration configuration, IGameGui gameGui, IPluginLog log)
    {
        this.configuration = configuration;
        this.gameGui = gameGui;
        this.log = log;
    }

    /// <summary>Short human status for the main window.</summary>
    public string Status { get; private set; } = "off";

    public void Tick()
    {
        if (!FirstMasterLegendaryProfile.MenusAllowed(this.configuration))
        {
            this.Status = "off";
            return;
        }

        if (this.TryConfirmClaim())
        {
            return;
        }

        this.TryClickClaimAll();
    }

    private void TryClickClaimAll()
    {
        var now = DateTime.UtcNow;
        if (now < this.nextClaimAttempt)
        {
            return;
        }

        AtkUnitBase* addon;
        try
        {
            var ptr = this.gameGui.GetAddonByName(LootAddonName);
            addon = ptr.IsNull ? null : (AtkUnitBase*)ptr.Address;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "[BeastHelper] Crucible loot: addon lookup failed.");
            return;
        }

        if (addon is null || !addon->IsVisible)
        {
            this.Status = "Waiting for loot screen";
            return;
        }

        var node = addon->GetNodeById(ClaimAllNodeId);
        if (node is null)
        {
            this.Status = "Button not found";
            return;
        }

        // Not node->Type != NodeType.Component: the raw Type field on a
        // component-backed node holds an internal component-kind value
        // (e.g. 1002 for a button), not the normalized NodeType.Component
        // (10000) that only GetNodeType() returns — comparing against that
        // directly always failed. GetComponent() sidesteps it entirely.
        var button = (AtkComponentButton*)node->GetComponent();
        if (button is null)
        {
            this.Status = "Button not found";
            return;
        }

        if (!this.TryClick(addon, &button->AtkComponentBase))
        {
            this.Status = "Click failed";
            return;
        }

        this.nextClaimAttempt = now + ClaimThrottle;
        this.lastOwnClick = now;
        this.Status = "Clicked Claim all";
        this.log.Information("[BeastHelper] Crucible loot: clicked すべて入手.");
    }

    /// <summary>
    /// Returns true (and consumes the tick) when the shared SelectYesno addon
    /// is open with our claim-confirmation text — regardless of whether it
    /// actually clicked yet, so an unrelated SelectYesno never falls through
    /// into the claim-all click above and races it.
    /// </summary>
    private bool TryConfirmClaim()
    {
        if (DateTime.UtcNow - this.lastOwnClick > ConfirmWindow)
        {
            return false;
        }

        AtkUnitBase* addon;
        try
        {
            var ptr = this.gameGui.GetAddonByName(ConfirmAddonName);
            addon = ptr.IsNull ? null : (AtkUnitBase*)ptr.Address;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "[BeastHelper] Crucible loot: SelectYesno lookup failed.");
            return false;
        }

        if (addon is null || !addon->IsVisible)
        {
            return false;
        }

        var selectYesno = (AddonSelectYesno*)addon;
        var promptText = selectYesno->PromptText is not null ? selectYesno->PromptText->NodeText.ToString() : string.Empty;
        var lootPtr = this.gameGui.GetAddonByName(LootAddonName);
        if (lootPtr.IsNull || !((AtkUnitBase*)lootPtr.Address)->IsVisible)
        {
            return false;
        }

        if (!CrucibleConfirmationText.IsLootClaim(promptText))
        {
            // Do not click the underlying loot screen while an unrelated modal is open.
            this.Status = "Waiting: unrecognized confirmation (answer manually)";
            return true;
        }

        var now = DateTime.UtcNow;
        if (now < this.nextConfirmAttempt)
        {
            this.Status = "Waiting for confirmation";
            return true;
        }

        var yesButton = selectYesno->YesButton;
        if (yesButton is null)
        {
            this.Status = "Yes button not found";
            return true;
        }

        this.nextConfirmAttempt = now + ConfirmThrottle;
        if (this.TryClick(addon, &yesButton->AtkComponentBase))
        {
            this.Status = "Confirmed claim (Yes)";
            this.log.Information("[BeastHelper] Crucible loot: clicked はい to confirm the claim.");
        }
        else
        {
            this.Status = "Click failed";
        }

        return true;
    }

    private bool TryClick(AtkUnitBase* addon, AtkComponentBase* component)
    {
        var ownerNode = component->OwnerNode;
        if (ownerNode is null)
        {
            return false;
        }

        var evt = ownerNode->AtkResNode.AtkEventManager.Event;
        if (evt is null)
        {
            return false;
        }

        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
        return true;
    }
}
