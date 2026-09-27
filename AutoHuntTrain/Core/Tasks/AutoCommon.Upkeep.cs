using AutoHuntTrain.Core.Game.Ops;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

public abstract partial class AutoCommon
{
    private const int UpkeepConsumeWaitMs = 6_000;
    private const int UpkeepConsumeCheckFrames = 100;
    // A failed repair tends to fail the same way straight after (no Dark Matter, no mender), so it rests first.
    private const int UpkeepRepairRetryMs = 600_000;

    private long repairRetryAtMs;

    // Cheap enough to poll: whether RunUpkeep would find anything to do right now.
    protected bool UpkeepDue(bool travelAllowed)
    {
        var configuration = Plugin.Instance.Configuration;
        return UpkeepRepairDue(configuration, travelAllowed) || UpkeepConsumablesDue(configuration);
    }

    // True when any step ran, which can leave the character dismounted or in another zone, so the caller should travel
    // to its next spot afresh. Without travel only the bag's Dark Matter repairs, since a trip to a mender would carry
    // the character away from the train. The upkeep stops between steps once interrupted says so.
    protected async Task<bool> RunUpkeep(bool travelAllowed, Func<bool>? interrupted = null)
    {
        var configuration = Plugin.Instance.Configuration;
        var repairDue = UpkeepRepairDue(configuration, travelAllowed);
        if (!repairDue && !UpkeepConsumablesDue(configuration))
        {
            return false;
        }

        if (!await UpkeepWaitUntilFree())
        {
            return false;
        }

        if (repairDue)
        {
            await RunScheduledRepair(travelAllowed);
        }

        if (!CancelToken.IsCancellationRequested && !UpkeepInterrupted(interrupted) && UpkeepConsumablesDue(configuration))
        {
            await RefreshConsumables(configuration, interrupted);
        }

        return true;
    }

    private bool UpkeepRepairDue(Configuration configuration, bool travelAllowed)
        => configuration.AutoRepair
        && Environment.TickCount64 >= repairRetryAtMs
        && (travelAllowed || CanRepairWithoutTravel(configuration))
        && RepairOps.NeedsRepair(configuration.AutoRepairThresholdPercent);

    private static bool CanRepairWithoutTravel(Configuration configuration)
        => configuration.RepairMode != RepairMode.NpcOnly && RepairOps.HasDarkMatterForAllEquipped();

    private static bool UpkeepConsumablesDue(Configuration configuration)
        => configuration.AutoConsume
        && configuration.AutoConsumeItems.Count > 0
        && FoodOps.AnyNeeded(configuration);

    private static bool UpkeepInterrupted(Func<bool>? interrupted) => interrupted is not null && interrupted();

    private static bool UpkeepCharacterFree()
        => Svc.Objects.LocalPlayer is { IsDead: false }
        && !Svc.Condition[ConditionFlag.InCombat]
        && !Svc.Condition[ConditionFlag.BetweenAreas]
        && !Svc.Condition[ConditionFlag.BetweenAreas51];

    private async Task<bool> UpkeepWaitUntilFree()
    {
        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        await FightOffAttackers("upkeep");
        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (UpkeepCharacterFree())
        {
            return true;
        }

        Diag($"Upkeep: something is due but the character is busy ({ConditionTag()}); trying again later");
        return false;
    }

    private async Task RunScheduledRepair(bool travelAllowed)
    {
        if (await RepairGear(travelAllowed) || CancelToken.IsCancellationRequested)
        {
            return;
        }

        repairRetryAtMs = Environment.TickCount64 + UpkeepRepairRetryMs;
        Warn($"Upkeep: the repair did not finish; the next try waits {UpkeepRepairRetryMs / TimeUnits.MillisecondsPerMinute} minutes");
    }

    // Each item gets a wall-clock deadline, so a use the game never applies cannot park the run.
    private async Task RefreshConsumables(Configuration configuration, Func<bool>? interrupted)
    {
        if (Svc.Condition[ConditionFlag.Mounted])
        {
            await SafeDismount("upkeep-dismount-consume");
        }

        if (Svc.Condition[ConditionFlag.Mounted] || Svc.Condition[ConditionFlag.InCombat])
        {
            Diag($"Upkeep: cannot eat right now ({ConditionTag()}); trying again later");
            return;
        }

        var minimumSeconds = FoodOps.MinimumBuffSeconds(configuration);
        var items = configuration.AutoConsumeItems;
        for (var index = 0; index < items.Count && !CancelToken.IsCancellationRequested && !UpkeepInterrupted(interrupted); index++)
        {
            var entry = items[index];
            if (FoodOps.HasStatus(entry.StatusId, minimumSeconds) || !FoodOps.IsAvailable(entry))
            {
                continue;
            }

            Status = $"Consuming {entry.Name}";
            Diag($"Upkeep: consuming {entry.Name} (status {entry.StatusId} missing or under {configuration.AutoConsumeMinMinutes}m)");
            await WaitUntilTimed(() => ConsumeUntilBuffed(entry, minimumSeconds), UpkeepConsumeWaitMs, $"consume-{entry.ItemId}", UpkeepConsumeCheckFrames);
        }
    }

    // Stops once the buff shows, or when combat starts, since eating is refused then and the next upkeep tries again.
    private static bool ConsumeUntilBuffed(ConsumableEntry entry, float minimumSeconds)
    {
        if (FoodOps.HasStatus(entry.StatusId, minimumSeconds) || Svc.Condition[ConditionFlag.InCombat])
        {
            return true;
        }

        FoodOps.UseConsumable(entry);
        return false;
    }
}
