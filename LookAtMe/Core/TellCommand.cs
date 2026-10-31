namespace LookAtMe.Core;

public static class TellCommand
{
    /// <summary>"/tell Name@World", or "/tell Name" when the world isn't known.</summary>
    public static string For(string name, string worldName) =>
        worldName.Length > 0 ? $"/tell {name}@{worldName}" : $"/tell {name}";
}
