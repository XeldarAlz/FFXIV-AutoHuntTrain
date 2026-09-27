using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;

namespace AutoHuntTrain.Windows;

// Every relay channel's name in the plugin language, in RelayChannel order, rebuilt only when the language changes.
internal static class RelayChannelLabels
{
    private static readonly string[] labels = new string[TrainRelay.ChannelCount];

    private static LanguageInfo? builtFor;

    public static string[] All()
    {
        if (!ReferenceEquals(builtFor, Loc.Current))
        {
            Build();
        }

        return labels;
    }

    public static string Of(RelayChannel channel)
    {
        var all = All();
        return (uint)channel < (uint)all.Length ? all[(int)channel] : all[(int)RelayChannel.Party];
    }

    private static void Build()
    {
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = Label((RelayChannel)index);
        }

        builtFor = Loc.Current;
    }

    private static string Label(RelayChannel channel) => channel switch
    {
        RelayChannel.Say => Loc.T(L.Details.ChannelSay),
        RelayChannel.Yell => Loc.T(L.Details.ChannelYell),
        RelayChannel.Shout => Loc.T(L.Details.ChannelShout),
        RelayChannel.Party => Loc.T(L.Details.ChannelParty),
        RelayChannel.Alliance => Loc.T(L.Details.ChannelAlliance),
        RelayChannel.FreeCompany => Loc.T(L.Details.ChannelFreeCompany),
        >= RelayChannel.Linkshell1 and <= RelayChannel.Linkshell8 => Loc.T(L.Details.ChannelLinkshell, channel - RelayChannel.Linkshell1 + 1),
        >= RelayChannel.CrossWorldLinkshell1 and <= RelayChannel.CrossWorldLinkshell8 => Loc.T(L.Details.ChannelCrossWorldLinkshell, channel - RelayChannel.CrossWorldLinkshell1 + 1),
        _ => Loc.T(L.Details.ChannelEcho),
    };
}
