using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;

namespace AutoHuntTrain.Windows.Sections;

// The marks credited against the expansion's count once the ride knows it, the plain count before; rebuilt only when
// a number or the language changes, so drawing it every frame allocates nothing.
internal static class CreditedLine
{
    private static CachedText text;

    public static string Get(RideProgress progress)
    {
        var credited = progress.MarksCredited;
        var expected = progress.ExpectedMarks;
        var key = ((long)expected << 32) | (uint)credited;
        if (text.TryGet(key, out var line))
        {
            return line;
        }

        return text.Set(key, expected > 0 ? Loc.T(L.Ride.CreditedOf, credited, expected) : Loc.Plural(L.Run.MarksCredited, credited));
    }
}
