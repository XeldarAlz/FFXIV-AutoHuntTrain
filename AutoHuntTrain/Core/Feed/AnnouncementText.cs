using System.Text;

namespace AutoHuntTrain.Core.Feed;

// What the relay's Discord text says beyond its fields: the start time in a timestamp tag, the conductor's name after
// a "Conductor" label, and the emoji codes that only clutter a log line.
internal static class AnnouncementText
{
    private const string TimestampOpen = "<t:";
    private const char TagOpen = '<';
    private const char TagClose = '>';
    private const char Colon = ':';
    private const string ConductorWord = "Conductor";
    private const char WorldOpen = '[';
    private const char WorldClose = ']';
    private const char AnimatedEmojiMarker = 'a';
    // A Unix timestamp in seconds has ten digits until the year 2286.
    private const int MaxEpochDigits = 11;
    // A character name is two words of at most fifteen letters each.
    private const int NameWordMaxLength = 15;
    private const int EmojiNameMaxLength = 32;
    private const string PostedLabel = "Posted:";
    private const char LineBreak = '\n';
    // HuntAlerts relays every Discord timestamp as this PC's local clock time in the form "07:45 PM", eight characters.
    private const int ClockLength = 8;
    private const int HoursPerHalfDay = 12;
    private const int MinutesPerHour = 60;

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

    // The first "hh:mm AM" or "hh:mm PM" outside the "Posted:" line, read on the day nearest the post: HuntAlerts turns
    // every Discord timestamp into exactly that before relaying the text, so the tag above is usually gone.
    public static bool TryReadClockTime(string text, DateTime postedAtUtc, out DateTime startAtUtc)
    {
        startAtUtc = default;
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf(LineBreak, lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            if (!StartsWithLabel(text, lineStart, lineEnd, PostedLabel) && TryFindClock(text, lineStart, lineEnd, out var hour, out var minute))
            {
                startAtUtc = NearestLocalTime(hour, minute, postedAtUtc);
                return true;
            }

            lineStart = lineEnd + 1;
        }

        return false;
    }

    private static bool StartsWithLabel(string text, int start, int end, string label)
    {
        var index = start;
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return end - index >= label.Length && string.Compare(text, index, label, 0, label.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool TryFindClock(string text, int start, int end, out int hour, out int minute)
    {
        for (var index = start; index + ClockLength <= end; index++)
        {
            if (index > start && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (index + ClockLength < end && char.IsLetterOrDigit(text[index + ClockLength]))
            {
                continue;
            }

            if (TryReadClockAt(text, index, out hour, out minute))
            {
                return true;
            }
        }

        hour = 0;
        minute = 0;
        return false;
    }

    private static bool TryReadClockAt(string text, int index, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (!char.IsAsciiDigit(text[index]) || !char.IsAsciiDigit(text[index + 1]) || text[index + 2] != Colon
            || !char.IsAsciiDigit(text[index + 3]) || !char.IsAsciiDigit(text[index + 4]) || text[index + 5] != ' '
            || char.ToUpperInvariant(text[index + 7]) != 'M')
        {
            return false;
        }

        var meridiem = char.ToUpperInvariant(text[index + 6]);
        if (meridiem != 'A' && meridiem != 'P')
        {
            return false;
        }

        var twelveHour = (text[index] - '0') * 10 + (text[index + 1] - '0');
        minute = (text[index + 3] - '0') * 10 + (text[index + 4] - '0');
        if (twelveHour is < 1 or > HoursPerHalfDay || minute >= MinutesPerHour)
        {
            return false;
        }

        hour = twelveHour % HoursPerHalfDay + (meridiem == 'P' ? HoursPerHalfDay : 0);
        return true;
    }

    // A clock time carries no date; the train starts on whichever day puts it nearest the post, so a train posted at
    // 23:50 for 00:10 lands on the next day.
    private static DateTime NearestLocalTime(int hour, int minute, DateTime postedAtUtc)
    {
        var postedDate = postedAtUtc.ToLocalTime().Date;
        var best = postedAtUtc;
        var bestGap = TimeSpan.MaxValue;
        for (var dayOffset = -1; dayOffset <= 1; dayOffset++)
        {
            var local = DateTime.SpecifyKind(postedDate.AddDays(dayOffset).AddHours(hour).AddMinutes(minute), DateTimeKind.Local);
            var candidate = local.ToUniversalTime();
            var gap = (candidate - postedAtUtc).Duration();
            if (gap >= bestGap)
            {
                continue;
            }

            bestGap = gap;
            best = candidate;
        }

        return best;
    }

    // "Conductor: First Last" or "Conductor: [World] First Last", with Discord marks and spaces allowed around the
    // label as in "**Conductor**: First Last"; the world is empty when the text named none.
    public static bool TryReadConductor(string text, out string name, out string worldName)
    {
        var label = text.IndexOf(ConductorWord, StringComparison.OrdinalIgnoreCase);
        while (label >= 0)
        {
            if (TryReadConductorAt(text, label, out name, out worldName))
            {
                return true;
            }

            label = text.IndexOf(ConductorWord, label + ConductorWord.Length, StringComparison.OrdinalIgnoreCase);
        }

        name = string.Empty;
        worldName = string.Empty;
        return false;
    }

    private static bool TryReadConductorAt(string text, int label, out string name, out string worldName)
    {
        name = string.Empty;
        worldName = string.Empty;
        if (label > 0 && char.IsLetterOrDigit(text[label - 1]))
        {
            return false;
        }

        var colon = SkipFiller(text, label + ConductorWord.Length);
        if (colon >= text.Length || text[colon] != Colon)
        {
            return false;
        }

        var index = SkipFiller(text, colon + 1);
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
            worldName = string.Empty;
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
        while (index < text.Length && (text[index] is ' ' or '	' || IsMarkdownMark(text[index])))
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

    private static bool IsMarkdownMark(char character) => character is '*' or '_' or '`' or '~' or '|';

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
