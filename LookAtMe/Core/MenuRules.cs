namespace LookAtMe.Core;

public static class MenuRules
{
    /// <summary>
    /// Whether the Focus Camera / Stop Focusing item is usable. Starting needs the viewer nearby,
    /// and not in combat when focus would stop on combat right away. Stopping always works.
    /// </summary>
    public static bool CanFocusCamera(bool loaded, bool focused, bool inCombat, bool stopOnCombat) =>
        focused || (loaded && !(inCombat && stopOnCombat));
}
