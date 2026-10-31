using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using LookAtMe.Core;
using LookAtMe.Tracking;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace LookAtMe.Game;

/// <summary>
/// Makes the world camera orbit a viewer instead of the local player, TargetZoom-style, and puts
/// the camera back afterwards.
/// </summary>
public sealed unsafe class CameraFocus : IDisposable
{
    // World camera layout (the same values Cammy and TargetZoom use).
    private const int GetCameraTargetVfIndex = 18;
    private const int CanChangePerspectiveVfIndex = 23;
    private const int CurrentZoomOffset = 0x124;
    private const int MinZoomOffset = 0x128;
    private const int MaxZoomOffset = 0x12C;
    private const int HRotationOffset = 0x140;
    private const int VRotationOffset = 0x144;
    private const float FrontVRotation = -0.1f;
    private const float DefaultVRotation = -0.349066f;

    private delegate GameObject* GetCameraTargetDelegate(nint camera);
    private delegate byte CanChangePerspectiveDelegate(nint camera);

    private readonly Configuration config;
    private readonly ViewerTracker tracker;
    private readonly EscGate escGate = new();
    private Hook<GetCameraTargetDelegate>? getCameraTargetHook;
    private Hook<CanChangePerspectiveDelegate>? canChangePerspectiveHook;
    private bool hookFailed;

    private string? focusedKey;
    private nint focusedAddress;
    private float savedZoom, savedHRotation, savedVRotation, savedMinZoom;

    public bool Available => getCameraTargetHook is { IsEnabled: true };
    public ulong FocusedId { get; private set; }

    public CameraFocus(Configuration config, ViewerTracker tracker)
    {
        this.config = config;
        this.tracker = tracker;
        TryInstallHooks();
    }

    public void Dispose()
    {
        Stop();
        getCameraTargetHook?.Dispose();
        canChangePerspectiveHook?.Dispose();
    }

    private static nint WorldCamera
    {
        get
        {
            var manager = CameraManager.Instance();
            return manager == null ? 0 : (nint)manager->Camera;
        }
    }

    private static float* CameraFloat(int offset) => (float*)(WorldCamera + offset);

    private void TryInstallHooks()
    {
        if (getCameraTargetHook != null || hookFailed || WorldCamera == 0)
            return;

        try
        {
            var vtbl = *(nint**)WorldCamera;
            getCameraTargetHook = Services.GameInterop.HookFromAddress<GetCameraTargetDelegate>(vtbl[GetCameraTargetVfIndex], GetCameraTargetDetour);
            canChangePerspectiveHook = Services.GameInterop.HookFromAddress<CanChangePerspectiveDelegate>(vtbl[CanChangePerspectiveVfIndex], CanChangePerspectiveDetour);
            getCameraTargetHook.Enable();
            canChangePerspectiveHook.Enable();
        }
        catch (Exception ex)
        {
            hookFailed = true;
            getCameraTargetHook?.Dispose();
            canChangePerspectiveHook?.Dispose();
            getCameraTargetHook = null;
            canChangePerspectiveHook = null;
            Services.Log.Warning(ex, "Camera focus unavailable: couldn't hook the world camera");
        }
    }

    public bool IsFocused(ViewerRecord record) => focusedKey == record.Key;

    public void Toggle(ViewerRecord record)
    {
        if (IsFocused(record))
            Stop();
        else
            Start(record);
    }

    public void Start(ViewerRecord record)
    {
        if (!Available || WorldCamera == 0 || tracker.Resolve(record) is not { } pc)
            return;

        if (focusedKey == null)
        {
            savedZoom = *CameraFloat(CurrentZoomOffset);
            savedHRotation = *CameraFloat(HRotationOffset);
            savedVRotation = *CameraFloat(VRotationOffset);
            savedMinZoom = *CameraFloat(MinZoomOffset);
        }

        focusedKey = record.Key;
        FocusedId = pc.GameObjectId;
        focusedAddress = pc.Address;

        ApplyCloseUpLimit();
        *CameraFloat(CurrentZoomOffset) = Math.Clamp(config.ZoomDistance, *CameraFloat(MinZoomOffset), *CameraFloat(MaxZoomOffset));

        if (config.StartInFront)
        {
            // A camera angle equal to their facing puts the camera in front of them.
            *CameraFloat(HRotationOffset) = pc.Rotation;
            *CameraFloat(VRotationOffset) = FrontVRotation;
        }
    }

    public void Stop()
    {
        if (focusedKey == null)
            return;

        focusedKey = null;
        FocusedId = 0;
        focusedAddress = 0;

        if (WorldCamera == 0)
            return;

        // The game's closest zoom goes back first, so restoring the distance isn't clamped by our limit.
        *CameraFloat(MinZoomOffset) = savedMinZoom;

        switch (config.CameraOnStop)
        {
            case CameraOnStop.RestorePrevious:
                *CameraFloat(CurrentZoomOffset) = savedZoom;
                *CameraFloat(HRotationOffset) = savedHRotation;
                *CameraFloat(VRotationOffset) = savedVRotation;
                break;
            case CameraOnStop.BehindMe when Services.ObjectTable.LocalPlayer is { } me:
                *CameraFloat(CurrentZoomOffset) = savedZoom;
                var h = me.Rotation + MathF.PI;
                *CameraFloat(HRotationOffset) = h > MathF.PI ? h - MathF.Tau : h;
                *CameraFloat(VRotationOffset) = DefaultVRotation;
                break;
        }
    }

    /// <summary>Called every framework tick: follows the viewer and stops when focus should end.</summary>
    public void Update()
    {
        TryInstallHooks();

        var stopRequested = escGate.ShouldStop(Services.KeyState[VirtualKey.ESCAPE], TextInput.IsActive());
        if (focusedKey == null)
            return;

        var target = Services.ObjectTable.SearchById(FocusedId) as IPlayerCharacter;
        if (stopRequested
            || target == null
            || ViewerRecord.MakeKey(target.Name.TextValue, target.HomeWorld.RowId) != focusedKey
            || Services.ObjectTable.LocalPlayer == null
            || Services.Condition[ConditionFlag.BetweenAreas]
            || Services.Condition[ConditionFlag.BetweenAreas51]
            || Services.Condition[ConditionFlag.WatchingCutscene]
            || Services.Condition[ConditionFlag.WatchingCutscene78]
            || Services.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || (config.StopOnCombat && Services.Condition[ConditionFlag.InCombat]))
        {
            Stop();
            return;
        }

        focusedAddress = target.Address;
        // The game resets the closest zoom now and then (mounting, for example), so keep it applied.
        ApplyCloseUpLimit();
    }

    private void ApplyCloseUpLimit()
    {
        if (WorldCamera == 0)
            return;
        var limit = config.AllowCloseUps ? MathF.Min(config.CloseUpLimit, savedMinZoom) : savedMinZoom;
        if (*CameraFloat(MinZoomOffset) != limit)
            *CameraFloat(MinZoomOffset) = limit;
    }

    private GameObject* GetCameraTargetDetour(nint camera)
    {
        if (focusedAddress != 0 && camera == WorldCamera)
            return (GameObject*)focusedAddress;
        return getCameraTargetHook!.Original(camera);
    }

    // The game asks this before switching to first person when you scroll all the way in.
    // While focused with close-ups allowed, say no so scrolling in stops at the closest zoom.
    private byte CanChangePerspectiveDetour(nint camera)
    {
        if (focusedAddress != 0 && config.AllowCloseUps)
            return 0;
        return canChangePerspectiveHook!.Original(camera);
    }
}
