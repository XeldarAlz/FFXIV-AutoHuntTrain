using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// The way home after a ride, the first link of the after-run chain; a transfer home that the relog cuts is picked
// up at login as a bare journey, since no ride stands behind it.
internal sealed class AutoWayHome : AutoCommon
{
    private const string Scope = "way-home";

    protected override async Task Execute()
    {
        await HoldCombatMovementAndSettle(Scope);
        try
        {
            Status = "Travelling home";
            var outcome = await TravelHome();
            if (CancelToken.IsCancellationRequested)
            {
                Diag("Way home: cancelled.");
                return;
            }

            if (outcome != JourneyOutcome.Arrived)
            {
                Warn($"Way home: ended with {outcome}; the character stays where it is");
                return;
            }

            var home = Worlds.TryHome(out var world) ? world.Name : "the home world";
            Svc.Chat.Print($"{AhtConstants.LogPrefix} Back home on {home}.");
        }
        finally
        {
            ReleaseCombatMovement(Scope);
        }
    }
}
