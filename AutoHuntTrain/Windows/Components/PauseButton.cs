using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;
using Dalamud.Interface;

namespace AutoHuntTrain.Windows.Components;

internal static class PauseButton
{
    public static bool Draw(PauseReason reason, float width = 0f) => reason switch
    {
        PauseReason.InContent => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Train.ResumeCaps), null, Styling.AccentMint, false, Loc.T(L.Train.InContent), width),
        PauseReason.Manual    => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Train.ResumeCaps), null, Styling.AccentMint, true, null, width),
        _                     => HeroButton.Draw(FontAwesomeIcon.Pause, Loc.T(L.Train.PauseCaps), null, Styling.AccentAmber, true, null, width),
    };
}
