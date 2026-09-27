using AutoHuntTrain.Core.Localization;
using Dalamud.Interface;

namespace AutoHuntTrain.Windows.Components;

internal static class StartButton
{
    public static bool Draw(string sublabel, bool enabled, string? disabledReason = null, float width = 0f)
        => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Train.Start), sublabel, Styling.AccentGlow, enabled, disabledReason, width);
}
