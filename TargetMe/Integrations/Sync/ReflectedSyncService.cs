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
    private object? bound;
    private object? failed;
    private string? missing;

    protected ReflectedSyncService(string name, string internalName)
    {
        Name = name;
        this.internalName = internalName;
    }

    public string Name { get; }
    public SyncServiceStatus Status { get; private set; } = new(SyncServiceStatusKind.NotLoaded);

    /// <summary>Resolves and stores every member the adapter needs; throws <see cref="MissingMemberException"/> on any gap.</summary>
    protected abstract void Bind(object plugin);
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
            if (Status.Kind != SyncServiceStatusKind.Connected)
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
            if (!EnsureBound() || Status.Kind != SyncServiceStatusKind.Connected)
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

    /// <summary>Makes sure the members are bound to the plugin instance that's loaded right now.</summary>
    private bool EnsureBound()
    {
        var plugin = SyncReflection.FindPluginInstance(internalName);
        if (plugin == null)
        {
            Reconnect();
            Status = new(SyncServiceStatusKind.NotLoaded);
            return false;
        }

        if (ReferenceEquals(plugin, failed))
            return false;

        if (!ReferenceEquals(plugin, bound))
        {
            cache.Clear();
            try
            {
                Bind(plugin);
                bound = plugin;
            }
            catch (Exception e)
            {
                bound = null;
                failed = plugin;
                missing = SyncReflection.Unwrap(e).Message;
                Status = new(SyncServiceStatusKind.Unavailable, missing);
                Services.Log.Warning($"{Name} sync pause is unavailable on this version: {missing}");
                return false;
            }
        }

        Status = new(IsConnected ? SyncServiceStatusKind.Connected : SyncServiceStatusKind.Disconnected);
        return true;
    }
}
