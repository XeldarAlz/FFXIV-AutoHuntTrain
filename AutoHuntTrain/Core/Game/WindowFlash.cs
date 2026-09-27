using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoHuntTrain.Core.Game;

internal static unsafe partial class WindowFlash
{
    // FLASHW_TRAY: the taskbar button only, never the caption.
    private const uint FlashTray = 0x2;
    // FLASHW_TIMERNOFG: keep flashing until the window comes to the foreground.
    private const uint FlashUntilForeground = 0xC;
    private const uint BriefFlashCount = 3;

    public static bool GameInBackground()
    {
        var framework = Framework.Instance();
        return framework is not null && framework->WindowInactive;
    }

    public static void FlashUntilFocused() => Flash(FlashTray | FlashUntilForeground, 0);

    // A window already in the foreground ignores the until-focused flash, so a test from the settings flashes a set number of times.
    public static void FlashBriefly() => Flash(FlashTray, BriefFlashCount);

    private static void Flash(uint flags, uint count)
    {
        nint window;
        using (var process = Process.GetCurrentProcess())
        {
            window = process.MainWindowHandle;
        }

        if (window == nint.Zero)
        {
            RunLog.Debug("Notify: the game window has no handle yet; the taskbar is not flashed");
            return;
        }

        var info = new FlashInfo
        {
            Size = (uint)sizeof(FlashInfo),
            Window = window,
            Flags = flags,
            Count = count,
            TimeoutMs = 0,
        };
        _ = FlashWindowEx(ref info);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint TimeoutMs;
    }
}
