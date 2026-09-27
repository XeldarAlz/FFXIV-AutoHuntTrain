namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Notify
    {
        public static readonly LocString SettingsGroup = new("notify.settings.group", "Notifications");
        public static readonly LocString OpenWindow = new("notify.settings.openWindow", "Open the Train page");
        public static readonly LocString OpenWindowHelp = new("notify.settings.openWindowHelp", "When a train on your allowed data centers that you could ride is announced, open this window on the Train page. While you type in a text box it waits for you to finish, for up to 30 seconds. Nothing happens during a ride, a duty or a cutscene, or while auto-join is snoozed.");
        public static readonly LocString FlashTaskbar = new("notify.settings.flashTaskbar", "Flash the taskbar in the background");
        public static readonly LocString FlashTaskbarHelp = new("notify.settings.flashTaskbarHelp", "When a train on your allowed data centers that you could ride is announced while the game is in the background, flash its taskbar button until you switch back to it.");
        public static readonly LocString ChatAlert = new("notify.settings.chatAlert", "Post a chat line");
        public static readonly LocString ChatAlertHelp = new("notify.settings.chatAlertHelp", "When a train the Upcoming trains list shows is announced, post one line in chat with a link to its details. It follows the list's view, and stays quiet during a ride, a duty or a cutscene, or while auto-join is snoozed.");
        public static readonly LocString ChatSound = new("notify.settings.chatSound", "Chat line sound");
        public static readonly LocString ChatSoundHelp = new("notify.settings.chatSoundHelp", "Play one of the game's chat sounds with the chat line.");
        public static readonly LocString ChatSoundEffect = new("notify.settings.chatSoundEffect", "Sound effect");
        public static readonly LocString ChatSoundEffectHelp = new("notify.settings.chatSoundEffectHelp", "Which of the game's sixteen chat sounds plays, the same as <se.1> to <se.16> in a chat message. Changing it plays it once.");
        public static readonly LocString SoundFormat = new("notify.settings.soundFormat", "<se.%d>");
        public static readonly LocString ChatLine = new("notify.chat.line", "{0} train on {1} ({2}, {3}) {4}");
        public static readonly LocString ChatConductor = new("notify.chat.conductor", ", conductor {0}");
        public static readonly LocString ChatDetails = new("notify.chat.details", "Details");
        public static readonly LocString Test = new("notify.settings.test", "Try it out");
        public static readonly LocString TestHelp = new("notify.settings.testHelp", "Opens the Train page and flashes the taskbar a few times, for whichever of the two is on.");
        public static readonly LocString TestButton = new("notify.settings.testButton", "Test");
        public static readonly LocString HuntAlertsNote = new("notify.settings.huntAlertsNote", "HuntAlerts only detects the trains; the alerts above come from this plugin. Turn HuntAlerts' own chat alert and banner off in its settings, or each train is announced twice.");
    }
}
