namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Notify
    {
        public static readonly LocString SettingsGroup = new("notify.settings.group", "Notifications");
        public static readonly LocString OpenWindow = new("notify.settings.openWindow", "Open the Train page");
        public static readonly LocString OpenWindowHelp = new("notify.settings.openWindowHelp", "When a train you could ride is announced, open this window on the Train page. While you type in a text box it waits for you to finish, for up to 30 seconds. Nothing happens during a ride, a duty or a cutscene, or while auto-ride is snoozed.");
        public static readonly LocString FlashTaskbar = new("notify.settings.flashTaskbar", "Flash the taskbar in the background");
        public static readonly LocString FlashTaskbarHelp = new("notify.settings.flashTaskbarHelp", "When a train you could ride is announced while the game is in the background, flash its taskbar button until you switch back to it.");
        public static readonly LocString Test = new("notify.settings.test", "Try it out");
        public static readonly LocString TestHelp = new("notify.settings.testHelp", "Opens the Train page and flashes the taskbar a few times, for whichever of the two is on.");
        public static readonly LocString TestButton = new("notify.settings.testButton", "Test");
        public static readonly LocString HuntAlertsNote = new("notify.settings.huntAlertsNote", "The chat alert, the sound and the banner come from HuntAlerts and are set in its own settings. These two add what it cannot do.");
    }
}
