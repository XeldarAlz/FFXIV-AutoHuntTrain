using System.Text;

namespace AutoHuntTrain.Core.Feed;

// What the relay's Discord text says beyond its fields: the start time in a timestamp tag, the conductor's name after
// a "Conductor:" label, and the emoji codes that only clutter a log line.
internal static class AnnouncementText
{
    private const string TimestampOpen = "<t:";
    private const char TagOpen = '<';
    private const char TagClose = '>';
    private const char Colon = ':';
    private const string ConductorLabel = "Conductor:";
    private const char WorldOpen = '[';
    private const char WorldClose = ']';
    private const char AnimatedEmojiMarker = 'a';
    // A Unix timestamp in seconds has ten digits until the year 2286.
    private const int MaxEpochDigits = 11;
    // A character name is two words of at most fifteen letters each.
    private const int NameWordMaxLength = 15;
    private const int EmojiNameMaxLength = 32;

    // A start outside the game's lifetime is a mangled tag, not a train.
    private static readonly long EarliestEpoch = new DateTimeOffset(2013, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    private static readonly long LatestEpoch = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    // The first well-formed <t:EPOCH> or <t:EPOCH:STYLE> tag.
    public static bool TryReadStartTime(string text, out DateTime startAtUtc)
    {
        startAtUtc = default;
        var open = text.IndexOf(TimestampOpen, StringComparison.Ordinal);
        while (open >= 0)
        {
            var digitsStart = open + TimestampOpen.Length;
            var index = digitsStart;
            long epoch = 0;
            while (index < text.Length && char.IsAsciiDigit(text[index]) && index - digitsStart < MaxEpochDigits)
            {
                epoch = epoch * 10 + (text[index] - '0');
                index++;
            }

            var wellFormed = index > digitsStart && index < text.Length && (text[index] == TagClose || text[index] == Colon);
            if (wellFormed && epoch >= EarliestEpoch && epoch <= LatestEpoch)
            {
                startAtUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                return true;
            }

            open = text.IndexOf(TimestampOpen, index, StringComparison.Ordinal);
        }

        return false;
    }

    // "Conductor: First Last" or "Conductor: [World] First Last"; the world is empty when the text named none.
    public static bool TryReadConductor(string text, out string name, out string worldName)
    {
        name = string.Empty;
        worldName = string.Empty;
        var label = text.IndexOf(ConductorLabel, StringComparison.OrdinalIgnoreCase);
        if (label < 0)
        {
            return false;
        }

        var index = SkipFiller(text, label + ConductorLabel.Length);
        if (index < text.Length && text[index] == WorldOpen)
        {
            var close = text.IndexOf(WorldClose, index);
            if (close < 0)
            {
                return false;
            }

            worldName = text[(index + 1)..close].Trim();
            index = SkipFiller(text, close + 1);
        }

        var firstEnd = NameWordEnd(text, index);
        var secondStart = SkipFiller(text, firstEnd);
        var secondEnd = NameWordEnd(text, secondStart);
        var firstLength = firstEnd - index;
        var secondLength = secondEnd - secondStart;
        if (firstLength == 0 || secondLength == 0 || firstLength > NameWordMaxLength || secondLength > NameWordMaxLength)
        {
            return false;
        }

        name = string.Concat(text.AsSpan(index, firstLength), " ", text.AsSpan(secondStart, secondLength));
        return true;
    }

    // Drops <:name:id>, <a:name:id> and :name: codes; every other tag, the timestamp included, stays as it was.
    public static string StripEmoji(string text)
    {
        if (text.IndexOf(Colon) < 0)
        {
            return text.Trim();
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == TagOpen)
            {
                var close = text.IndexOf(TagClose, index);
                if (close < 0)
                {
                    builder.Append(text, index, text.Length - index);
                    break;
                }

                if (!IsCustomEmoji(text, index, close))
                {
                    builder.Append(text, index, close - index + 1);
                }

                index = close + 1;
                continue;
            }

            if (character == Colon && TryEmojiCodeEnd(text, index, out var codeEnd))
            {
                index = codeEnd + 1;
                continue;
            }

            builder.Append(character);
            index++;
        }

        return builder.ToString().Trim();
    }

    private static int SkipFiller(string text, int start)
    {
        var index = start;
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || IsMarkdownMark(text[index])))
        {
            index++;
        }

        return index;
    }

    private static int NameWordEnd(string text, int start)
    {
        var index = start;
        while (index < text.Length && IsNameCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsNameCharacter(char character) => char.IsLetter(character) || character == '\'' || character == '-';

    private static bool IsMarkdownMark(char character) => character is '*' or '_' or '`' or '~';

    // <:name:id> or <a:name:id>, with the id all digits.
    private static bool IsCustomEmoji(string text, int open, int close)
    {
        var index = open + 1;
        if (index + 1 < close && char.ToLowerInvariant(text[index]) == AnimatedEmojiMarker && text[index + 1] == Colon)
        {
            index++;
        }

        if (index >= close || text[index] != Colon)
        {
            return false;
        }

        var nameStart = index + 1;
        var nameEnd = text.IndexOf(Colon, nameStart, close - nameStart);
        if (nameEnd <= nameStart || nameEnd + 1 >= close)
        {
            return false;
        }

        for (var digitIndex = nameEnd + 1; digitIndex < close; digitIndex++)
        {
            if (!char.IsAsciiDigit(text[digitIndex]))
            {
                return false;
            }
        }

        return true;
    }

    // :name: over letters, digits and underscores with at least one letter, so a clock time keeps its colons.
    private static bool TryEmojiCodeEnd(string text, int open, out int close)
    {
        close = -1;
        var hasLetter = false;
        var index = open + 1;
        while (index < text.Length && index - open <= EmojiNameMaxLength)
        {
            var character = text[index];
            if (character == Colon)
            {
                if (index == open + 1 || !hasLetter)
                {
                    return false;
                }

                close = index;
                return true;
            }

            if (char.IsAsciiLetter(character))
            {
                hasLetter = true;
            }
            else if (!char.IsAsciiDigit(character) && character != '_' && character != '+' && character != '-')
            {
                return false;
            }

            index++;
        }

        return false;
    }
}
