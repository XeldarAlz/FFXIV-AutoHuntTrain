using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Windows.Sections;

// The composed texts are rebuilt only when the mark or the language changes, so drawing them every frame allocates nothing.
internal static class CurrentMark
{
    private static Identity cachedIdentity;
    private static LanguageInfo? cachedLanguage;
    private static string cachedName = string.Empty;
    private static string cachedZoneName = string.Empty;
    private static string cachedLine = string.Empty;
    private static HuntMarkRank? cachedRank;

    // Rank is set only for a mark the registry lists.
    public readonly record struct View(string Name, string ZoneName, string Line, HuntMarkRank? Rank);

    // The flag does not change the texts, so it is left out of the key; a NaN inside it would never compare equal anyway.
    private readonly record struct Identity(uint NameId, uint TerritoryId);

    public static bool TryGet(AutoHuntController controller, out View view)
    {
        var progress = controller.Progress;
        if (!controller.Running || !progress.HasMark)
        {
            view = default;
            return false;
        }

        Compose(progress.Mark);
        view = new View(cachedName, cachedZoneName, cachedLine, cachedRank);
        return true;
    }

    private static void Compose(in TrainMark mark)
    {
        var identity = new Identity(mark.NameId, mark.TerritoryId);
        var language = Loc.Current;
        if (identity == cachedIdentity && ReferenceEquals(language, cachedLanguage))
        {
            return;
        }

        cachedIdentity = identity;
        cachedLanguage = language;
        cachedName = HuntMarkRegistry.NameOf(mark.NameId);
        cachedZoneName = TerritoryNames.Of(mark.TerritoryId);
        cachedRank = HuntMarkRegistry.TryGet(mark.NameId, out var listed) ? listed.Rank : null;
        cachedLine = Loc.T(L.Progress.MarkLine, cachedName, cachedZoneName);
    }
}
