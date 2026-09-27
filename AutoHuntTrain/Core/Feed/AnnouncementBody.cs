using System.Globalization;
using System.Numerics;
using System.Text;

namespace AutoHuntTrain.Core.Feed;

// The relay's text as a reader sees it: the "Label: value" header HuntAlerts puts above the Discord post, the post
// itself with Discord's markup taken out, and the first map coordinates the post gives.
internal static class AnnouncementBody
{
    public const string StartZoneLabel = "Start Zone";
    public const string AetheryteLabel = "Aetheryte";

    private const string KindLabel = "Kind";
    private const char LineBreak = '\n';
    private const char Colon = ':';
    // A map coordinate runs from 1 to a little over 42 on the largest maps; "12.34" is as long as one gets.
    private const float MinimumMapCoordinate = 1f;
    private const float MaximumMapCoordinate = 45f;
    private const int MaxCoordinateLength = 6;
    private const string BulletMarker = "• ";

    // The header ends at the first blank line; a text that does not open with the relay's "Kind:" line has none.
    public static int BodyStart(string text)
    {
        if (!StartsWithField(text, 0, KindLabel))
        {
            return 0;
        }

        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf(LineBreak, lineStart);
            if (lineEnd < 0)
            {
                return text.Length;
            }

            if (IsBlank(text, lineStart, lineEnd))
            {
                return lineEnd + 1;
            }

            lineStart = lineEnd + 1;
        }

        return text.Length;
    }

    // The value of a "Label: value" header line, or empty when the header has no such line.
    public static string ReadHeaderField(string text, string label)
    {
        var end = BodyStart(text);
        var lineStart = 0;
        while (lineStart < end)
        {
            var lineEnd = text.IndexOf(LineBreak, lineStart);
            if (lineEnd < 0 || lineEnd > end)
            {
                lineEnd = end;
            }

            if (StartsWithField(text, lineStart, label))
            {
                var valueStart = lineStart + label.Length + 1;
                return text[valueStart..lineEnd].Trim();
            }

            lineStart = lineEnd + 1;
        }

        return string.Empty;
    }

    // The post without its header, with bold, italic, underline, strike, spoiler and code marks, heading and quote
    // prefixes and mention tags dropped. Line breaks stay, runs of blank lines fold to one, and a starred list item
    // keeps a bullet. Emoji were already taken out at intake.
    public static string CleanForReading(string text)
    {
        var start = BodyStart(text);
        var builder = new StringBuilder(text.Length - start);
        var lineStart = start;
        var pendingBlank = false;
        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf(LineBreak, lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var line = StripLinePrefix(text.AsSpan(lineStart, lineEnd - lineStart).Trim(), out var bullet);
            lineStart = lineEnd + 1;
            if (line.IsEmpty)
            {
                pendingBlank = builder.Length > 0;
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(LineBreak);
                if (pendingBlank)
                {
                    builder.Append(LineBreak);
                }
            }

            pendingBlank = false;
            if (bullet)
            {
                builder.Append(BulletMarker);
            }

            AppendWithoutMarks(builder, line);
        }

        return builder.ToString();
    }

    // The first "(x, y)" pair whose two numbers both fit on a map, as in "(9.0, 12.0)".
    public static bool TryReadCoordinates(string text, out Vector2 coordinates)
    {
        var open = text.IndexOf('(');
        while (open >= 0)
        {
            if (TryReadPairAt(text, open + 1, out coordinates))
            {
                return true;
            }

            open = text.IndexOf('(', open + 1);
        }

        coordinates = default;
        return false;
    }

    private static bool TryReadPairAt(string text, int start, out Vector2 coordinates)
    {
        coordinates = default;
        var index = SkipSpaces(text, start);
        if (!TryReadNumber(text, ref index, out var x))
        {
            return false;
        }

        index = SkipSpaces(text, index);
        if (index >= text.Length || text[index] != ',')
        {
            return false;
        }

        index = SkipSpaces(text, index + 1);
        if (!TryReadNumber(text, ref index, out var y))
        {
            return false;
        }

        index = SkipSpaces(text, index);
        if (index >= text.Length || text[index] != ')' || !OnMap(x) || !OnMap(y))
        {
            return false;
        }

        coordinates = new Vector2(x, y);
        return true;
    }

    private static bool TryReadNumber(string text, ref int index, out float value)
    {
        value = 0f;
        var start = index;
        while (index < text.Length && index - start < MaxCoordinateLength && (char.IsAsciiDigit(text[index]) || text[index] == '.'))
        {
            index++;
        }

        return index > start
            && float.TryParse(text.AsSpan(start, index - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool OnMap(float coordinate) => coordinate is >= MinimumMapCoordinate and <= MaximumMapCoordinate;

    private static int SkipSpaces(string text, int start)
    {
        var index = start;
        while (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        return index;
    }

    private static bool StartsWithField(string text, int start, string label)
        => text.Length - start > label.Length
        && string.Compare(text, start, label, 0, label.Length, StringComparison.OrdinalIgnoreCase) == 0
        && text[start + label.Length] == Colon;

    private static bool IsBlank(string text, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    // "# ", "## ", "-# " headings and "> ", ">>> " quotes lose their prefix; "* " and "- " list items become bullets.
    private static ReadOnlySpan<char> StripLinePrefix(ReadOnlySpan<char> line, out bool bullet)
    {
        bullet = false;
        if (line.StartsWith("-#"))
        {
            line = line[2..];
        }

        var marks = 0;
        while (marks < line.Length && (line[marks] == '#' || line[marks] == '>'))
        {
            marks++;
        }

        if (marks > 0 && (marks == line.Length || line[marks] == ' '))
        {
            line = line[marks..];
        }

        line = line.TrimStart();
        if (line.Length > 1 && (line[0] == '*' || line[0] == '-') && line[1] == ' ')
        {
            bullet = true;
            line = line[2..].TrimStart();
        }

        return line;
    }

    // A lone underscore inside a word, as in a snake_case name, is text rather than a mark.
    private static void AppendWithoutMarks(StringBuilder builder, ReadOnlySpan<char> line)
    {
        var index = 0;
        while (index < line.Length)
        {
            var character = line[index];
            if (character is '*' or '`')
            {
                index++;
                continue;
            }

            if (character is '~' or '|' or '_' && index + 1 < line.Length && line[index + 1] == character)
            {
                index += 2;
                continue;
            }

            if (character == '_' && IsWordBoundary(line, index))
            {
                index++;
                continue;
            }

            if (character == '<' && TryMentionEnd(line, index, out var close))
            {
                index = close + 1;
                continue;
            }

            builder.Append(character);
            index++;
        }
    }

    private static bool IsWordBoundary(ReadOnlySpan<char> line, int index)
        => index == 0 || index == line.Length - 1 || !char.IsLetterOrDigit(line[index - 1]) || !char.IsLetterOrDigit(line[index + 1]);

    // <@123>, <@!123>, <@&123> and <#123>: users, roles and channels of the Discord server, meaningless in game.
    private static bool TryMentionEnd(ReadOnlySpan<char> line, int open, out int close)
    {
        close = -1;
        var index = open + 1;
        if (index >= line.Length || (line[index] != '@' && line[index] != '#'))
        {
            return false;
        }

        index++;
        if (index < line.Length && (line[index] == '!' || line[index] == '&'))
        {
            index++;
        }

        var digitsStart = index;
        while (index < line.Length && char.IsAsciiDigit(line[index]))
        {
            index++;
        }

        if (index == digitsStart || index >= line.Length || line[index] != '>')
        {
            return false;
        }

        close = index;
        return true;
    }
}
