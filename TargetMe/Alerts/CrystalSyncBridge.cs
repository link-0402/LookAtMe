using System;
using System.Numerics;
using System.Text.Json;
using Dalamud.Plugin.Ipc;
using TargetMe.Core;

namespace TargetMe.Alerts;

/// <summary>
/// Optional: forwards "someone is looking at you" to Discord through the CrystalSync plugin.
/// Uses the same message format as Peeping Tim, so CrystalSync routes replies back as tells.
/// </summary>
public sealed class CrystalSyncBridge
{
    private readonly Configuration config;
    private readonly Cooldowns cooldowns = new();
    private readonly ICallGateSubscriber<bool> registered =
        Services.PluginInterface.GetIpcSubscriber<bool>("CrystalSync.Registered");
    private readonly ICallGateSubscriber<string, string> sendCustomMessage =
        Services.PluginInterface.GetIpcSubscriber<string, string>("CrystalSync.SendCustomMessage");

    public CrystalSyncBridge(Configuration config) => this.config = config;

    public bool Available => registered.HasFunction && sendCustomMessage.HasFunction;

    public void OnViewerStarted(ViewerRecord record)
    {
        if (!config.CrystalSyncEnabled || !Available)
            return;

        var viewerKey = $"{record.Name}@{record.WorldName}";
        var cooldown = TimeSpan.FromSeconds(Math.Clamp(config.CrystalSyncCooldownSeconds, 5, 3600));
        if (!cooldowns.TryFire(record.Key, DateTime.Now, cooldown))
            return;

        try
        {
            if (!registered.InvokeFunc())
                return;

            var payload = JsonSerializer.Serialize(new
            {
                sender_type = "peepingtim",
                sender_id = viewerKey,
                content = $"Peeping alert: {viewerKey} is looking at you.",
                color = ToHex(config.CrystalSyncColor),
                reply_mode = "game",
                reply_sender_type = "tell",
                reply_receiver_id = viewerKey,
                metadata = new
                {
                    viewer_name = record.Name,
                    viewer_world = record.WorldName,
                    viewer_key = viewerKey,
                },
            });
            sendCustomMessage.InvokeFunc(payload);
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "CrystalSync forwarding failed");
        }
    }

    private static string ToHex(Vector4 color)
    {
        static int Channel(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255);
        return $"#{Channel(color.X):X2}{Channel(color.Y):X2}{Channel(color.Z):X2}";
    }
}
