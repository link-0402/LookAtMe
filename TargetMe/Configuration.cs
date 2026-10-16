using System;
using System.Numerics;
using Dalamud.Configuration;

namespace TargetMe;

public enum CameraOnStop
{
    RestorePrevious,
    BehindMe,
    LeaveAsIs,
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Window
    public bool WindowUnlocked { get; set; } = false;
    public float BackgroundOpacity { get; set; } = 0.75f;
    public bool OpenOnLogin { get; set; } = false;
    public bool HideInDuty { get; set; } = false;
    public int ListCap { get; set; } = 15;

    // Colors
    public Vector4 ActiveColor { get; set; } = new(0.30f, 0.90f, 0.45f, 1f);
    public Vector4 EarlierColor { get; set; } = new(0.85f, 0.85f, 0.85f, 0.80f);
    public Vector4 AwayColor { get; set; } = new(0.50f, 0.50f, 0.50f, 0.70f);

    // Emotes
    public bool ShowEmotes { get; set; } = true;
    public int EmoteLifetimeSeconds { get; set; } = 15;

    // Overlay
    public Vector4 OverlayColor { get; set; } = new(1.0f, 0.31f, 0.64f, 1f);
    public float OverlayThickness { get; set; } = 3f;
    public bool OverlayLine { get; set; } = true;
    public bool OverlayRing { get; set; } = true;
    public bool OverlayMarker { get; set; } = true;
    public bool OverlayPulse { get; set; } = true;

    // Context menu
    public bool MenuDote { get; set; } = true;
    public bool MenuExamine { get; set; } = true;
    public bool MenuAdventurePlate { get; set; } = true;
    public bool MenuGameMenu { get; set; } = true;
    public bool MenuMoodles { get; set; } = true;

    // Sound
    public bool SoundEnabled { get; set; } = false;
    public float SoundVolume { get; set; } = 0.5f;
    public int SoundCooldownSeconds { get; set; } = 5;
    public bool SoundWhenWindowClosed { get; set; } = false;
    public bool SoundMuteInDuty { get; set; } = true;
    public int GameSoundEffect { get; set; } = 1;
    public string? CustomSoundPath { get; set; } = null;

    // Camera
    public float ZoomDistance { get; set; } = 4f;
    public bool AllowCloseUps { get; set; } = true;
    public float CloseUpLimit { get; set; } = 0.5f;
    public bool StartInFront { get; set; } = true;
    public bool StopOnCombat { get; set; } = true;
    public CameraOnStop CameraOnStop { get; set; } = CameraOnStop.RestorePrevious;

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}
