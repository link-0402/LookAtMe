using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace TargetMe.Integrations.Sync;

/// <summary>Lightless Sync, mirroring what its own right-click "Toggle Pause State" item does.</summary>
public sealed class LightlessSyncService : ReflectedSyncService
{
    private object pairManager = null!;
    private object api = null!;
    private MethodInfo hashCid = null!;
    private MethodInfo enumeratePairs = null!;
    private MethodInfo matchesKnownIdent = null!;
    private MethodInfo pauseAsync = null!;
    private MethodInfo unpauseAsync = null!;
    private PropertyInfo isPaused = null!;
    private PropertyInfo userData = null!;
    private PropertyInfo isConnected = null!;

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

        hashCid = SyncReflection.Method(util, "GetHashedCIDFromPlayerPointer", typeof(nint));
        enumeratePairs = SyncReflection.Method(newPairManager.GetType(), "EnumeratePairs");
        matchesKnownIdent = SyncReflection.Method(pair, "MatchesKnownIdent", typeof(string));
        isPaused = SyncReflection.Property(pair, "IsPaused");
        userData = SyncReflection.Property(pair, "UserData");
        isConnected = SyncReflection.Property(apiType, "IsConnected");
        pauseAsync = SyncReflection.Method(apiType, "PauseAsync", userData.PropertyType);
        unpauseAsync = SyncReflection.Method(apiType, "UnpauseAsync", userData.PropertyType);
        pairManager = newPairManager;
        api = newApi;
    }

    protected override bool IsConnected => isConnected.GetValue(api) is true;

    protected override object? FindPair(nint address)
    {
        if (address == 0 || hashCid.Invoke(null, [address]) is not string { Length: > 0 } hash)
            return null;

        foreach (var pair in (IEnumerable)enumeratePairs.Invoke(pairManager, null)!)
        {
            if (matchesKnownIdent.Invoke(pair, [hash]) is true)
                return pair;
        }

        return null;
    }

    protected override bool IsPaused(object pair) => isPaused.GetValue(pair) is true;

    protected override Task Pause(object pair) => (Task)pauseAsync.Invoke(api, [userData.GetValue(pair)])!;

    protected override Task Unpause(object pair) => (Task)unpauseAsync.Invoke(api, [userData.GetValue(pair)])!;
}
