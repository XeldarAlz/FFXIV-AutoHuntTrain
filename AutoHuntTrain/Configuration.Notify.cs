namespace AutoHuntTrain;

public sealed partial class Configuration
{
    public bool NotifyOpenWindow { get; set; } = true;

    public bool NotifyFlashTaskbar { get; set; } = true;

    public bool ChatAlert { get; set; } = true;

    public bool ChatAlertSound { get; set; } = false;

    // The game's own chat sound effects, <se.1> to <se.16>.
    public int ChatAlertSoundEffect { get; set; } = 1;
}
