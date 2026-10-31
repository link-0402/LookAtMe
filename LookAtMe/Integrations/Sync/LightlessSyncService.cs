using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace LookAtMe.Integrations.Sync;

/// <summary>Lightless Sync, mirroring what its own right-click "Toggle Pause State" item does.</summary>
public sealed class LightlessSyncService : ReflectedSyncService
{
    private object pairManager = null!;
    private object api = null!;
    private object? utilService;
    private MethodInfo hashCid = null!;
    private MethodInfo enumeratePairs = null!;
    private MethodInfo matchesKnownIdent = null!;
    private MethodInfo pauseAsync = null!;
    private MethodInfo unpauseAsync = null!;
    private PropertyInfo isPaused = null!;
    private PropertyInfo userData = null!;
    private PropertyInfo isConnected = null!;
    private PropertyInfo uid = null!;
    private object? blacklist;
    private MethodInfo? blockAsync;

    public LightlessSyncService()
        : base("Lightless", "LightlessSync")
    {
    }

    protected override void Bind(object plugin)
    {
        var assembly = plugin.GetType().Assembly;
        var newPairManager = SyncReflection.GetService(plugin, "LightlessSync.PlayerData.Pairs.PairManager");
        var newApi = SyncReflection.GetService(plugin, "LightlessSync.WebAPI.ApiController");
        var util = SyncReflection.Type(assembly, "LightlessSync.Services.DalamudUtilService");
        var pair = SyncReflection.Type(assembly, "LightlessSync.PlayerData.Pairs.Pair");
        var apiType = newApi.GetType();

        // 3.3.1 replaced the static GetHashedCIDFromPlayerPointer with an instance Try method.
        object? newUtil = null;
        MethodInfo newHashCid;
        try
        {
            newHashCid = SyncReflection.Method(util, "TryGetHashedCIDFromAddress", typeof(nint), typeof(string).MakeByRefType());
            newUtil = SyncReflection.GetService(plugin, util.FullName!);
        }
        catch (MissingMemberException)
        {
            newHashCid = SyncReflection.Method(util, "GetHashedCIDFromPlayerPointer", typeof(nint));
        }

        hashCid = newHashCid;
        enumeratePairs = SyncReflection.Method(newPairManager.GetType(), "EnumeratePairs");
        matchesKnownIdent = SyncReflection.Method(pair, "MatchesKnownIdent", typeof(string));
        isPaused = SyncReflection.Property(pair, "IsPaused");
        userData = SyncReflection.Property(pair, "UserData");
        isConnected = SyncReflection.Property(apiType, "IsConnected");
        pauseAsync = SyncReflection.Method(apiType, "PauseAsync", userData.PropertyType);
        unpauseAsync = SyncReflection.Method(apiType, "UnpauseAsync", userData.PropertyType);
        uid = SyncReflection.Property(userData.PropertyType, "UID");

        // What its "Blacklist Pair" item runs; without it the adapter still pauses.
        try
        {
            var newBlacklist = SyncReflection.GetService(plugin, "LightlessSync.Services.BlacklistService");
            blockAsync = SyncReflection.Method(newBlacklist.GetType(), "BlockAsync", typeof(string));
            blacklist = newBlacklist;
        }
        catch (MissingMemberException e)
        {
            blacklist = null;
            blockAsync = null;
            Services.Log.Warning($"Lightless blacklist is unavailable on this version: {e.Message}");
        }

        pairManager = newPairManager;
        api = newApi;
        utilService = newUtil;
    }

    protected override void Unbind()
    {
        pairManager = null!;
        api = null!;
        utilService = null;
        hashCid = null!;
        enumeratePairs = null!;
        matchesKnownIdent = null!;
        pauseAsync = null!;
        unpauseAsync = null!;
        isPaused = null!;
        userData = null!;
        isConnected = null!;
        uid = null!;
        blacklist = null;
        blockAsync = null;
    }

    protected override bool IsConnected => isConnected.GetValue(api) is true;

    protected override bool CanBlock => blockAsync != null;

    protected override object? FindPair(nint address)
    {
        if (address == 0 || HashCid(address) is not { Length: > 0 } hash)
            return null;

        foreach (var pair in (IEnumerable)enumeratePairs.Invoke(pairManager, null)!)
        {
            if (matchesKnownIdent.Invoke(pair, [hash]) is true)
                return pair;
        }

        return null;
    }

    private string? HashCid(nint address)
    {
        if (utilService == null)
            return hashCid.Invoke(null, [address]) as string;

        object?[] args = [address, null];
        return hashCid.Invoke(utilService, args) is true ? args[1] as string : null;
    }

    protected override bool IsPaused(object pair) => isPaused.GetValue(pair) is true;

    protected override Task Pause(object pair) => (Task)pauseAsync.Invoke(api, [userData.GetValue(pair)])!;

    protected override Task Unpause(object pair) => (Task)unpauseAsync.Invoke(api, [userData.GetValue(pair)])!;

    // Lightless catches server errors itself and answers false, so a failed blacklist doesn't fault the task.
    protected override async Task Block(object pair)
    {
        var blocked = (Task<bool>)blockAsync!.Invoke(blacklist, [uid.GetValue(userData.GetValue(pair))])!;
        if (!await blocked)
            throw new InvalidOperationException("Lightless didn't blacklist them; see its log.");
    }
}
