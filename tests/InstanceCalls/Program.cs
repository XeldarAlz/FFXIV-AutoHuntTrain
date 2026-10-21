using System.Text;
using AutoHuntTrain.Core.Train;

const string LinkMarker = "";
const string LinkInstance1 = "";
const string LinkInstance2 = "";
const string LinkInstance3 = "";
const string HeritageFoundLink = $"{LinkMarker}Heritage Found{LinkInstance1} ( 16.5  , 23.7 )";

(string Text, int Instance)[] standaloneCases =
[
    ("instance TWO. DOS. NI.", 2),
    ("inst two", 2),
    ("Instance Three", 3),
    ("INSTANCE nine", 9),
    ("i2", 2),
    ("i2 now", 2),
    ("inst 3", 3),
    ("instance ②", 2),
    ($"instance {LinkInstance3}", 3),
    ($"i {LinkInstance2}", 2),
    ("swap to two", 2),
    ("move to THREE!", 3),
    ("switch to instance four", 4),
    ("go to second instance", 2),
    ("Third inst", 3),
    ("GO TO 2ND INSTANCE", 2),
    ("i1 done, swap to i2", 2),
    ("instance one done, swap to two", 2),
    ("move 2 min", 0),
    ("move two min", 0),
    ("go to one more", 0),
    ("I one-shot it", 0),
    ("i two", 0),
    ("instead of one", 0),
    ("instance seventeen", 0),
    ("instance twenty", 0),
    ("instance 12", 0),
    ("someone said one", 0),
    ("a second please", 0),
];

(string Text, int Number, bool Same, int Linked)[] flagCases =
[
    ($"Vibing at {HeritageFoundLink} instance {LinkInstance2}", 2, false, 1),
    ($"instance {LinkInstance2} {HeritageFoundLink}", 2, false, 1),
    ($"{HeritageFoundLink} instance two", 2, false, 1),
    ($"{HeritageFoundLink} INST THREE", 3, false, 1),
    ($"{HeritageFoundLink} second instance", 2, false, 1),
    ($"{HeritageFoundLink} i2", 2, false, 1),
    ($"{HeritageFoundLink} same", 0, true, 1),
    ($"All together Meow! Onto the next act~ {HeritageFoundLink}", 0, false, 1),
    ($"{LinkMarker}Il Mheg{LinkInstance2} ( 10.0  , 10.0 )", 0, false, 2),
    ($"{LinkMarker}Il Mheg{LinkInstance1} ( 10.0  , 10.0 ) i one-shot it", 0, false, 1),
    ("i1 pull, move 2 min", 1, false, 0),
];

var failures = 0;
for (var caseIndex = 0; caseIndex < standaloneCases.Length; caseIndex++)
{
    var (text, instance) = standaloneCases[caseIndex];
    var parsed = InstanceHints.ParseStandalone(text);
    Check(parsed == instance, $"standalone \"{Visible(text)}\" calls {instance} (read {parsed})");
}

for (var caseIndex = 0; caseIndex < flagCases.Length; caseIndex++)
{
    var (text, number, same, linked) = flagCases[caseIndex];
    var hint = InstanceHints.Parse(text);
    var expected = new InstanceHint(number, same, linked);
    Check(hint == expected, $"flag \"{Visible(text)}\" reads {expected} (read {hint})");
}

Console.WriteLine(failures == 0 ? "All instance call checks passed" : $"{failures} instance call checks failed");
return failures == 0 ? 0 : 1;

void Check(bool condition, string name)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine($"FAIL: {name}");
        return;
    }

    Console.WriteLine($"PASS: {name}");
}

// The game's private use glyphs print as boxes in a console.
static string Visible(string text)
{
    var builder = new StringBuilder(text.Length);
    for (var index = 0; index < text.Length; index++)
    {
        var character = text[index];
        if (character is >= '' and <= '')
        {
            builder.Append($"<U+{(int)character:X4}>");
            continue;
        }

        builder.Append(character);
    }

    return builder.ToString();
}
