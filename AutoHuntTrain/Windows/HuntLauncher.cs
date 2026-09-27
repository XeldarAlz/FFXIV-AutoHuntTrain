using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;

namespace AutoHuntTrain.Windows;

// What the Start button says: a ride needs the required plugins and a conductor to follow.
internal static class HuntLauncher
{
    public enum Readiness : byte
    {
        SetupNeeded,
        NoConductor,
        Ready,
    }

    public static Readiness Assess()
    {
        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return Readiness.SetupNeeded;
        }

        return Conductor.IsSet ? Readiness.Ready : Readiness.NoConductor;
    }

    public static void Start() => Plugin.Instance.Controller.Start();

    public static string Sublabel() => Loc.T(L.Train.StartSub);

    public static string? Reason(Readiness readiness) => readiness switch
    {
        Readiness.SetupNeeded => Loc.T(L.Train.ReasonInstall),
        Readiness.NoConductor => Loc.T(L.Ride.ReasonPickConductor),
        _ => null,
    };
}
