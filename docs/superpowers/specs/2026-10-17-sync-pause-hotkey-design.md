# Sync pause hotkey — Design

Date: 2026-10-17
Status: approved in chat, awaiting spec review

## Purpose

A configurable hotkey that, while held, marks the player under the mouse cursor and shows what
releasing will do in Lightless Sync and PlayerSync. Releasing over that player toggles their
sync: someone being synced is paused at once; someone already paused is unpaused after a
confirmation popup. It is the fastest way to stop (or restore) syncing one person without
opening either plugin's window.

Supported plugins (both inspected in the running game, 2026-10-17):

| Plugin | Internal name | Version inspected |
|---|---|---|
| Lightless Sync | `LightlessSync` | 3.3.0.0 |
| PlayerSync | `MareSempiterne` | 1.15.5.9 |

Neither exposes IPC or a chat command for pausing. TargetMe reaches their internals by
reflection, the same way XIV Sync Manager (`github.com/TexNevada/xiv-sync-manager`, read for
behaviour only, no code copied) does. Any update to either plugin can break this; the design
contains each break to the plugin it happened in.

## Behaviour

### Holding the hotkey

Active only while all of these hold: a hotkey is bound, the player is logged in, no text input is
active (chat or any ImGui field), not in a cutscene or GPose, and the unpause popup is closed.

The target, in order:
1. The viewer-list row under the cursor (`ViewerWindow.Hovered`, resolved to a loaded player).
2. The game's mouseover target (`ITargetManager.MouseOverTarget`), if it is a player character
   other than the local player.

The target gets the world highlight (ring at the feet, marker above the head, edge arrow when
off-screen; **no line**) in a colour that depends on what releasing would do, plus a label under
the head marker:

| Release would… | Colour (default) | Label example |
|---|---|---|
| pause them in at least one plugin | Pause colour, `#FF4FA3` (TargetMe's accent) | `Pause · Lightless` |
| only offer to unpause them | Unpause colour, green `(0.30, 0.90, 0.45)` | `Unpause… · PlayerSync` |
| do nothing (not known to any connected plugin) | Grey `(0.55, 0.55, 0.55, 0.8)`, ring and marker greyed | `Not paired` |

The label lists every plugin that knows them, one part per plugin, e.g.
`Pause · Lightless   Unpause… · PlayerSync`. Other per-plugin parts:
- `Lightless offline` when that plugin is loaded but not connected to its server.
- `Paused (can't unpause here)` when a pair is paused but the plugin's unpause call can't be
  resolved (see "Known limits").

Plugins that aren't loaded, or whose adapter is unavailable, don't appear in the label.

### Releasing the hotkey

Evaluated against the target under the cursor at the moment of release (not the one the key went
down over). For each plugin, independently:
- **Synced** → pause at once. Toast: `Paused Name (Lightless)`.
- **Paused** → collected into one confirmation popup.
- **Not paired / offline / unavailable** → nothing.

Releasing over nobody does nothing.

### Unpause confirmation popup

A small centered window, `NoCollapse | AlwaysAutoResize`, no close button:
- Text: `Unpause Name in PlayerSync?` (both plugins: `Unpause Name in Lightless and PlayerSync?`).
- Buttons: **Unpause** and **Cancel**. Esc cancels.
- **Unpause** unpauses in each listed plugin, then shows a toast per plugin:
  `Unpaused Name (PlayerSync)`.
- The popup keeps the pair references it was opened with. It re-checks state when confirmed and
  skips a plugin whose pair is no longer paused or no longer known.

### Feedback

Toasts go through Dalamud's `INotificationManager` (success for pause/unpause, warning for a
failure: `Couldn't pause Name in Lightless`). Exceptions go to the log with the full stack.

## Hotkey

- A binding is one main key plus any of Ctrl / Shift / Alt. Modifiers must match exactly:
  Ctrl+F doesn't fire on Ctrl+Shift+F. The default is no key (feature off).
- **Keyboard keys** are read through `IKeyState`. While the feature is active and the binding is
  held, TargetMe clears the main key there every frame, so the game never acts on it.
- **Mouse side buttons** (Mouse 4 / Mouse 5) are read through ImGui. The game's key array doesn't
  include them, so they can't be hidden from the game. Settings say so next to the capture button.
- **Capture** in settings: click the hotkey button, then press the key or chord. Esc cancels
  the capture. **Clear** turns the feature off. Ctrl, Shift and Alt can't be the main key.
- A key held while the feature became inactive (for example, chat opened) doesn't fire on
  release.

## Reaching the sync plugins

### Finding a plugin

`SyncReflection` (shared by both adapters):
1. `IDalamudPluginInterface.InstalledPlugins` → the entry whose `InternalName` matches and
   `IsLoaded`.
2. That `IExposedPlugin` wraps Dalamud's `LocalPlugin`. Find the field by its type name
   (`Dalamud.Plugin.Internal.Types.LocalPlugin`), not a compiler-generated field name, then
   read its `instance`.
3. The plugin's service container: `instance._host.Services` (PlayerSync) or
   `instance._lifecycle._host.Services` (Lightless).
4. Services are fetched with `GetService(Type)` where the `Type` comes from the plugin's own
   assembly.

Everything after step 1 is reflection. TargetMe references neither plugin's assemblies nor
`Microsoft.Extensions.*`; each plugin loads its own copy of those, so typed casts across them
fail (seen in testing: casting the host to `IHost` resolved, but service lookup through it
returned null).

Members are resolved once, at connect. If any is missing, the adapter is **Unavailable on this
version**. The missing member's name goes to the log, settings show it, and the adapter does
nothing else until the next connect.

Connect happens on first use and again whenever Dalamud raises `ActivePluginsChanged`. Before
each pause or unpause the adapter checks that the plugin instance is still the one it connected
to; if not, it reconnects first.

### Matching a character to a pair

Both plugins identify people by a hashed content ID and expose the same static helper:
`DalamudUtilService.GetHashedCIDFromPlayerPointer(nint address) : string`. TargetMe calls it on
the target's address and never reimplements the hash.

| | Lightless | PlayerSync |
|---|---|---|
| Container | `Plugin._lifecycle._host.Services` | `Plugin._host.Services` |
| Hash helper | `LightlessSync.Services.DalamudUtilService` (static) | `MareSynchronos.Services.DalamudUtilService` (static) |
| All pairs | `PairManager.EnumeratePairs()` | `PairManager._allClientPairs.Values` |
| Match | first pair where `MatchesKnownIdent(hash)` | first pair where `Ident == hash` |
| Connected | `ApiController.IsConnected` | `ApiController.IsConnected` |
| Paused | `Pair.IsPaused` | `Pair.IsPaused` |
| Pause | `ApiController.PauseAsync(UserData)` | `ApiController.PauseAsync(UserData, PauseReason.Manual, null)` |
| Unpause | `ApiController.UnpauseAsync(UserData)` | `ApiController.UnPauseAsync(UserData, notification: false)` |

`UserData` is the pair's `UserData` property. `PauseReason` is
`MareSynchronos.MareConfiguration.Models.PauseReason`, resolved by name.

Every pair counts: direct pairs and syncshell members alike. Both plugins' own pause paths
(`GetPairByUID` → `UserPair.OwnPermissions.SetPaused` → `UserSetPairPermissions`) accept any
pair; neither checks for a direct pair. Calling those methods rather than sending a
hand-built permissions request keeps PlayerSync's own pause-reason and timeout records right.
`notification: false` avoids PlayerSync's own "Auto unpausing" notice next to TargetMe's toast.

Pair state is read at most every 250 ms per player and plugin (`TimedCache`, like Moodles).
The release action always reads fresh state.

### Async calls and threads

Pause and unpause return a `Task`. TargetMe starts them on the framework thread, doesn't block
on them, and shows the toast from a continuation (success or failure). The cached state for that
player is dropped so the next frame re-reads it.

### Errors

- Every call into a sync plugin is wrapped in try/catch. `TargetInvocationException` is
  unwrapped before logging.
- A failure in one plugin's call never stops the other plugin's action.
- A member missing at connect disables that adapter only (see above).

## Known limits

- **Internals can change.** Any Lightless or PlayerSync update may rename what this relies on.
  The adapter then shows "Unavailable on this version" until TargetMe is updated.
- **PlayerSync, paused pairs:** PlayerSync's own right-click menu only finds pairs it is
  currently syncing, so the unpause lookup matches on `Ident` instead. From its code, `Ident`
  comes from the online-user record and is only cleared when the pair goes offline, so a paused,
  online pair should still match. This couldn't be checked live (neither plugin was connected).
  It is on the in-game checklist. If it fails, the label shows `Paused (can't unpause here)`
  and no popup is offered for PlayerSync.
- **Syncshell permissions:** like the plugins' own pause buttons, TargetMe doesn't set the
  "Sticky" (preferred permissions) flag. A later syncshell-wide permission change by the shell's
  owner can overwrite a pause on a syncshell-only member, as it would a native pause.
- **Server pauses are two-way:** both plugins' pauses stop syncing in both directions.
- **Mouse side buttons** can't be hidden from the game.

## Architecture

New and changed files:

```
TargetMe/
  Core/
    HotkeyBinding.cs          key + modifiers, IsHeld(...), display text, validation (pure)
    SyncPauseRules.cs         per-plugin states -> marker style, label parts, release action (pure)
  Integrations/Sync/
    ISyncService.cs           Name, Status, GetState(player), SetPaused(player, paused)
    SyncReflection.cs         plugin instance, service container, member lookup helpers
    LightlessSyncService.cs
    PlayerSyncService.cs
  Game/
    TextInput.cs              IsTyping(), moved out of CameraFocus (now shared)
    PauseHotkey.cs            per frame: key state, hiding the key, target, release -> rules
  UI/
    HighlightOverlay.cs       changed: Draw takes an OverlayStyle (colour, line on/off, label)
    UnpauseConfirmWindow.cs
    SettingsWindow.cs         Integrations tab: "Sync pause" section
  Configuration.cs            PauseHotkey, PauseColor, UnpauseColor, NotPairedColor
  Services.cs                 + INotificationManager
  Plugin.cs                   wiring, draw, dispose
```

### Core types

```csharp
public enum SyncState { Unavailable, Offline, NotPaired, Synced, Paused, PausedCantUnpause }

public readonly record struct SyncPluginState(string Plugin, SyncState State);

public enum MarkerKind { Pause, Unpause, NotPaired }

// SyncPauseRules
public static MarkerKind Marker(IReadOnlyList<SyncPluginState> states);
public static IReadOnlyList<string> LabelParts(IReadOnlyList<SyncPluginState> states);
public static (IReadOnlyList<string> Pause, IReadOnlyList<string> ConfirmUnpause) OnRelease(IReadOnlyList<SyncPluginState> states);
```

`Marker`: any `Synced` → `Pause`; else any `Paused` → `Unpause`; else `NotPaired`
(`PausedCantUnpause` and `Offline` alone count as `NotPaired` for colour, but keep their
label parts). `Unavailable` contributes nothing.

`HotkeyBinding`: `int Key` (a Windows virtual-key code, 0 = none), `HotkeyMouse Mouse` (`None`,
`Button4`, `Button5`), `bool Ctrl, Shift, Alt`. At most one of `Key` / `Mouse` is set. `IsEmpty`,
`IsHeld(Func<int, bool> keyDown, Func<HotkeyMouse, bool> mouseDown, bool ctrl, bool shift,
bool alt)`, and `Display(Func<int, string> keyName)` giving text such as `Ctrl+F` or
`Shift+Mouse 4`. The test project compiles `Core/*.cs` without a Dalamud reference, so the key
is a plain `int`; `PauseHotkey` converts to and from Dalamud's `VirtualKey` and supplies key
names (`VirtualKey.GetFancyName()`).

### ISyncService

```csharp
public interface ISyncService
{
    string Name { get; }                      // "Lightless", "PlayerSync"
    SyncServiceStatus Status { get; }         // NotLoaded, Connected, Disconnected, Unavailable(reason)
    SyncState GetState(IPlayerCharacter player);
    Task SetPaused(IPlayerCharacter player, bool paused);
}
```

`GetState` goes through the 250 ms cache. `SetPaused` reads fresh state, finds the pair and
calls the plugin's method; it throws `InvalidOperationException` when the pair is gone, which
becomes a failure toast.

### Data flow

1. `Framework.Update` → `PauseHotkey.Update()`: works out whether the binding is held and
   whether the feature is active, hides the key, and picks the target.
2. `UiBuilder.Draw` → if the key is held and there's a target: query both services'
   `GetState`, then `SyncPauseRules.Marker` / `LabelParts` → `HighlightOverlay.Draw(target,
   self, style)`.
3. On the frame the binding stops being held (and the feature was active the whole time):
   fresh states → `SyncPauseRules.OnRelease` → `SetPaused(true)` for each `Pause` entry, and
   open `UnpauseConfirmWindow` when `ConfirmUnpause` isn't empty.
4. The popup's **Unpause** → `SetPaused(false)` for each listed service.

The viewer-list hover overlay keeps its current look, passing a style built from the
existing overlay settings.

### Settings ("Sync pause" in the Integrations tab)

- Hotkey: capture button showing the current binding (or "Not set"), and **Clear**.
- Note under the button when a mouse button is bound: "Mouse buttons also reach the game."
- Colours: Pause, Unpause, Not paired.
- Status line per plugin: `Lightless: Connected` / `Disconnected` / `Not loaded` /
  `Unavailable on this version (missing: <member>)`.

### Dispose

Unsubscribe `ActivePluginsChanged`, then drop adapter references. TargetMe never registers
anything inside the sync plugins, so there's nothing there to undo. Pending pause tasks are
left to finish; their continuations check a disposed flag before showing a toast.

## Testing

### Unit tests (xUnit, `TargetMe.Tests`)

- `HotkeyBinding`: empty binding is never held; exact modifier matching (Ctrl+F not held on
  Ctrl+Shift+F, F not held on Ctrl+F); modifiers rejected as main key; display text.
- `SyncPauseRules`: every combination of two plugins × {Unavailable, Offline, NotPaired, Synced,
  Paused, PausedCantUnpause}: marker kind, label parts in plugin order, release action
  (pause where synced, confirm where paused, nothing otherwise).

### In game through XIV Bridge (no other players needed, never pauses anyone)

- The plugin loads with no errors in dalamud.log.
- Both adapters connect: every member in the table above resolves, and settings show their
  status.
- The hotkey capture records a key and Clear removes it.
- The marker and label draw on a target, checked by feeding a target and states from a script
  and taking a screenshot.
- The unpause popup opens from a script, and Cancel calls nothing.

### Checks only the user can do (needs paired people online)

- Holding the key over a synced pair shows the pause colour and label; releasing pauses them.
  In Lightless and in PlayerSync.
- Same for a syncshell-only member in each plugin.
- Releasing over a paused pair opens the popup; confirming unpauses them.
- PlayerSync: a paused, online pair is still found (the `Ident` question above).
- The bound keyboard key doesn't trigger its game action while the feature is active.

## Out of scope

- Timed pauses (PlayerSync's "Pause for 30 minutes" etc.).
- Snowcloak or other sync plugins.
- Setting the Sticky / preferred-permissions flag.
- Asking either plugin's team for an official pause IPC (worth doing, but outside this project).
