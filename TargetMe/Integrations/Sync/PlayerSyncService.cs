using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace TargetMe.Integrations.Sync;

/// <summary>PlayerSync (internal name MareSempiterne), mirroring its own right-click "Pause" and unpause.</summary>
public sealed class PlayerSyncService : ReflectedSyncService
{
    private object pairManager = null!;
    private object api = null!;
    private object manualReason = null!;
    private MethodInfo hashCid = null!;
    private FieldInfo allClientPairs = null!;
    private MethodInfo pauseAsync = null!;
    private MethodInfo? unPauseAsync;
    private MethodInfo? stickyPauseAndRemove;
    private PropertyInfo ident = null!;
    private PropertyInfo isPaused = null!;
    private PropertyInfo userData = null!;
    private PropertyInfo isConnected = null!;

    public PlayerSyncService()
        : base("PlayerSync", "MareSempiterne")
    {
    }

    protected override void Bind(object plugin)
    {
        var assembly = plugin.GetType().Assembly;
        var newPairManager = SyncReflection.GetService(plugin, "MareSynchronos.PlayerData.Pairs.PairManager");
        var newApi = SyncReflection.GetService(plugin, "MareSynchronos.WebAPI.ApiController");
        var util = SyncReflection.Type(assembly, "MareSynchronos.Services.DalamudUtilService");
        var pair = SyncReflection.Type(assembly, "MareSynchronos.PlayerData.Pairs.Pair");
        var reason = SyncReflection.Type(assembly, "MareSynchronos.MareConfiguration.Models.PauseReason");
        var duration = SyncReflection.Type(assembly, "MareSynchronos.MareConfiguration.Models.PauseDuration");
        var apiType = newApi.GetType();

        hashCid = SyncReflection.Method(util, "GetHashedCIDFromPlayerPointer", typeof(nint));
        allClientPairs = SyncReflection.Field(newPairManager.GetType(), "_allClientPairs");
        ident = SyncReflection.Property(pair, "Ident");
        isPaused = SyncReflection.Property(pair, "IsPaused");
        userData = SyncReflection.Property(pair, "UserData");
        isConnected = SyncReflection.Property(apiType, "IsConnected");
        manualReason = Enum.Parse(reason, "Manual");
        pauseAsync = SyncReflection.Method(apiType, "PauseAsync",
            userData.PropertyType, reason, typeof(Nullable<>).MakeGenericType(duration));

        // Without unpause the adapter still pauses; paused pairs then show "can't unpause here".
        try
        {
            unPauseAsync = SyncReflection.Method(apiType, "UnPauseAsync", userData.PropertyType, typeof(bool));
        }
        catch (MissingMemberException e)
        {
            unPauseAsync = null;
            Services.Log.Warning($"PlayerSync unpause is unavailable on this version: {e.Message}");
        }

        // What its "Block Pairing" item runs; without it the adapter still pauses.
        try
        {
            stickyPauseAndRemove = SyncReflection.Method(apiType, "UserPairStickyPauseAndRemove", userData.PropertyType);
        }
        catch (MissingMemberException e)
        {
            stickyPauseAndRemove = null;
            Services.Log.Warning($"PlayerSync block pairing is unavailable on this version: {e.Message}");
        }

        pairManager = newPairManager;
        api = newApi;
    }

    protected override void Unbind()
    {
        pairManager = null!;
        api = null!;
        manualReason = null!;
        hashCid = null!;
        allClientPairs = null!;
        pauseAsync = null!;
        unPauseAsync = null;
        stickyPauseAndRemove = null;
        ident = null!;
        isPaused = null!;
        userData = null!;
        isConnected = null!;
    }

    protected override bool IsConnected => isConnected.GetValue(api) is true;

    protected override bool CanUnpause => unPauseAsync != null;

    protected override bool CanBlock => stickyPauseAndRemove != null;

    protected override object? FindPair(nint address)
    {
        if (address == 0 || hashCid.Invoke(null, [address]) is not string { Length: > 0 } hash)
            return null;

        var pairs = (IDictionary)allClientPairs.GetValue(pairManager)!;
        foreach (var pair in pairs.Values)
        {
            if (string.Equals(ident.GetValue(pair) as string, hash, StringComparison.Ordinal))
                return pair;
        }

        return null;
    }

    protected override bool IsPaused(object pair) => isPaused.GetValue(pair) is true;

    protected override Task Pause(object pair) =>
        (Task)pauseAsync.Invoke(api, [userData.GetValue(pair), manualReason, null])!;

    // notification: false, so PlayerSync doesn't add its own "Auto unpausing" notice next to TargetMe's toast.
    protected override Task Unpause(object pair) =>
        (Task)unPauseAsync!.Invoke(api, [userData.GetValue(pair), false])!;

    protected override Task Block(object pair) =>
        (Task)stickyPauseAndRemove!.Invoke(api, [userData.GetValue(pair)])!;
}
