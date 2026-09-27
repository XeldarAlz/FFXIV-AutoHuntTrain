using AutoHuntTrain.Core.Localization;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.ChatMethods;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Tasks;

internal static class RideChat
{
    // The start of a ride is the one line that carries a link, so the Train page is a click away from chat.
    public static void PrintStart(string line)
    {
        var message = new SeStringBuilder()
            .AddText($"{line} ")
            .Add(Plugin.Instance.OpenTrainLink)
            .AddUiForeground($"[{Loc.T(L.Ride.ChatOpenTrain)}]", (ushort)UIColor.LightBlue)
            .Add(RawPayload.LinkTerminator)
            .Build();
        Svc.Chat.Print(message);
    }
}
