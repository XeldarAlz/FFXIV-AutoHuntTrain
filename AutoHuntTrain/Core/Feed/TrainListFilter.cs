using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Core.Feed;

// Which announced trains the player looks at, from the view picked on the Train page: geography only, never the
// auto-join switches. The list and the chat line follow it; the other notifications and auto-join do not.
internal static class TrainListFilter
{
    public static bool Shows(in Announcement announcement, TrainListView view)
    {
        if (announcement.World.Id == 0)
        {
            return false;
        }

        return view switch
        {
            TrainListView.Everywhere => true,
            TrainListView.MyRegion => InHomeRegion(announcement.World),
            _ => RideRules.IsAllowedDataCenter(announcement.World),
        };
    }

    public static bool InHomeRegion(in WorldInfo world) => Worlds.TryHome(out var home) && home.Region == world.Region;
}
