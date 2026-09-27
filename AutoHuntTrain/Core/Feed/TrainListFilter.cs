using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Core.Feed;

// Which announced trains the player looks at, from the view picked on the Train page. The list and the chat line
// follow it; notifications and auto-ride go by the ride rules alone.
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
            TrainListView.MyRegion => Plugin.Instance.Configuration.IsGroupEnabled(announcement.Group) && InHomeRegion(announcement.World),
            _ => RideRules.IsListed(announcement),
        };
    }

    public static bool InHomeRegion(in WorldInfo world) => Worlds.TryHome(out var home) && home.Region == world.Region;
}
