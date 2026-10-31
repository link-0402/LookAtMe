using LookAtMe.Core;

namespace LookAtMe.Tests;

public class EscGateTests
{
    [Fact]
    public void RisingEdgeStops()
    {
        var gate = new EscGate();
        Assert.False(gate.ShouldStop(escDown: false, textInputActive: false));
        Assert.True(gate.ShouldStop(escDown: true, textInputActive: false));
    }

    [Fact]
    public void HeldDoesNotRepeat()
    {
        var gate = new EscGate();
        gate.ShouldStop(true, false);
        Assert.False(gate.ShouldStop(true, false));
    }

    [Fact]
    public void TypingBlocks()
    {
        var gate = new EscGate();
        gate.ShouldStop(false, true);
        Assert.False(gate.ShouldStop(true, true));
    }

    [Fact]
    public void ClosingChatWithEscDoesNotStop()
    {
        // Esc closes the chat input: on that frame the input is already inactive.
        var gate = new EscGate();
        gate.ShouldStop(false, true);
        Assert.False(gate.ShouldStop(true, false));
    }

    [Fact]
    public void StopsAgainAfterRelease()
    {
        var gate = new EscGate();
        gate.ShouldStop(true, false);
        gate.ShouldStop(false, false);
        Assert.True(gate.ShouldStop(true, false));
    }
}
