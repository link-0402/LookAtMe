using System;

namespace TargetMe.Core;

/// <summary>Someone who has targeted the local player this session.</summary>
public sealed class ViewerRecord
{
    public string Name { get; init; } = string.Empty;
    public uint WorldId { get; init; }
    public string WorldName { get; set; } = string.Empty;
    public ulong ContentId { get; set; }

    private string? key;

    // Name and WorldId are init-only, so the key never changes once built.
    public string Key => key ??= MakeKey(Name, WorldId);

    /// <summary>Their target is the local player right now.</summary>
    public bool IsTargeting { get; set; }

    /// <summary>They are in the object table (close enough to interact with).</summary>
    public bool IsLoaded { get; set; }

    public ulong GameObjectId { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public EmoteChips Emotes { get; } = new();

    /// <summary>Name plus home world id, so same-named players from different worlds stay apart.</summary>
    public static string MakeKey(string name, uint worldId) => $"{name}@{worldId}";
}
