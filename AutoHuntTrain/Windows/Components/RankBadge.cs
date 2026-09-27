using AutoHuntTrain.Core.Marks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Components;

internal static class RankBadge
{
    public const float DefaultSize = 22f;

    private const float Rounding = 6f;
    private const string LetterB = "B";
    private const string LetterA = "A";
    private const string LetterS = "S";

    public static Vector4 Color(HuntMarkRank rank) => rank switch
    {
        HuntMarkRank.B => Styling.AccentBlue,
        HuntMarkRank.A => Styling.AccentAmber,
        _ => Styling.AccentNebula,
    };

    public static string Letter(HuntMarkRank rank) => rank switch
    {
        HuntMarkRank.B => LetterB,
        HuntMarkRank.A => LetterA,
        _ => LetterS,
    };

    // Returns the badge's width, so the caller can place what follows it.
    public static float Draw(ImDrawListPtr drawList, HuntMarkRank rank, float leftX, float midY, float size = DefaultSize)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var side = size * scale;
        var rounding = Rounding * scale;
        var min = new Vector2(leftX, midY - side * 0.5f);
        var max = min + new Vector2(side, side);
        var fill = Color(rank);
        Paint.Fill(drawList, min, max, fill, rounding);
        Paint.TopLight(drawList, min, max, rounding, 0.18f);
        using (Fonts.PushCaption())
        {
            TextDraw.Middle(Letter(rank), min, max, Styling.ForegroundOn(fill));
        }

        return side;
    }
}
