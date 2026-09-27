using AutoHuntTrain.Core.Localization;
using Dalamud.Interface;

namespace AutoHuntTrain.Windows.Components;

internal static class StopButton
{
    public static bool Draw(string? sublabel, float width = 0f)
        => HeroButton.Draw(FontAwesomeIcon.Stop, Loc.T(L.Hunt.Stop), sublabel, Styling.AccentRose, true, null, width);
}
