using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Localization;

namespace AutoHuntTrain.Windows;

// What the Start button says. Nothing can start until the ride task exists, so the only question is which reason to show.
internal static class HuntLauncher
{
    public enum Readiness : byte
    {
        SetupNeeded,
        NotBuilt,
    }

    public static Readiness Assess() => ExternalPlugins.AllRequiredInstalled() ? Readiness.NotBuilt : Readiness.SetupNeeded;

    public static void Start() => Plugin.Instance.Controller.Start();

    public static string Sublabel() => Loc.T(L.Train.StartSub);

    public static string Reason(Readiness readiness)
        => Loc.T(readiness == Readiness.SetupNeeded ? L.Train.ReasonInstall : L.Train.ReasonNotBuilt);
}
