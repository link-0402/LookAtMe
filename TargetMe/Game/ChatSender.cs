using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace TargetMe.Game;

/// <summary>Sends a line through the chat box exactly as if the player had typed it.</summary>
public static unsafe class ChatSender
{
    private const int MaxBytes = 500;
    // The same set of allowed payloads the game uses when sanitising typed chat.
    private const AllowedEntities TypedChatEntities = (AllowedEntities)0x27F;

    public static bool Send(string line, out string error)
    {
        error = string.Empty;
        line = line.Replace("\r", " ").Replace("\n", " ").Trim();
        if (line.Length == 0)
        {
            error = "Nothing to send.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(line) > MaxBytes)
        {
            error = "That line is too long for the game's chat.";
            return false;
        }

        var uiModule = UIModule.Instance();
        if (uiModule == null)
        {
            error = "Chat isn't available right now.";
            return false;
        }

        var str = Utf8String.FromString(line);
        try
        {
            str->SanitizeString(TypedChatEntities);
            if (str->ToString() != line)
            {
                error = "The line has characters the game chat can't send.";
                return false;
            }

            uiModule->ProcessChatBoxEntry(str);
            return true;
        }
        finally
        {
            str->Dtor(true);
        }
    }
}
