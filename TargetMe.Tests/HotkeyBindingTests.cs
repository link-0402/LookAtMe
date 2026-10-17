using TargetMe.Core;

namespace TargetMe.Tests;

public class HotkeyBindingTests
{
    private const int F = 0x46;
    private const int Ctrl = 0x11;
    private const int Shift = 0x10;

    private static Func<int, bool> Down(params int[] keys) => vk => keys.Contains(vk);

    private static string KeyName(int vk) => vk == F ? "F" : "?";

    [Fact]
    public void EmptyIsNeverHeld()
    {
        Assert.False(new HotkeyBinding().IsHeld(_ => true));
    }

    [Fact]
    public void HeldNeedsExactModifiers()
    {
        var binding = new HotkeyBinding { Key = F, Ctrl = true };

        Assert.True(binding.IsHeld(Down(Ctrl, F)));
        Assert.False(binding.IsHeld(Down(Ctrl, Shift, F)));
        Assert.False(binding.IsHeld(Down(F)));
    }

    [Fact]
    public void PlainKeyNotHeldWithModifier()
    {
        var binding = new HotkeyBinding { Key = F };

        Assert.False(binding.IsHeld(Down(Ctrl, F)));
        Assert.True(binding.IsHeld(Down(F)));
    }

    [Fact]
    public void PhaseUpWhenMainKeyUpOrEmpty()
    {
        Assert.Equal(KeyPhase.Up, new HotkeyBinding { Key = F, Ctrl = true }.Phase(Down(Ctrl)));
        Assert.Equal(KeyPhase.Up, new HotkeyBinding().Phase(_ => true));
    }

    [Fact]
    public void PhaseChordWithExactModifiers()
    {
        Assert.Equal(KeyPhase.Chord, new HotkeyBinding { Key = F, Ctrl = true }.Phase(Down(Ctrl, F)));
        Assert.Equal(KeyPhase.Chord, new HotkeyBinding { Key = F }.Phase(Down(F)));
    }

    [Fact]
    public void PhaseSpoiledByUnboundModifier()
    {
        Assert.Equal(KeyPhase.Spoiled, new HotkeyBinding { Key = F, Ctrl = true }.Phase(Down(Ctrl, Shift, F)));
        Assert.Equal(KeyPhase.Spoiled, new HotkeyBinding { Key = F }.Phase(Down(0x12, F)));
    }

    [Fact]
    public void PhaseMainOnlyWhenBoundModifierReleased()
    {
        Assert.Equal(KeyPhase.MainOnly, new HotkeyBinding { Key = F, Ctrl = true, Shift = true }.Phase(Down(Ctrl, F)));
    }

    [Fact]
    public void MouseButtonsAreMouse()
    {
        Assert.True(new HotkeyBinding { Key = 0x05 }.IsMouse);
        Assert.True(new HotkeyBinding { Key = 0x06 }.IsMouse);
        Assert.False(new HotkeyBinding { Key = F }.IsMouse);
    }

    public static TheoryData<HotkeyBinding, string> DisplayCases => new()
    {
        { new HotkeyBinding { Key = F, Ctrl = true, Shift = true }, "Ctrl+Shift+F" },
        { new HotkeyBinding { Key = 0x06, Alt = true }, "Alt+Mouse 5" },
        { new HotkeyBinding { Key = 0x05 }, "Mouse 4" },
        { new HotkeyBinding(), "Not set" },
    };

    [Theory]
    [MemberData(nameof(DisplayCases))]
    public void DisplayText(HotkeyBinding binding, string expected)
    {
        Assert.Equal(expected, binding.Display(KeyName));
    }

    [Theory]
    [InlineData(0x10, true)]
    [InlineData(0x11, true)]
    [InlineData(0x12, true)]
    [InlineData(0xA0, true)]
    [InlineData(0xA5, true)]
    [InlineData(0x46, false)]
    [InlineData(0x05, false)]
    public void ModifiersAreRejected(int vk, bool expected)
    {
        Assert.Equal(expected, HotkeyBinding.IsModifier(vk));
    }
}
