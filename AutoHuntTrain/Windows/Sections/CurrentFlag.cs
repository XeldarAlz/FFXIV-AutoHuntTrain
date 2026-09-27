using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Windows.Sections;

// The composed line is rebuilt only when the flag or the language changes, so drawing it every frame allocates nothing.
internal static class CurrentFlag
{
    private static Identity cachedIdentity;
    private static LanguageInfo? cachedLanguage;
    private static string cachedZoneName = string.Empty;
    private static string cachedLine = string.Empty;

    public readonly record struct View(string ZoneName, string Line);

    // The posting time tells two flags apart even when they point at the same spot.
    private readonly record struct Identity(long PostedTicks, uint TerritoryId, int Instance);

    public static bool TryGet(AutoHuntController controller, out View view)
    {
        var progress = controller.Progress;
        if (!controller.Running || !progress.HasFlag)
        {
            view = default;
            return false;
        }

        Compose(progress.Flag);
        view = new View(cachedZoneName, cachedLine);
        return true;
    }

    public static string Line(in FlagPost flag)
    {
        Compose(flag);
        return cachedLine;
    }

    private static void Compose(in FlagPost flag)
    {
        var identity = new Identity(flag.PostedAtUtc.Ticks, flag.TerritoryId, flag.Instance);
        var language = Loc.Current;
        if (identity == cachedIdentity && ReferenceEquals(language, cachedLanguage))
        {
            return;
        }

        cachedIdentity = identity;
        cachedLanguage = language;
        cachedZoneName = TerritoryNames.Of(flag.TerritoryId);
        cachedLine = flag.NamesInstance
            ? Loc.T(L.Ride.FlagLineInstance, cachedZoneName, flag.MapX, flag.MapY, flag.Instance)
            : Loc.T(L.Ride.FlagLine, cachedZoneName, flag.MapX, flag.MapY);
    }
}
