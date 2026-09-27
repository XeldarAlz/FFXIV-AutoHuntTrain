using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Globalization;
using System.Text;

namespace AutoHuntTrain.Core.Feed;

// A made-up announcement pushed through the same intake as the feed's, so every rule, the details view and the whole
// ride can be tried with no real train, on a world in any region. A body: tail stands in for the Discord post.
internal static class FeedInjector
{
    private const string Usage = AhtConstants.LogPrefix + " Usage: /aht inject <World> <DT|EW|SHB|Centurio> [<aetheryte name>] [i<n>] [+<minutes>] [conductor:<First Last>] [body:<text, \\n for a new line>].";
    private const string ConductorPrefix = "conductor:";
    private const string BodyPrefix = "body:";
    private const string EscapedLineBreak = "\\n";
    private const string TrainType = "new_hunt";
    private const int DefaultLeadMinutes = 5;
    private const int MaxLeadMinutes = 360;

    private static readonly char[] wordSeparators = [' ', '\t'];

    public static void HandleCommand(string arguments, FeedListener feed)
    {
        var customBody = SplitBody(ref arguments);
        var words = arguments.Split(wordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        if (!Worlds.TryFind(words[0], out var world))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No world matches '{words[0]}'.");
            return;
        }

        if (!ExpansionGroups.TryParse(words[1], out var group))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} '{words[1]}' is not an expansion group; use DT, EW, SHB or Centurio.");
            return;
        }

        var minutes = DefaultLeadMinutes;
        var instance = 0;
        var conductor = string.Empty;
        var aetheryteName = new StringBuilder();
        for (var index = 2; index < words.Length; index++)
        {
            var word = words[index];
            if (word.StartsWith(ConductorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                conductor = word[ConductorPrefix.Length..];
                if (index + 1 < words.Length)
                {
                    conductor = $"{conductor} {words[index + 1]}";
                    index++;
                }

                continue;
            }

            if (TryParseLead(word, out var lead))
            {
                minutes = lead;
                continue;
            }

            if (AutoGoto.TryParseInstance(word, out var parsedInstance))
            {
                instance = parsedInstance;
                continue;
            }

            if (aetheryteName.Length > 0)
            {
                aetheryteName.Append(' ');
            }

            aetheryteName.Append(word);
        }

        var aetheryteId = 0u;
        var territoryId = 0u;
        var place = string.Empty;
        var header = string.Empty;
        if (aetheryteName.Length > 0)
        {
            var name = aetheryteName.ToString();
            if (!ZoneAetherytes.TryFindByName(name, out territoryId, out var aetheryte))
            {
                Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No single aetheryte matches '{name}'.");
                return;
            }

            aetheryteId = aetheryte.Id;
            place = $" at {TerritoryNames.Of(territoryId)} - {aetheryte.Name}";
            header = $"{Environment.NewLine}Start Zone: {TerritoryNames.Of(territoryId)}{Environment.NewLine}Aetheryte: {aetheryte.Name}";
        }

        var now = DateTimeOffset.UtcNow;
        var startAt = now.AddMinutes(minutes);
        var conductorText = conductor.Length > 0 ? $" (Conductor: [{world.Name}] {conductor})" : string.Empty;
        // Shaped like the text HuntAlerts relays: a header with the post time, then the announcement with its Discord
        // timestamp already turned into a local clock time.
        var newLine = Environment.NewLine;
        var body = customBody is null
            ? $"**[{world.Name}]** Hunt train starting {ClockText(startAt)}{place}{conductorText}."
            : CompleteBody(customBody, startAt, now, conductor);
        var text = $"Kind: Hunt Train{newLine}Hunt: {ExpansionGroups.Name(group)}{header}{newLine}World: {world.Name}{newLine}Posted: {ClockText(now)}{newLine}{newLine}{body}";
        var currentWorldId = Worlds.TryCurrent(out var current) ? current.Id : 0u;
        var message = new HuntAlertMessage(text, TrainType, ExpansionGroups.Name(group), world.Id, currentWorldId, 0, 0, now, now.ToUnixTimeSeconds(), aetheryteId, territoryId, instance, null, null);

        var instanceText = instance > 0 ? $", instance {instance}" : string.Empty;
        var conductorNote = conductor.Length > 0 ? $", conductor {conductor}" : string.Empty;
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Injected a {ExpansionGroups.Name(group)} train on {world.Name} starting in {minutes} min{place}{instanceText}{conductorNote}.");
        feed.Accept(message);
    }

    // Everything after "body:" is the post, spaces and all, with \n standing for a line break; the words before it
    // are the usual arguments.
    private static string? SplitBody(ref string arguments)
    {
        var start = arguments.IndexOf(BodyPrefix, StringComparison.OrdinalIgnoreCase);
        while (start > 0 && !char.IsWhiteSpace(arguments[start - 1]))
        {
            start = arguments.IndexOf(BodyPrefix, start + BodyPrefix.Length, StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            return null;
        }

        var body = arguments[(start + BodyPrefix.Length)..].Trim().Replace(EscapedLineBreak, Environment.NewLine, StringComparison.Ordinal);
        arguments = arguments[..start];
        return body;
    }

    // A post that gives no start time or conductor of its own gets the ones the command gave, on lines of their own
    // in the relay's style.
    private static string CompleteBody(string body, DateTimeOffset startAt, DateTimeOffset now, string conductor)
    {
        var completed = body;
        if (!AnnouncementText.TryReadStartTime(body, out _) && !AnnouncementText.TryReadClockTime(body, now.UtcDateTime, out _))
        {
            completed = $"{completed}{Environment.NewLine}**Starting time**: {ClockText(startAt)}";
        }

        if (conductor.Length > 0 && !AnnouncementText.TryReadConductor(body, out _, out _))
        {
            completed = $"{completed}{Environment.NewLine}**Conductor**: {conductor}";
        }

        return completed;
    }

    private static string ClockText(DateTimeOffset time)
        => time.ToLocalTime().ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"));

    // "+5" or "-2": minutes from now, negative for a train that already started.
    private static bool TryParseLead(string token, out int minutes)
    {
        minutes = 0;
        if (token.Length < 2 || (token[0] != '+' && token[0] != '-'))
        {
            return false;
        }

        if (!int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var magnitude) || magnitude > MaxLeadMinutes)
        {
            return false;
        }

        minutes = token[0] == '-' ? -magnitude : magnitude;
        return true;
    }
}
