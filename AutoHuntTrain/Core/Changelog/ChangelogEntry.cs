using AutoHuntTrain.Core.Localization;

namespace AutoHuntTrain.Core.Changelog;

internal readonly record struct ChangelogEntry(string Version, string Date, LocString[] Highlights);
