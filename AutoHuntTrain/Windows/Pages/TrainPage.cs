using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using AutoHuntTrain.Windows.Sections;
using AutoHuntTrain.Windows.Shell;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Pages;

internal sealed class TrainPage
{
    private const float SwitchRevealMs = 320f;
    private const float EmptyCardHeight = 118f;
    private const float EmptyCardPadX = 24f;
    private const float ViewPickerHeight = 32f;
    private const float ViewPickerGap = 10f;
    private const string ViewPickerId = "##aht_train_view";

    private static readonly Segmented.Item[] viewItems = new Segmented.Item[3];

    public void Draw(Plugin plugin, AppWindow window)
    {
        var controller = plugin.Controller;
        var running = controller.Running;

        using var reveal = Motion.PushSwitch("##aht_train_state", running, SwitchRevealMs);
        if (running)
        {
            RunningPanel.Draw(controller);
            return;
        }

        DrawIdle(plugin, window);
    }

    private static void DrawIdle(Plugin plugin, AppWindow window)
    {
        if (Headline.Draw(plugin.Controller, plugin.History))
        {
            window.Show(AppWindow.Page.Plugins);
        }

        Styling.VSpace(20f);
        SectionTitle(Loc.T(L.Train.Upcoming), FeedStatusLine.Get(plugin.Configuration));
        DrawViewPicker(plugin.Configuration);
        if (plugin.Feed.Count == 0)
        {
            EmptyCard(FontAwesomeIcon.Train, Loc.T(L.Train.UpcomingEmpty));
        }
        else if (FeedCard.ListedCount(plugin.Feed) == 0)
        {
            EmptyCard(FontAwesomeIcon.Filter, Loc.T(HiddenText(plugin.Configuration.TrainListView)));
        }
        else
        {
            FeedCard.Draw(plugin);
        }

        Styling.VSpace(16f);
        SectionTitle(Loc.T(L.Train.Ride));
        RideCard.Draw(plugin);
        Styling.VSpace(12f);
    }

    private static void DrawViewPicker(Configuration configuration)
    {
        viewItems[0] = new Segmented.Item(FontAwesomeIcon.Server, Loc.T(L.Feed.ViewMyDataCenters));
        viewItems[1] = new Segmented.Item(FontAwesomeIcon.MapMarkedAlt, Loc.T(L.Feed.ViewMyRegion));
        viewItems[2] = new Segmented.Item(FontAwesomeIcon.Globe, Loc.T(L.Feed.ViewEverywhere));
        var width = MathF.Min(Segmented.PreferredWidth(viewItems), ImGui.GetContentRegionAvail().X);
        var selected = (int)configuration.TrainListView;
        if (Segmented.Draw(ViewPickerId, viewItems, ref selected, height: ViewPickerHeight, width: width))
        {
            configuration.TrainListView = (TrainListView)selected;
            configuration.SaveDebounced();
        }

        Styling.VSpace(ViewPickerGap);
    }

    // Everywhere shows every train, so only the two narrower views can hide the whole list.
    private static LocString HiddenText(TrainListView view)
        => view == TrainListView.MyRegion ? L.Feed.AllHiddenRegion : L.Feed.AllHidden;

    private static void SectionTitle(string text, string? trailing = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var size = TextDraw.SectionTitleSize(text);
        TextDraw.SectionTitle(text, origin, Styling.TextStrong);
        if (trailing is not null)
        {
            using (Fonts.PushCaption())
            {
                var trailingSize = TextDraw.Measure(trailing);
                TextDraw.At(trailing, new Vector2(origin.X + width - trailingSize.X, origin.Y + size.Y - trailingSize.Y - 2f * scale), Styling.TextMuted);
            }
        }

        ImGui.Dummy(new Vector2(width, size.Y + 8f * scale));
    }

    private static void EmptyCard(FontAwesomeIcon icon, string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, EmptyCardHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);

        var padX = EmptyCardPadX * scale;
        var wrapWidth = size.X - padX * 2f;
        var textSize = TextDraw.MeasureWrapped(text, wrapWidth);
        var iconHeight = 26f * scale;
        var gap = 12f * scale;
        var top = origin.Y + (size.Y - iconHeight - gap - textSize.Y) * 0.5f;
        var center = new Vector2((origin.X + end.X) * 0.5f, top + iconHeight * 0.5f);
        ProgressRing.CenterIcon(center, icon, Styling.TextMuted, iconHeight);
        TextDraw.Wrapped(text, new Vector2(center.X - textSize.X * 0.5f, top + iconHeight + gap), wrapWidth, Styling.TextMuted);
        ImGui.Dummy(size);
    }
}
