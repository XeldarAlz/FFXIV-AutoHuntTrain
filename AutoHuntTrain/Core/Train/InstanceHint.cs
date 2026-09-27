namespace AutoHuntTrain.Core.Train;

// What a flag's text says about the instance: a number, "same" for the one the conductor already named, or nothing.
internal readonly record struct InstanceHint(int Number, bool Same)
{
    public bool NamesNumber => Number > 0;
}

internal static class InstanceHints
{
    private const char FirstCircledDigit = '①';
    private const char LastCircledDigit = '⑥';
    private const char ShortPrefix = 'i';
    private const string LongWord = "instance";
    private const string ShortWord = "inst";
    private const string SameWord = "same";

    public static InstanceHint Parse(string text)
    {
        var same = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is >= FirstCircledDigit and <= LastCircledDigit)
            {
                return new InstanceHint(character - FirstCircledDigit + 1, false);
            }

            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (char.ToLowerInvariant(character) == ShortPrefix)
            {
                var afterWord = WordEnd(text, index, LongWord) ?? WordEnd(text, index, ShortWord) ?? index + 1;
                if (TryReadNumber(text, afterWord, out var number))
                {
                    return new InstanceHint(number, false);
                }

                continue;
            }

            if (WordEnd(text, index, SameWord) is { } end && (end == text.Length || !char.IsLetterOrDigit(text[end])))
            {
                same = true;
            }
        }

        return new InstanceHint(0, same);
    }

    private static int? WordEnd(string text, int start, string word)
    {
        if (start + word.Length > text.Length)
        {
            return null;
        }

        return string.Compare(text, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0 ? start + word.Length : null;
    }

    // One digit, so "i 20" and "instance 12" name no instance.
    private static bool TryReadNumber(string text, int start, out int number)
    {
        number = 0;
        var index = start;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length || text[index] is < '1' or > '9')
        {
            return false;
        }

        if (index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
        {
            return false;
        }

        number = text[index] - '0';
        return true;
    }
}
