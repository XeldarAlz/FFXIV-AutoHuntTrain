using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Train;

// The player whose flags drive the ride: a character name and the id of their home world.
internal readonly record struct ConductorIdentity(string Name, uint WorldId)
{
    public static readonly ConductorIdentity None = new(string.Empty, 0);

    public bool IsSet => Name.Length > 0;

    public bool Matches(in FlagPost post)
        => post.SenderWorldId == WorldId && string.Equals(post.SenderName, Name, StringComparison.OrdinalIgnoreCase);

    public bool SameAs(in ConductorIdentity other)
        => other.WorldId == WorldId && string.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase);
}

internal static class Conductor
{
    private const string ClearArgument = "clear";
    private const char WorldSeparator = '@';
    private const string Usage = AhtConstants.LogPrefix + " Usage: /aht conductor <First Last>[@World], or /aht conductor clear.";

    private static readonly char[] nameSeparators = [' ', '\t'];

    public static ConductorIdentity Current
    {
        get
        {
            var configuration = Plugin.Instance.Configuration;
            return new ConductorIdentity(configuration.ConductorName, configuration.ConductorWorldId);
        }
    }

    public static bool IsSet => Current.IsSet;

    public static string Describe(in ConductorIdentity identity)
        => Worlds.TryFindById(identity.WorldId, out var world) ? $"{identity.Name}{WorldSeparator}{world.Name}" : identity.Name;

    public static void HandleCommand(string arguments)
    {
        if (arguments.Length == 0)
        {
            var current = Current;
            Svc.Chat.Print(current.IsSet
                ? $"{AhtConstants.LogPrefix} The conductor is {Describe(current)}."
                : $"{AhtConstants.LogPrefix} No conductor is set. {Usage}");
            return;
        }

        if (arguments.Equals(ClearArgument, StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            Svc.Chat.Print($"{AhtConstants.LogPrefix} Conductor cleared.");
            return;
        }

        TryApply(arguments);
    }

    // Shared by the command and the Ride card's text field, so both answer in chat the same way.
    public static bool TryApply(string text)
    {
        if (!TryParse(text, out var identity, out var problem))
        {
            Svc.Chat.PrintError(problem);
            return false;
        }

        Apply(identity);
        return true;
    }

    public static void Apply(in ConductorIdentity identity)
    {
        Set(identity);
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Following {Describe(identity)}; their flags in chat drive the ride.");
    }

    public static void Set(in ConductorIdentity identity)
    {
        var configuration = Plugin.Instance.Configuration;
        configuration.ConductorName = identity.Name;
        configuration.ConductorWorldId = identity.WorldId;
        configuration.SaveDebounced();
        RunLog.Info($"Conductor set to {Describe(identity)} (world {identity.WorldId}).");
    }

    public static void Clear()
    {
        var configuration = Plugin.Instance.Configuration;
        if (configuration.ConductorName.Length == 0 && configuration.ConductorWorldId == 0)
        {
            return;
        }

        configuration.ConductorName = string.Empty;
        configuration.ConductorWorldId = 0;
        configuration.SaveDebounced();
        RunLog.Info("Conductor cleared.");
    }

    // "First Last" on the current world, or "First Last@World" for a visitor from elsewhere.
    public static bool TryParse(string text, out ConductorIdentity identity, out string problem)
    {
        identity = ConductorIdentity.None;
        problem = string.Empty;
        var trimmed = text.Trim();
        var separator = trimmed.IndexOf(WorldSeparator);
        var namePart = separator >= 0 ? trimmed[..separator] : trimmed;
        var worldPart = separator >= 0 ? trimmed[(separator + 1)..].Trim() : string.Empty;
        var name = string.Join(' ', namePart.Split(nameSeparators, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length == 0)
        {
            problem = Usage;
            return false;
        }

        WorldInfo world;
        if (worldPart.Length == 0)
        {
            if (!Worlds.TryCurrent(out world))
            {
                problem = $"{AhtConstants.LogPrefix} The current world could not be read; is a character logged in?";
                return false;
            }
        }
        else if (!Worlds.TryFind(worldPart, out world))
        {
            problem = $"{AhtConstants.LogPrefix} No world matches '{worldPart}'.";
            return false;
        }

        identity = new ConductorIdentity(name, world.Id);
        return true;
    }
}
