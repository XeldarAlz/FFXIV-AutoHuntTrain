using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// One announced train on the Train page: who, where and when, the post itself cleaned for reading, and what can be
// done with it. Every text is built once per train, feed change and language; only the countdown changes, once a
// minute.
internal sealed partial class TrainDetails
{
    private const float BackButtonHeight = 28f;
    private const float CardPadX = 20f;
    private const float CardPadY = 16f;
    private const float SectionGap = 14f;
    private const float BadgeGap = 8f;
    private const float LabelGap = 6f;
    private const float RowGap = 14f;
    private const float ClockGap = 14f;
    private const float FactColumnGap = 18f;
    private const float WideFactsWidth = 560f;
    private const float HeroTint = 0.10f;
    private const int MaxFacts = 6;
    private const string BackButtonId = "##aht_details_back";

    private readonly string[] factLabels = new string[MaxFacts];
    private readonly string[] factValues = new string[MaxFacts];

    private int builtForId = -1;
    private int builtForVersion = -1;
    private LanguageInfo? builtForLanguage;
    private int factCount;
    private string title = string.Empty;
    private string subtitle = string.Empty;
    private string worldBadge = string.Empty;
    private string startClock = string.Empty;
    private string body = string.Empty;
    private string navHint = string.Empty;
    private bool hasFlagPoint;
    private Vector2 flagPoint;
    private CachedText countdown;

    // True when the player asked to go back to the list.
    public bool Draw(Plugin plugin, in Announcement announcement)
    {
        Build(announcement, plugin.Feed.Version);
        var back = DrawBackButton();
        Styling.VSpace(12f);
        PageHeader.Draw(title, subtitle);
        DrawHero(plugin, announcement);
        Styling.VSpace(SectionGap);
        DrawFacts();
        Styling.VSpace(SectionGap);
        DrawBody();
        Styling.VSpace(12f);
        return back;
    }

    private static bool DrawBackButton()
        => PillButton.Draw(BackButtonId, Loc.T(L.Details.Back), Styling.AccentGlow, PillButton.Emphasis.Ghost, FontAwesomeIcon.ArrowLeft, height: BackButtonHeight);

    // A repeat announcement can fill in what the first one lacked under the same id, so a feed change rebuilds too.
    private void Build(in Announcement announcement, int feedVersion)
    {
        if (builtForId == announcement.Id && builtForVersion == feedVersion && ReferenceEquals(builtForLanguage, Loc.Current))
        {
            return;
        }

        builtForId = announcement.Id;
        builtForVersion = feedVersion;
        builtForLanguage = Loc.Current;
        var world = announcement.World;
        var region = RegionLabels.Name(world.Region);
        var zone = TrainFacts.StartZone(announcement);
        var notNamed = Loc.T(L.Details.NotNamed);
        title = Loc.T(L.Details.Title, ExpansionGroups.LocalName(announcement.Group), world.Name);
        worldBadge = Loc.T(L.Details.WorldLine, world.Name, world.DataCenterName, region);
        startClock = LocalClock(announcement.StartAtUtc);
        body = AnnouncementBody.CleanForReading(announcement.Message);
        subtitle = FirstLine(body, worldBadge);
        hasFlagPoint = TrainFacts.TryFlagPoint(announcement, out flagPoint);
        navHint = Loc.T(L.Details.NavHint, zone.Length > 0 ? zone : notNamed, world.Name);

        factCount = 0;
        AddFact(L.Details.LabelStartZone, zone.Length > 0 ? zone : notNamed);
        var aetheryte = TrainFacts.Aetheryte(announcement);
        AddFact(L.Details.LabelAetheryte, aetheryte.Length > 0 ? aetheryte : notNamed);
        AddFact(L.Details.LabelConductor, announcement.NamesConductor ? Conductor.Describe(announcement.Conductor) : notNamed);
        AddFact(L.Details.LabelPosted, LocalClock(announcement.PostedAtUtc));
        if (announcement.NamesInstance)
        {
            AddFact(L.Details.LabelInstance, NumberText.Of(announcement.Instance));
        }

        if (hasFlagPoint)
        {
            AddFact(L.Details.LabelFlag, TrainRelay.FormatCoordinates(flagPoint));
        }
    }

    private void AddFact(LocString label, string value)
    {
        factLabels[factCount] = Loc.T(label);
        factValues[factCount] = value;
        factCount++;
    }

    // A Discord post opens with the train's own name, which makes the best subtitle.
    private static string FirstLine(string text, string fallback)
    {
        if (text.Length == 0)
        {
            return fallback;
        }

        var end = text.IndexOf('\n');
        return end < 0 ? text : text[..end];
    }

    private static string LocalClock(DateTime utc) => utc.ToLocalTime().ToString("t", Loc.Culture);

    // Badges, the start as a clock time with its countdown, then the actions, on a glass card tinted by the group.
    private void DrawHero(Plugin plugin, in Announcement announcement)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CardPadX * scale;
        var left = origin.X + padX;
        var right = origin.X + width - padX;
        var y = origin.Y + CardPadY * scale;

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        float badgeHeight;
        using (Fonts.PushCaption())
        {
            badgeHeight = ImGui.GetTextLineHeight() + 4f * scale;
        }

        var midY = y + badgeHeight * 0.5f;
        var x = left;
        x += Badge.DrawLeft(drawList, TextDraw.Upper(Loc.T(L.Details.BadgeTrain)), Styling.AccentGlow, x, midY) + BadgeGap * scale;
        x += Badge.DrawLeft(drawList, GroupLabels.Name(announcement.Group), GroupLabels.Color(announcement.Group), x, midY) + BadgeGap * scale;
        Badge.DrawLeft(drawList, worldBadge, Styling.AccentBlue, x, midY);
        y += badgeHeight + RowGap * scale;

        var label = Loc.T(L.Details.LabelStart);
        TextDraw.SmallCaps(label, new Vector2(left, y), Styling.TextMuted);
        y += TextDraw.SmallCapsSize(label).Y + LabelGap * scale;
        y = DrawStart(announcement, left, y);
        y += RowGap * scale;

        y = DrawActions(plugin, announcement, left, right, y);
        var end = new Vector2(origin.X + width, y + CardPadY * scale);
        drawList.ChannelsSetCurrent(0);
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, GroupLabels.Color(announcement.Group), HeroTint);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, end.Y - origin.Y));
    }

    private float DrawStart(in Announcement announcement, float left, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Vector2 clockSize;
        using (Fonts.PushTitle())
        {
            clockSize = TextDraw.Measure(startClock);
            TextDraw.At(startClock, new Vector2(left, y), Styling.TextStrong);
        }

        var text = TrainTexts.Countdown(ref countdown, announcement, DateTime.UtcNow, out var color);
        var textSize = TextDraw.Measure(text);
        TextDraw.At(text, new Vector2(left + clockSize.X + ClockGap * scale, y + (clockSize.Y - textSize.Y) * 0.5f), color);
        return y + clockSize.Y;
    }

    // Start zone, aetheryte, conductor, post time and, when known, instance and map spot, in a grid of two or three.
    private void DrawFacts()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CardPadX * scale;
        var innerWidth = width - padX * 2f;
        var columns = width >= WideFactsWidth * scale ? 3 : 2;
        var columnGap = FactColumnGap * scale;
        var columnWidth = (innerWidth - columnGap * (columns - 1)) / columns;
        var labelHeight = TextDraw.SmallCapsSize(factLabels[0]).Y;
        var cellHeight = labelHeight + LabelGap * scale + ImGui.GetTextLineHeight();
        var rows = (factCount + columns - 1) / columns;
        var top = origin.Y + CardPadY * scale;

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        for (var index = 0; index < factCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cell = new Vector2(origin.X + padX + column * (columnWidth + columnGap), top + row * (cellHeight + RowGap * scale));
            TextDraw.SmallCaps(factLabels[index], cell, Styling.TextMuted);
            TextDraw.At(TextDraw.Truncate(factValues[index], columnWidth), cell + new Vector2(0f, labelHeight + LabelGap * scale), Styling.TextStrong);
        }

        var contentHeight = rows * cellHeight + Math.Max(0, rows - 1) * RowGap * scale;
        var end = new Vector2(origin.X + width, top + contentHeight + CardPadY * scale);
        drawList.ChannelsSetCurrent(0);
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, end.Y - origin.Y));
    }

    private void DrawBody()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CardPadX * scale;
        var wrapWidth = width - padX * 2f;
        var label = Loc.T(L.Details.LabelAnnouncement);
        var labelHeight = TextDraw.SmallCapsSize(label).Y;
        var empty = body.Length == 0;
        var text = empty ? Loc.T(L.Details.NoBody) : body;
        var textSize = TextDraw.MeasureWrapped(text, wrapWidth);
        var textTop = origin.Y + CardPadY * scale + labelHeight + LabelGap * scale;
        var end = new Vector2(origin.X + width, textTop + textSize.Y + CardPadY * scale);

        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        TextDraw.SmallCaps(label, new Vector2(origin.X + padX, origin.Y + CardPadY * scale), Styling.TextMuted);
        TextDraw.Wrapped(text, new Vector2(origin.X + padX, textTop), wrapWidth, empty ? Styling.TextMuted : Styling.TextSecondary);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, end.Y - origin.Y));
    }
}
