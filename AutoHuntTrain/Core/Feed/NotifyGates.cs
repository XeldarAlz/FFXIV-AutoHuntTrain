using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Feed;

// What keeps every train notification quiet whatever the train: a snooze, a ride under way, a duty or a cutscene.
internal static class NotifyGates
{
    public static bool TryBlock(Configuration configuration, out string reason)
    {
        if (configuration.IsSnoozed(DateTime.UtcNow))
        {
            reason = "auto-ride is snoozed";
            return true;
        }

        if (Plugin.Instance.Controller.Running)
        {
            reason = "a ride is running";
            return true;
        }

        if (InDutyOrCutscene())
        {
            reason = "the character is in a duty or a cutscene";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool InDutyOrCutscene()
        => Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        || Svc.Condition[ConditionFlag.WatchingCutscene]
        || Svc.Condition[ConditionFlag.WatchingCutscene78]
        || Svc.Condition[ConditionFlag.BoundByDuty]
        || Svc.Condition[ConditionFlag.BoundByDuty56]
        || Svc.Condition[ConditionFlag.BoundByDuty95];
}
