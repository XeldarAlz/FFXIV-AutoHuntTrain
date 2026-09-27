using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System.Numerics;

namespace AutoHuntTrain.Windows;

// Ported from HuntAlerts' WorldArrowWindow: a small overlay whose arrow points along the ground at the waypoint while
// the character is in its zone on its world. The camera's yaw is matched to the screen's own axes on the fly, because
// its sign and zero differ from the world's; right-click clears the arrow, and reaching the spot clears it too.
internal sealed class NavArrowWindow : Window
{
    private const float AreaSize = 80f;
    private const float ArrowRadius = 28f;
    private const float OutlineGrow = 2f;
    private const float TipOutlineGrow = 2.5f;
    private const float LabelPadX = 6f;
    private const float LabelPadY = 2f;
    private const float LabelGap = 6f;
    private const float LabelRounding = 6f;
    private const float ArrivedRingThickness = 3f;
    private const float ArrivedDotRadius = 5f;
    private const float ArrivalRadius = 12f;
    private const int ArrivalHoldMs = 3_000;
    // A yaw change this small, in radians, is noise rather than the camera turning, so it cannot calibrate anything.
    private const float MinimumYawStep = 0.02f;
    private const float MinimumAxisStep = 0.005f;
    private const float MinimumDeterminant = 0.01f;
    private const string OverlayId = "##aht_nav_arrow";

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly Vector2 DefaultPosition = new(200f, 200f);

    private float handedness = 1f;
    private float yawSign = 1f;
    private float yawOffset;
    private bool yawCalibrated;
    private float previousYaw = float.NaN;
    private float previousAxisAngle = float.NaN;
    private long arrivedAtMs;
    private CachedText distanceText;
    private CachedText tooltipText;

    public NavArrowWindow() : base(OverlayId, OverlayFlags)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Position = DefaultPosition;
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions()
    {
        if (!NavWaypoint.IsActive)
        {
            NavWaypoint.Clear();
            arrivedAtMs = 0;
            return false;
        }

        return Svc.ClientState.IsLoggedIn
            && Svc.Objects.LocalPlayer is not null
            && Svc.ClientState.TerritoryType == NavWaypoint.TerritoryId
            && Worlds.TryCurrent(out var world)
            && world.Id == NavWaypoint.WorldId;
    }

    public override void Draw()
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var area = AreaSize * scale;
        var origin = ImGui.GetCursorScreenPos();
        var center = origin + new Vector2(area * 0.5f);
        var target = NavWaypoint.WorldPosition;
        var deltaX = target.X - player.Position.X;
        var deltaZ = target.Z - player.Position.Z;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        var hovered = ImGui.IsWindowHovered();
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            NavWaypoint.Clear();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, area * 0.5f, Paint.Col(hovered ? Styling.WithAlpha(Styling.TextStrong, 0.25f) : Styling.WithAlpha(Styling.WindowBg, 0.1f)));
        var arrived = distance < ArrivalRadius;
        if (arrived)
        {
            DrawArrived(drawList, center, scale);
        }
        else
        {
            arrivedAtMs = 0;
            DrawArrow(drawList, center, Rotation(player.Position, deltaX, deltaZ), scale);
        }

        ImGui.Dummy(new Vector2(area));
        DrawLabel(drawList, origin.X, area, arrived ? Loc.T(L.Details.NavArrived) : DistanceText(distance), scale);
        if (hovered)
        {
            Tooltip.Show(TooltipText());
        }

        HoldArrival(arrived);
    }

    // The arrow shows it has arrived for a moment before it goes away.
    private void HoldArrival(bool arrived)
    {
        if (!arrived)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (arrivedAtMs == 0)
        {
            arrivedAtMs = now;
            return;
        }

        if (now - arrivedAtMs < ArrivalHoldMs)
        {
            return;
        }

        arrivedAtMs = 0;
        NavWaypoint.Clear();
    }

    // The ground's X and Z axes are projected to the screen around the character. Their inverse gives the camera's
    // heading in world terms, which calibrates the camera's own yaw; the calibrated yaw keeps the arrow right when
    // the character is off screen and the projection falls apart.
    private float Rotation(Vector3 position, float deltaX, float deltaZ)
    {
        Svc.GameGui.WorldToScreen(position, out var screenOrigin);
        Svc.GameGui.WorldToScreen(position + Vector3.UnitX, out var screenX);
        Svc.GameGui.WorldToScreen(position + Vector3.UnitZ, out var screenZ);
        var axisX = screenX - screenOrigin;
        var axisZ = screenZ - screenOrigin;
        var determinant = axisX.X * axisZ.Y - axisZ.X * axisX.Y;
        var yaw = CameraYaw();
        float? axisAngle = null;
        if (determinant > MinimumDeterminant)
        {
            var upX = axisZ.X / determinant;
            var upZ = -axisX.X / determinant;
            var rightX = axisZ.Y / determinant;
            var rightZ = -axisX.Y / determinant;
            var upAngle = MathF.Atan2(upX, upZ);
            handedness = NormalizeAngle(MathF.Atan2(rightX, rightZ) - upAngle) > 0f ? 1f : -1f;
            axisAngle = upAngle;
        }

        if (yaw is { } currentYaw && axisAngle is { } currentAxis)
        {
            Calibrate(currentYaw, currentAxis);
        }

        var targetAngle = MathF.Atan2(deltaX, deltaZ);
        if (yaw is { } calibratedYaw && yawCalibrated)
        {
            return handedness * NormalizeAngle(targetAngle - NormalizeAngle(yawSign * calibratedYaw + yawOffset));
        }

        if (axisAngle is { } fallbackAxis)
        {
            return handedness * NormalizeAngle(targetAngle - fallbackAxis);
        }

        var onScreen = axisX * deltaX + axisZ * deltaZ;
        return MathF.Atan2(onScreen.Y, onScreen.X) + MathF.PI * 0.5f;
    }

    private void Calibrate(float yaw, float axisAngle)
    {
        if (!float.IsNaN(previousYaw))
        {
            var yawStep = NormalizeAngle(yaw - previousYaw);
            var axisStep = NormalizeAngle(axisAngle - previousAxisAngle);
            if (MathF.Abs(yawStep) > MinimumYawStep && MathF.Abs(axisStep) > MinimumAxisStep)
            {
                yawSign = yawStep * axisStep > 0f ? 1f : -1f;
                yawOffset = NormalizeAngle(axisAngle - yawSign * yaw);
                yawCalibrated = true;
            }
        }

        previousYaw = yaw;
        previousAxisAngle = axisAngle;
    }

    private static void DrawArrow(ImDrawListPtr drawList, Vector2 center, float angle, float scale)
    {
        var radius = ArrowRadius * scale;
        var grow = OutlineGrow * scale;
        var tip = center + Rotate(new Vector2(0f, -radius), angle);
        var left = center + Rotate(new Vector2(-radius * 0.72f, radius * 0.55f), angle);
        var notch = center + Rotate(new Vector2(0f, radius * 0.28f), angle);
        var right = center + Rotate(new Vector2(radius * 0.72f, radius * 0.55f), angle);
        var tipOutline = center + Rotate(new Vector2(0f, -radius - TipOutlineGrow * scale), angle);
        var leftOutline = center + Rotate(new Vector2(-radius * 0.72f - grow, radius * 0.55f + grow), angle);
        var notchOutline = center + Rotate(new Vector2(0f, radius * 0.28f + grow), angle);
        var rightOutline = center + Rotate(new Vector2(radius * 0.72f + grow, radius * 0.55f + grow), angle);
        drawList.AddQuadFilled(tipOutline, leftOutline, notchOutline, rightOutline, Paint.Col(Styling.WithAlpha(Styling.InkOnGlow, 0.7f)));
        drawList.AddTriangleFilled(tip, left, notch, Paint.Col(Styling.AccentGlow));
        drawList.AddTriangleFilled(tip, notch, right, Paint.Col(Styling.AccentNebula));
    }

    private static void DrawArrived(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = ArrowRadius * 0.8f * scale;
        drawList.AddCircle(center, radius, Paint.Col(Styling.WithAlpha(Styling.InkOnGlow, 0.8f)), 0, (ArrivedRingThickness + 2f) * scale);
        drawList.AddCircle(center, radius, Paint.Col(Styling.AccentGlow), 0, ArrivedRingThickness * scale);
        drawList.AddCircleFilled(center, ArrivedDotRadius * scale, Paint.Col(Styling.AccentGlow));
    }

    private static void DrawLabel(ImDrawListPtr drawList, float left, float area, string text, float scale)
    {
        using (Fonts.PushCaption())
        {
            var size = TextDraw.Measure(text);
            var padX = LabelPadX * scale;
            var padY = LabelPadY * scale;
            var start = new Vector2(left + (area - size.X) * 0.5f - padX, ImGui.GetCursorScreenPos().Y);
            var end = start + new Vector2(size.X + padX * 2f, size.Y + padY * 2f);
            Paint.Fill(drawList, start, end, Styling.WithAlpha(Styling.WindowBg, 0.8f), LabelRounding * scale);
            TextDraw.At(text, start + new Vector2(padX, padY), Styling.AccentGlowSoft);
            ImGui.Dummy(new Vector2(area, size.Y + padY * 2f + LabelGap * scale));
        }
    }

    private string DistanceText(float distance)
    {
        var yalms = (int)distance;
        if (distanceText.TryGet(yalms, out var text))
        {
            return text;
        }

        return distanceText.Set(yalms, Loc.T(L.Details.NavDistance, yalms));
    }

    private string TooltipText()
    {
        var key = HashCode.Combine(NavWaypoint.AnnouncementId, NavWaypoint.MapCoordinates);
        if (tooltipText.TryGet(key, out var text))
        {
            return text;
        }

        return tooltipText.Set(key, Loc.T(L.Details.NavTooltip, TrainRelay.FormatCoordinates(NavWaypoint.MapCoordinates)));
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.PI * 2f;
        }

        while (angle <= -MathF.PI)
        {
            angle += MathF.PI * 2f;
        }

        return angle;
    }

    private static unsafe float? CameraYaw()
    {
        var manager = CameraManager.Instance();
        if (manager is null)
        {
            return null;
        }

        var camera = manager->GetActiveCamera();
        return camera is null ? null : camera->DirH;
    }
}
