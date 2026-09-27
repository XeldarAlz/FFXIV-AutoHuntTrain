using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;

namespace AutoHuntTrain.Windows.Sections;

// The marks credited so far; rebuilt only when the count or the language changes, so drawing it every frame allocates
// nothing.
internal static class CreditedLine
{
    private static CachedText text;

    public static string Get(RideProgress progress)
    {
        var credited = progress.MarksCredited;
        if (text.TryGet(credited, out var line))
        {
            return line;
        }

        return text.Set(credited, Loc.Plural(L.Ride.Credited, credited));
    }
}
