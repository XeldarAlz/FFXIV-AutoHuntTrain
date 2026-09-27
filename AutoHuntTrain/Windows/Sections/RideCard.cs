using AutoHuntTrain.Core;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Windows.Components;
using AutoHuntTrain.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// The idle Ride card: who the ride follows, picked from the players who posted flags lately or typed by hand, and the
// last flag that conductor posted.
internal static class RideCard
{
    private const float PadX = 18f;
    private const float PadY = 14f;
    private const float LabelGap = 8f;
    private const float RowGap = 10f;
    private const float ControlGap = 10f;
    private const float PickerWidth = 230f;
    private const float NameFieldWidth = 230f;
    private const float ClearButtonSize = 26f;
    private const int NameMaxLength = 48;
    private const int MaxEntries = FlagListener.Capacity + 1;
    private const string PickerId = "##aht_ride_conductor_pick";
    private const string NameFieldId = "##aht_ride_conductor_name";
    private const string ClearId = "##aht_ride_conductor_clear";

    private static readonly string[] labels = new string[MaxEntries];
    private static readonly ConductorIdentity[] entries = new ConductorIdentity[MaxEntries];
    private static readonly ConductorIdentity[] recentBuffer = new ConductorIdentity[FlagListener.Capacity];

    private static int entryCount;
    private static int entriesVersion = -1;
    private static ConductorIdentity entriesConductor;
    private static LanguageInfo? entriesLanguage;
    private static string typedName = string.Empty;
    private static CachedText followingText;
    private static CachedText latestFlagText;
    private static CachedText agoText;

    public static void Draw(Plugin plugin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var listener = plugin.Flags;
        var conductor = Conductor.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = PadX * scale;
        var innerWidth = width - padX * 2f;
        var x = origin.X + padX;
        var y = origin.Y + PadY * scale;

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        y = DrawLabel(x, y);
        y = DrawPicker(listener, conductor, x, y);
        y = DrawCaptions(listener, conductor, x, y, innerWidth);

        var end = new Vector2(origin.X + width, y + PadY * scale);
        drawList.ChannelsSetCurrent(0);
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, end.Y - origin.Y));
    }

    private static float DrawLabel(float x, float y)
    {
        var label = Loc.T(L.Ride.CardConductor);
        TextDraw.SmallCaps(label, new Vector2(x, y), Styling.TextMuted);
        return y + TextDraw.SmallCapsSize(label).Y + LabelGap * ImGuiHelpers.GlobalScale;
    }

    private static float DrawPicker(FlagListener listener, in ConductorIdentity conductor, float x, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = ImGui.GetFrameHeight();
        RefreshEntries(listener, conductor);

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        var selected = 0;
        if (Dropdown.Draw(PickerId, labels.AsSpan(0, entryCount), ref selected, PickerWidth) && selected > 0)
        {
            Conductor.Apply(entries[selected]);
        }

        x += PickerWidth * scale + ControlGap * scale;
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.SetNextItemWidth(NameFieldWidth * scale);
        using (SettingsControls.PushFrameColors())
        {
            if (ImGui.InputTextWithHint(NameFieldId, Loc.T(L.Ride.CardTypeHint), ref typedName, NameMaxLength, ImGuiInputTextFlags.EnterReturnsTrue) && Conductor.TryApply(typedName))
            {
                typedName = string.Empty;
            }
        }

        if (conductor.IsSet)
        {
            var clearSize = ClearButtonSize * scale;
            x += NameFieldWidth * scale + ControlGap * scale;
            ImGui.SetCursorScreenPos(new Vector2(x, y + (rowHeight - clearSize) * 0.5f));
            if (IconButton.Draw(FontAwesomeIcon.Times, ClearId, clearSize, Styling.TextDim, Loc.T(L.Ride.CardClear)))
            {
                Conductor.Clear();
            }
        }

        return y + rowHeight + RowGap * scale;
    }

    private static float DrawCaptions(FlagListener listener, in ConductorIdentity conductor, float x, float y, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!conductor.IsSet)
        {
            var hint = Loc.T(L.Ride.CardNone);
            TextDraw.Wrapped(hint, new Vector2(x, y), width, Styling.TextMuted);
            return y + TextDraw.MeasureWrapped(hint, width).Y;
        }

        if (!followingText.TryGet(entriesVersion, out var following))
        {
            following = followingText.Set(entriesVersion, Loc.T(L.Ride.CardFollowing, Conductor.Describe(conductor)));
        }

        TextDraw.At(TextDraw.Truncate(following, width), new Vector2(x, y), Styling.TextSecondary);
        y += ImGui.GetTextLineHeight() + 4f * scale;

        using (Fonts.PushCaption())
        {
            var latest = LatestFlagText(listener, conductor);
            TextDraw.At(TextDraw.Truncate(latest, width), new Vector2(x, y), Styling.TextDim);
            return y + ImGui.GetTextLineHeight();
        }
    }

    private static string LatestFlagText(FlagListener listener, in ConductorIdentity conductor)
    {
        if (!listener.TryLatestBy(conductor, out var post))
        {
            return Loc.T(L.Ride.CardNoFlagYet);
        }

        var seconds = (long)(DateTime.UtcNow - post.PostedAtUtc).TotalSeconds;
        var key = HashCode.Combine(post.PostedAtUtc.Ticks, seconds);
        if (latestFlagText.TryGet(key, out var text))
        {
            return text;
        }

        var ago = agoText.Get(seconds, static age => Ago(age));
        return latestFlagText.Set(key, Loc.T(L.Ride.CardLatestFlag, CurrentFlag.Line(post), ago));
    }

    private static string Ago(long seconds)
    {
        if (seconds < TimeUnits.SecondsPerMinute)
        {
            return Loc.T(L.Ride.SecondsAgo, seconds);
        }

        var minutes = seconds / TimeUnits.SecondsPerMinute;
        return minutes < TimeUnits.MinutesPerHour ? Loc.T(L.History.MinutesAgo, minutes) : Loc.T(L.History.HoursAgo, minutes / TimeUnits.MinutesPerHour);
    }

    // The first entry names the conductor, or invites a pick; the rest are the recent posters, newest first.
    private static void RefreshEntries(FlagListener listener, in ConductorIdentity conductor)
    {
        var language = Loc.Current;
        if (listener.Version == entriesVersion && conductor.SameAs(entriesConductor) && ReferenceEquals(language, entriesLanguage))
        {
            return;
        }

        entriesVersion = listener.Version;
        entriesConductor = conductor;
        entriesLanguage = language;
        entryCount = 0;
        entries[entryCount] = conductor;
        labels[entryCount++] = conductor.IsSet ? Conductor.Describe(conductor) : Loc.T(L.Ride.CardPickRecent);

        var recent = listener.RecentSenders(recentBuffer);
        for (var index = 0; index < recent; index++)
        {
            var identity = recentBuffer[index];
            if (conductor.IsSet && identity.SameAs(conductor))
            {
                continue;
            }

            entries[entryCount] = identity;
            labels[entryCount++] = Conductor.Describe(identity);
        }
    }
}
