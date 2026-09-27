using ECommons;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHuntTrain.Core.Game;

// Ported from HuntAlerts' Utilities.OpenPartyFinder: open the Party Finder when it is closed, then switch it to the
// Hunts category once the window is up. Driven from the framework tick, one step per frame, for five seconds at most.
internal sealed class PartyFinderOpener
{
    private const string AddonName = "LookingForGroup";
    private const string OpenCommand = "/partyfinder";
    private const string ThrottleKey = "aht_open_party_finder";
    private const int ThrottleMs = 1_000;
    private const int OpenSettleMs = 500;
    private const int TimeLimitMs = 5_000;
    // Node 35 of the Party Finder is the Hunts category tab. Inside its component, node 2 is fully opaque while the
    // category can be picked and node 4's image shows part 0 while it is the picked one.
    private const int HuntsTabNodeIndex = 35;
    private const int TabEnabledNodeIndex = 2;
    private const int TabSelectedNodeIndex = 4;
    private const byte OpaqueAlpha = 255;
    private const ushort SelectedPartId = 0;
    // Callback 21 picks a category; 11 is Hunts.
    private const int PickCategoryCallback = 21;
    private const int HuntsCategory = 11;

    private bool active;
    private long selectAfterMs;
    private long deadlineMs;

    public void Request()
    {
        if (!EzThrottler.Throttle(ThrottleKey, ThrottleMs))
        {
            return;
        }

        var now = Environment.TickCount64;
        selectAfterMs = now;
        if (!IsOpen())
        {
            try
            {
                Chat.SendMessage(OpenCommand);
            }
            catch (Exception exception)
            {
                RunLog.Warning(exception, "Party Finder: the open command could not be sent");
                return;
            }

            selectAfterMs = now + OpenSettleMs;
        }

        deadlineMs = now + TimeLimitMs;
        active = true;
    }

    public unsafe void Tick()
    {
        if (!active)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now > deadlineMs)
        {
            active = false;
            RunLog.Debug("Party Finder: the Hunts category could not be picked within five seconds");
            return;
        }

        if (now < selectAfterMs || !GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) || !GenericHelpers.IsAddonReady(addon))
        {
            return;
        }

        if (TryPickHunts(addon))
        {
            active = false;
        }
    }

    private static unsafe bool IsOpen() => GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out _);

    private static unsafe bool TryPickHunts(AtkUnitBase* addon)
    {
        if (addon->UldManager.NodeListCount <= HuntsTabNodeIndex)
        {
            return false;
        }

        var tab = addon->UldManager.NodeList[HuntsTabNodeIndex]->GetAsAtkComponentNode();
        if (tab is null || tab->Component is null)
        {
            return false;
        }

        var tabNodes = tab->Component->UldManager;
        if (tabNodes.NodeListCount <= TabSelectedNodeIndex || tabNodes.NodeList[TabEnabledNodeIndex]->Alpha_2 != OpaqueAlpha)
        {
            return false;
        }

        var marker = tabNodes.NodeList[TabSelectedNodeIndex]->GetAsAtkImageNode();
        if (marker is not null && marker->PartId == SelectedPartId)
        {
            return true;
        }

        RunLog.Debug("Party Finder: picking the Hunts category");
        Callback.Fire(addon, true, PickCategoryCallback, HuntsCategory, Callback.ZeroAtkValue);
        return true;
    }
}
