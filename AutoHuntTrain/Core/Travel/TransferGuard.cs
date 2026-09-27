using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Travel;

// What keeps a character from a data center transfer it should not make on its own: company it would leave behind,
// or a window it would tear down mid use. A reason reads as the end of "the transfer was not started: ...".
internal static class TransferGuard
{
    private static readonly string[] blockingAddons =
    [
        "RetainerList",
        "RetainerSellList",
        "InventoryRetainer",
        "InventoryRetainerLarge",
        "ItemSearch",
        "ItemSearchResult",
        "SelectYesno",
        "Trade",
    ];

    public static string? Blocker()
    {
        if (Svc.Party.Length > 1 || Svc.Condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance])
        {
            return "the character is in a party";
        }

        if (Svc.Condition[ConditionFlag.BoundByDuty] || Svc.Condition[ConditionFlag.BoundByDuty56] || Svc.Condition[ConditionFlag.BoundByDuty95] || Svc.Condition[ConditionFlag.InDutyQueue])
        {
            return "the character is in a duty or a duty queue";
        }

        if (Svc.Condition[ConditionFlag.TradeOpen])
        {
            return "a trade is open";
        }

        for (var index = 0; index < blockingAddons.Length; index++)
        {
            var addon = Svc.GameGui.GetAddonByName(blockingAddons[index]);
            if (!addon.IsNull && addon.IsVisible)
            {
                return $"the {blockingAddons[index]} window is open";
            }
        }

        return null;
    }
}
