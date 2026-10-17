using System;
using System.Text;

namespace TargetMe.Core;

/// <summary>A main key (a Windows virtual-key code, or Mouse 4 / Mouse 5) plus exact Ctrl/Shift/Alt state.</summary>
public sealed record HotkeyBinding
{
    public const int VkShift = 0x10;
    public const int VkControl = 0x11;
    public const int VkMenu = 0x12;
    private const int VkMouse4 = 0x05;
    private const int VkMouse5 = 0x06;

    /// <summary>Windows virtual-key code; 0 means no key is bound.</summary>
    public int Key { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }

    public bool IsEmpty => Key == 0;
    public bool IsMouse => Key is VkMouse4 or VkMouse5;

    /// <summary>True when the main key is down and each modifier's state matches the binding exactly.</summary>
    public bool IsHeld(Func<int, bool> down) =>
        !IsEmpty
        && down(Key)
        && down(VkControl) == Ctrl
        && down(VkShift) == Shift
        && down(VkMenu) == Alt;

    public string Display(Func<int, string> keyName)
    {
        if (IsEmpty)
            return "Not set";

        var text = new StringBuilder();
        if (Ctrl)
            text.Append("Ctrl+");
        if (Shift)
            text.Append("Shift+");
        if (Alt)
            text.Append("Alt+");
        text.Append(Key switch
        {
            VkMouse4 => "Mouse 4",
            VkMouse5 => "Mouse 5",
            _ => keyName(Key),
        });
        return text.ToString();
    }

    /// <summary>Ctrl, Shift and Alt (either side) can't be the main key.</summary>
    public static bool IsModifier(int vk) => vk is >= VkShift and <= VkMenu or >= 0xA0 and <= 0xA5;
}
