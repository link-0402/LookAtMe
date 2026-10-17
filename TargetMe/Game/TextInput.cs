using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace TargetMe.Game;

public static unsafe class TextInput
{
    /// <summary>True while the player is typing: in the game's chat or text fields, or in an ImGui input.</summary>
    public static bool IsActive()
    {
        var atk = RaptureAtkModule.Instance();
        return (atk != null && atk->AtkModule.IsTextInputActive()) || ImGui.GetIO().WantTextInput;
    }
}
