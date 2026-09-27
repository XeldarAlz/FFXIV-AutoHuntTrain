using AutoHuntTrain.Core.Hunts;
using AutoHuntTrain.Core.Localization;
using Dalamud.Interface;

namespace AutoHuntTrain.Windows;

internal static class HuntModeLabels
{
    public static FontAwesomeIcon Icon(HuntMode mode) => mode switch
    {
        HuntMode.HuntingLog => FontAwesomeIcon.BookOpen,
        HuntMode.CustomList => FontAwesomeIcon.Crosshairs,
        _ => FontAwesomeIcon.Scroll,
    };

    public static string Label(HuntMode mode) => Loc.T(mode switch
    {
        HuntMode.HuntingLog => L.HuntingLog.ModeHuntingLog,
        HuntMode.CustomList => L.HuntingLog.ModeCustom,
        _ => L.HuntingLog.ModeBills,
    });
}
