namespace LookAtMe.Core;

/// <summary>
/// Turns per-frame Esc and text-input state into a single "stop" signal. Ignores Esc while the
/// player is typing, including the frame where Esc itself closes the chat input.
/// </summary>
public sealed class EscGate
{
    private bool escWasDown;
    private bool textInputWasActive;

    public bool ShouldStop(bool escDown, bool textInputActive)
    {
        var pressed = escDown && !escWasDown;
        var typing = textInputActive || textInputWasActive;
        escWasDown = escDown;
        textInputWasActive = textInputActive;
        return pressed && !typing;
    }
}
