using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Localization;
using ECommons.Automation;
using ECommons.Throttlers;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AutoHuntTrain.Core.Feed;

// Ported from HuntAlerts' RelayChannels: a one-line summary of a train posted to a chat channel, as in
// "Dawntrail train on Siren! Kozama'uka (9.0, 12.0) i2 - Ok'Hanu", with <flag> in place of the coordinates when the
// map flag was placed on the train's start first.
internal static class TrainRelay
{
    // The game refuses a chat line longer than this many UTF-8 bytes.
    private const int MaxMessageBytes = 500;
    private const int ThrottleMs = 1_000;
    private const string ThrottleKey = "aht_train_relay";
    private const string FlagPlaceholder = "<flag>";
    private const string AetheryteSeparator = " - ";
    private const string CoordinateFormat = "0.0";

    private static readonly string[] commands =
    [
        "/s", "/y", "/sh", "/p", "/a", "/fc",
        "/l1", "/l2", "/l3", "/l4", "/l5", "/l6", "/l7", "/l8",
        "/cwl1", "/cwl2", "/cwl3", "/cwl4", "/cwl5", "/cwl6", "/cwl7", "/cwl8",
        "/echo",
    ];

    public static int ChannelCount => commands.Length;

    public static bool Send(in Announcement announcement, RelayChannel channel, bool withFlag)
    {
        if (!EzThrottler.Throttle(ThrottleKey, ThrottleMs))
        {
            return false;
        }

        var flagged = withFlag
            && TrainFacts.TryFlagPoint(announcement, out var point)
            && MapFlag.TrySetFlag(announcement.TerritoryId, point);
        var command = Command(channel);
        var line = Fit(Chat.SanitiseText(string.Concat(command, " ", BuildText(announcement, flagged))));
        try
        {
            Chat.SendMessage(line);
            RunLog.Info($"Relay: posted the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} to {command}{(flagged ? " with the map flag" : string.Empty)}");
            return true;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Relay: posting the train on {announcement.World.Name} to {command} failed");
            return false;
        }
    }

    public static string BuildText(in Announcement announcement, bool flagged)
    {
        var builder = new StringBuilder(Loc.T(L.Details.RelayText, ExpansionGroups.LocalName(announcement.Group), announcement.World.Name));
        var zone = TrainFacts.StartZone(announcement);
        var coordinates = flagged ? FlagPlaceholder
            : TrainFacts.TryFlagPoint(announcement, out var point) ? FormatCoordinates(point)
            : string.Empty;
        if (zone.Length > 0 || coordinates.Length > 0)
        {
            builder.Append(' ').Append(zone);
            if (zone.Length > 0 && coordinates.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(coordinates);
        }

        if (announcement.NamesInstance)
        {
            builder.Append(' ').Append(Loc.T(L.Details.InstanceShort, announcement.Instance));
        }

        var aetheryte = TrainFacts.Aetheryte(announcement);
        if (aetheryte.Length > 0)
        {
            builder.Append(AetheryteSeparator).Append(aetheryte);
        }

        return builder.ToString();
    }

    public static string FormatCoordinates(Vector2 coordinates)
        => string.Concat(
            "(",
            coordinates.X.ToString(CoordinateFormat, CultureInfo.InvariantCulture),
            ", ",
            coordinates.Y.ToString(CoordinateFormat, CultureInfo.InvariantCulture),
            ")");

    private static string Command(RelayChannel channel)
        => (uint)channel < (uint)commands.Length ? commands[(int)channel] : commands[(int)RelayChannel.Party];

    private static string Fit(string line)
    {
        var length = line.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(line.AsSpan(0, length)) > MaxMessageBytes)
        {
            length--;
        }

        if (length < line.Length && length > 0 && char.IsHighSurrogate(line[length - 1]))
        {
            length--;
        }

        return length == line.Length ? line : line[..length];
    }
}
