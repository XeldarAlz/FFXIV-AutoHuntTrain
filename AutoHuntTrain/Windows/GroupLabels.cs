using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using System.Numerics;

namespace AutoHuntTrain.Windows;

internal static class GroupLabels
{
    public static string Name(ExpansionGroup group) => group switch
    {
        ExpansionGroup.Centurio => Loc.T(L.Feed.BadgeCenturio),
        ExpansionGroup.Shadowbringers => Loc.T(L.Feed.BadgeShadowbringers),
        ExpansionGroup.Endwalker => Loc.T(L.Feed.BadgeEndwalker),
        _ => Loc.T(L.Feed.BadgeDawntrail),
    };

    public static Vector4 Color(ExpansionGroup group) => group switch
    {
        ExpansionGroup.Centurio => Styling.AccentAmber,
        ExpansionGroup.Shadowbringers => Styling.AccentNebula,
        ExpansionGroup.Endwalker => Styling.AccentBlue,
        _ => Styling.AccentMint,
    };
}
