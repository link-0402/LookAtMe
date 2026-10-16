using System;
using System.IO;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using NAudio.Wave;
using TargetMe.Core;

namespace TargetMe.Alerts;

/// <summary>Plays a sound when someone starts targeting the local player.</summary>
public sealed class SoundAlert
{
    // <se.1> is sound effect 37, <se.16> is 52.
    private const uint FirstChatSoundEffect = 36;
    // So a crowd targeting you at once plays one sound, not a pile of overlapping ones.
    private static readonly TimeSpan MinGapBetweenSounds = TimeSpan.FromSeconds(1);

    private readonly Configuration config;
    private readonly Func<bool> isWindowOpen;
    private readonly Cooldowns cooldowns = new();

    public SoundAlert(Configuration config, Func<bool> isWindowOpen)
    {
        this.config = config;
        this.isWindowOpen = isWindowOpen;
    }

    public void OnViewerStarted(ViewerRecord record)
    {
        if (!config.SoundEnabled
            || (config.SoundMuteInDuty && Services.Condition[ConditionFlag.BoundByDuty])
            || (!config.SoundWhenWindowClosed && !isWindowOpen())
            || !cooldowns.TryFire(record.Key, DateTime.Now, TimeSpan.FromSeconds(Math.Clamp(config.SoundCooldownSeconds, 0, 3600)), MinGapBetweenSounds))
            return;

        Play();
    }

    /// <summary>Plays the configured sound regardless of the alert conditions.</summary>
    public void Test() => Play();

    private unsafe void Play()
    {
        if (string.IsNullOrWhiteSpace(config.CustomSoundPath))
        {
            UIGlobals.PlaySoundEffect(FirstChatSoundEffect + (uint)Math.Clamp(config.GameSoundEffect, 1, 16));
            return;
        }

        var path = config.CustomSoundPath;
        var volume = Math.Clamp(config.SoundVolume, 0f, 1f);
        new Thread(() => PlayFile(path, volume)) { IsBackground = true, Name = "TargetMe sound" }.Start();
    }

    private static void PlayFile(string path, float volume)
    {
        try
        {
            if (!File.Exists(path))
            {
                Services.Log.Warning($"Alert sound not found: {path}");
                return;
            }

            using var reader = OpenReader(path);
            if (reader == null)
            {
                Services.Log.Warning($"Unsupported alert sound format: {path}");
                return;
            }

            using var channel = new WaveChannel32(reader) { Volume = volume, PadWithZeroes = false };
            using var output = new WaveOutEvent();
            output.Init(channel);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(50);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, $"Couldn't play alert sound {path}");
        }
    }

    private static WaveStream? OpenReader(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".wav" => new WaveFileReader(path),
        ".mp3" => new Mp3FileReader(path),
        ".aif" or ".aiff" => new AiffFileReader(path),
        ".wma" or ".aac" or ".m4a" => new MediaFoundationReader(path),
        _ => null,
    };
}
