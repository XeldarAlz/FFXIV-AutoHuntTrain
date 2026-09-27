using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Marks;

namespace AutoHuntTrain.Windows;

internal static class ExpansionLabels
{
    public static string Name(ExpansionKind kind) => kind switch
    {
        ExpansionKind.ARR => Loc.T(L.Train.ExpansionArr),
        ExpansionKind.HW  => Loc.T(L.Train.ExpansionHw),
        ExpansionKind.SB  => Loc.T(L.Train.ExpansionSb),
        ExpansionKind.ShB => Loc.T(L.Train.ExpansionShb),
        ExpansionKind.EW  => Loc.T(L.Train.ExpansionEw),
        _                 => Loc.T(L.Train.ExpansionDt),
    };
}
