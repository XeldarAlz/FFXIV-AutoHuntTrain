namespace AutoHuntTrain.Core.Train;

// What a conductor posts once the train is over. English only for now.
internal static class TrainEndPhrases
{
    private const char CurlyApostrophe = '\u2019';

    private static readonly string[] phrases =
    [
        "thank you for", "thanks for", "thank u", "ty for", "tyfc", "tyvm all",
        "that's all", "thats all", "that's it", "thats it",
        "end of train", "end of the train", "train over", "train is over", "train done", "train finished",
        "all done", "we're done", "we are done", "that concludes",
    ];

    // A phrase counts only as whole words, so "ty for" is not found inside "party for".
    public static string? Find(string text)
    {
        var lowered = text.ToLowerInvariant().Replace(CurlyApostrophe, '\'');
        for (var phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
        {
            if (ContainsWords(lowered, phrases[phraseIndex]))
            {
                return phrases[phraseIndex];
            }
        }

        return null;
    }

    private static bool ContainsWords(string text, string phrase)
    {
        var start = text.IndexOf(phrase, StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = start + phrase.Length;
            if ((start == 0 || !char.IsLetterOrDigit(text[start - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end])))
            {
                return true;
            }

            start = text.IndexOf(phrase, start + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
