using System;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using TargetMe.Core;

namespace TargetMe.Integrations.Sync;

/// <summary>
/// Shared part of the sync adapters: finds the plugin, binds its members once per plugin instance,
/// caches pair state and turns every failure into a status or a faulted task.
/// </summary>
public abstract class ReflectedSyncService : ISyncService
{
    private readonly string internalName;
    private readonly TimedCache<ulong, SyncState> cache = new(TimeSpan.FromMilliseconds(250));
    private readonly StaleCache<object> instance = new();
    private object? bound;
    private object? failed;
    private string? missing;

    protected ReflectedSyncService(string name, string internalName)
    {
        Name = name;
        this.internalName = internalName;
    }

    public string Name { get; }

    /// <summary>The connection is re-checked on every read, so settings show the plugin's current state.</summary>
    public SyncServiceStatus Status
    {
        get
        {
            try
            {
                EnsureBound();
            }
            catch (Exception e)
            {
                Services.Log.Debug(SyncReflection.Unwrap(e), $"{Name}: status check failed");
            }

            return status;
        }
    }

    private SyncServiceStatus status = new(SyncServiceStatusKind.NotLoaded);

    /// <summary>Resolves and stores every member the adapter needs; throws <see cref="MissingMemberException"/> on any gap.</summary>
    protected abstract void Bind(object plugin);

    /// <summary>Drops every reference Bind stored, so an unloaded plugin's objects and assembly can be freed.</summary>
    protected abstract void Unbind();
    protected abstract bool IsConnected { get; }
    protected abstract object? FindPair(nint address);
    protected abstract bool IsPaused(object pair);
    protected abstract Task Pause(object pair);
    protected abstract Task Unpause(object pair);
    protected virtual bool CanUnpause => true;

    public SyncState GetState(IPlayerCharacter player, bool fresh = false)
    {
        try
        {
            if (!EnsureBound())
                return SyncState.Unavailable;
            if (status.Kind != SyncServiceStatusKind.Connected)
                return SyncState.Offline;
            return fresh ? ReadState(player.Address) : cache.Get(player.GameObjectId, DateTime.Now, () => ReadState(player.Address));
        }
        catch (Exception e)
        {
            Services.Log.Debug(SyncReflection.Unwrap(e), $"{Name}: reading sync state failed");
            return SyncState.Unavailable;
        }
    }

    public Task SetPaused(IPlayerCharacter player, bool paused)
    {
        try
        {
            if (!EnsureBound(rescan: true) || status.Kind != SyncServiceStatusKind.Connected)
                throw new InvalidOperationException($"{Name} isn't connected.");
            if (!paused && !CanUnpause)
                throw new InvalidOperationException($"{Name}'s unpause isn't available on this version.");
            var pair = FindPair(player.Address) ?? throw new InvalidOperationException($"{Name}: pair not found.");
            return paused ? Pause(pair) : Unpause(pair);
        }
        catch (Exception e)
        {
            return Task.FromException(SyncReflection.Unwrap(e));
        }
        finally
        {
            cache.Clear();
        }
    }

    public void Reconnect()
    {
        instance.Invalidate();
        Forget();
    }

    private void Forget()
    {
        Unbind();
        bound = null;
        failed = null;
        missing = null;
        cache.Clear();
    }

    private SyncState ReadState(nint address)
    {
        if (FindPair(address) is not { } pair)
            return SyncState.NotPaired;
        if (IsPaused(pair))
            return CanUnpause ? SyncState.Paused : SyncState.PausedCantUnpause;
        return SyncState.Synced;
    }

    /// <summary>
    /// Makes sure the members are bound to the loaded plugin instance. Finding it allocates, so it's looked up
    /// again only after <see cref="Reconnect"/> (the plugin list changed) or when <paramref name="rescan"/> asks.
    /// </summary>
    private bool EnsureBound(bool rescan = false)
    {
        var plugin = instance.Get(() => SyncReflection.FindPluginInstance(internalName), rescan);
        if (plugin == null)
        {
            Forget();
            status = new(SyncServiceStatusKind.NotLoaded);
            return false;
        }

        if (ReferenceEquals(plugin, failed))
            return false;

        if (!ReferenceEquals(plugin, bound))
        {
            cache.Clear();
            Unbind();
            bound = null;
            try
            {
                Bind(plugin);
                bound = plugin;
            }
            catch (Exception e)
            {
                Unbind();
                failed = plugin;
                missing = SyncReflection.Unwrap(e).Message;
                status = new(SyncServiceStatusKind.Unavailable, missing);
                Services.Log.Warning($"{Name} sync pause is unavailable on this version: {missing}");
                return false;
            }
        }

        status = new(IsConnected ? SyncServiceStatusKind.Connected : SyncServiceStatusKind.Disconnected);
        return true;
    }
}
