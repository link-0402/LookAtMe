# Sync Pause Hotkey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hold a configurable hotkey to mark the player under the cursor with their Lightless / PlayerSync state; releasing pauses them where they're synced and offers to unpause them where they're paused.

**Architecture:** Pure decisions (hotkey matching, hold/release edges, state → marker/label/action) live in `TargetMe/Core` and are unit-tested. Two reflection adapters behind `ISyncService` share a base class that owns connecting, caching, status and error wrapping. `PauseHotkey` drives it per frame. `HighlightOverlay` takes a style so the hover overlay and the pause marker share one drawer.

**Tech Stack:** C# / `Dalamud.NET.Sdk/15.0.0` (net10.0), FFXIVClientStructs, System.Reflection, Win32 `GetAsyncKeyState`, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-10-17-sync-pause-hotkey-design.md`. Read it before any task. It fixes the behaviour, the copy, the defaults, and the exact reflection members (its "Matching a character to a pair" table).

## Global Constraints

- Plugin internal names: `LightlessSync` (Lightless 3.3.0.0) and `MareSempiterne` (PlayerSync 1.15.5.9). Display names in UI and toasts: `Lightless`, `PlayerSync`, in that order everywhere.
- TargetMe references neither sync plugin's assemblies nor any `Microsoft.Extensions.*` package. Every access after finding the plugin entry is reflection.
- Pause and unpause go through each plugin's own `ApiController` methods from the spec table. Never build a `UserPermissionsDto` yourself. PlayerSync pause uses `PauseReason.Manual` and a null duration; PlayerSync unpause passes `notification: false`.
- Every pair counts (direct and syncshell). No `IsDirectlyPaired` check anywhere.
- Pair state cache: 250 ms per player and plugin (`TimedCache`). The release action and popup confirm always read fresh.
- Default colours: Pause `(1.0, 0.31, 0.64, 1)`, Unpause `(0.30, 0.90, 0.45, 1)`, Not paired `(0.55, 0.55, 0.55, 0.8)`. Default hotkey: none.
- Exact copy: `Pause · {Plugin}`, `Unpause… · {Plugin}`, `{Plugin} offline`, `Paused (can't unpause here) · {Plugin}`, `Not paired`; parts joined by three spaces. Toasts: `Paused {Name} ({Plugin})`, `Unpaused {Name} ({Plugin})`, `Couldn't pause {Name} in {Plugin}`, `Couldn't unpause {Name} in {Plugin}`, `{Name} is no longer nearby`. Popup: `Unpause {Name} in {Plugin}?` / `Unpause {Name} in Lightless and PlayerSync?`, buttons `Unpause` and `Cancel`.
- Game memory, reflection calls into the sync plugins, and `Task` starts happen on the framework/draw thread only.
- **No XIV Bridge script ever calls `SetPaused`, `PauseAsync` or `UnpauseAsync`.** In-game checks only read state, draw previews and open the popup to cancel it. Tests run in an inn room with automation on.
- Commit after every task. Write the message to a file in the scratchpad and run `git commit -F <file>` (the user's CLAUDE.md shell rule); end it with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit on `main`, no branch.
- Build: `dotnet build TargetMe.sln -c Debug`. Tests: `dotnet test TargetMe.Tests`. The dev plugin loads from `TargetMe/bin/x64/Debug/TargetMe.dll`. After a build, "reload" means: Dalamud reloads it by itself when auto-reload is on for the dev plugin; otherwise ask the user to reload TargetMe in `/xlplugins`. Confirm with `xiv_status` (version/load) and `xiv_log`.

## Review Focus

1. **Cursor moves from one player to another while the key is held.** The release must act on the player under the cursor at release, the same one the marker was last drawn on. `PauseHotkey` reads the target fresh every held frame and on the release frame (Task 6, step 2). Needs two other players, so it's a README checklist item (Task 8, step 3).
2. **The feature goes inactive mid-hold** (chat opens, cutscene starts, popup opens). Releasing must do nothing, and the still-held key must not re-arm when the feature comes back. Pinned by `HoldTrackerTests.InactiveMidHoldCancels` and `StillHeldAfterReactivationNeedsFreshPress` (Task 1).
3. **Someone known to both plugins in different states.** Each plugin is handled on its own. Pinned by `SyncPauseRulesTests.ReleaseActsPerPlugin` over all 36 state pairs (Task 2).
4. **A sync plugin is reloaded or updated while TargetMe runs.** The adapter must drop the old instance and never call into an unloaded assembly. `ReflectedSyncService` compares the live plugin instance before each use and reconnects (Task 3, step 3); Task 3 step 6 checks `Reconnect()` in game.
5. **The pair changes between opening the popup and confirming** (they went offline, were unpaused elsewhere, walked out of range). Nothing must be sent for that plugin. Pinned by `SyncPauseRulesTests.StillPausedSkipsChanged` (Task 2) and the "no longer nearby" path (Task 6, step 3).

---

### Task 1: HotkeyBinding and HoldTracker

**Files:**
- Create: `TargetMe/Core/HotkeyBinding.cs`, `TargetMe/Core/HoldTracker.cs`
- Test: `TargetMe.Tests/HotkeyBindingTests.cs`, `TargetMe.Tests/HoldTrackerTests.cs`

**Interfaces:**
- Produces:
  - `sealed record HotkeyBinding { int Key { get; init; } bool Ctrl { get; init; } bool Shift { get; init; } bool Alt { get; init; } bool IsEmpty { get; } /* Key == 0 */ bool IsMouse { get; } /* Key is 0x05 or 0x06 */ bool IsHeld(Func<int, bool> down); string Display(Func<int, string> keyName); static bool IsModifier(int vk); }`. Constants: `VkShift = 0x10`, `VkControl = 0x11`, `VkMenu = 0x12`.
  - `IsHeld`: false when empty; else `down(Key)` and `down(0x11) == Ctrl`, `down(0x10) == Shift`, `down(0x12) == Alt`.
  - `Display`: `"Not set"` when empty; else modifiers in the order `Ctrl+`, `Shift+`, `Alt+`, then `Mouse 4` / `Mouse 5` for 0x05 / 0x06, otherwise `keyName(Key)`.
  - `IsModifier`: true for 0x10–0x12 and 0xA0–0xA5.
  - `sealed class HoldTracker { bool Holding { get; } bool Update(bool held, bool active); void Reset(); }`. Arms only on a fresh press (held now, not held on the previous update) while active. Returns true on the update where an armed hold ends (`held` false) while still active. Becoming inactive while armed disarms; the key then has to be released and pressed again.

- [ ] **Step 1: Write the failing tests**

```csharp
// HotkeyBindingTests  (F = 0x46)
[Fact] EmptyIsNeverHeld()                 // new HotkeyBinding().IsHeld(_ => true) == false
[Fact] HeldNeedsExactModifiers()          // {Key=0x46,Ctrl=true}: down {0x11,0x46} => true; {0x11,0x10,0x46} => false; {0x46} => false
[Fact] PlainKeyNotHeldWithModifier()      // {Key=0x46}: down {0x11,0x46} => false; {0x46} => true
[Fact] MouseButtonsAreMouse()             // Key 0x05 and 0x06 => IsMouse; 0x46 => !IsMouse
[Theory] DisplayText                      // ({0x46,Ctrl,Shift}, "Ctrl+Shift+F") ({0x06,Alt}, "Alt+Mouse 5") ({0x05}, "Mouse 4") ({}, "Not set"); keyName = vk => vk == 0x46 ? "F" : "?"
[Theory] ModifiersAreRejected             // 0x10,0x11,0x12,0xA0,0xA5 => IsModifier true; 0x46, 0x05 => false
// HoldTrackerTests
[Fact] PressAndReleaseFires()             // Update(true,true)=false, Holding=true; Update(false,true)=true, Holding=false
[Fact] InactiveMidHoldCancels()           // (true,true),(true,false) => Holding=false; (false,true) => false
[Fact] StillHeldAfterReactivationNeedsFreshPress() // (true,true),(true,false),(true,true),(false,true) => last false; then (true,true),(false,true) => true
[Fact] PressWhileInactiveDoesNotArm()     // (true,false),(true,true),(false,true) => all false
[Fact] ReleaseWhileInactiveDoesNotFire()  // (true,true),(false,false) => false
[Fact] ResetDisarms()                     // (true,true), Reset(), (false,true) => false
```

- [ ] **Step 2:** Run `dotnet test TargetMe.Tests`. Expected: compile errors, `HotkeyBinding` and `HoldTracker` not found.
- [ ] **Step 3:** Implement both files with the interfaces above (namespace `TargetMe.Core`, no Dalamud usings).
- [ ] **Step 4:** Run `dotnet test TargetMe.Tests`. Expected: all tests pass, old and new.
- [ ] **Step 5:** Commit "Add hotkey binding and hold tracker".

### Task 2: SyncPauseRules

**Files:**
- Create: `TargetMe/Core/SyncPauseRules.cs`
- Test: `TargetMe.Tests/SyncPauseRulesTests.cs`

**Interfaces:**
- Produces (all in `SyncPauseRules.cs`, namespace `TargetMe.Core`):
  - `enum SyncState { Unavailable, Offline, NotPaired, Synced, Paused, PausedCantUnpause }`
  - `readonly record struct SyncPluginState(string Plugin, SyncState State)`
  - `enum MarkerKind { Pause, Unpause, NotPaired }`
  - `sealed record ReleaseAction(IReadOnlyList<string> Pause, IReadOnlyList<string> ConfirmUnpause)`
  - `static class SyncPauseRules`:
    - `MarkerKind Marker(IReadOnlyList<SyncPluginState> states)`: any `Synced` → `Pause`; else any `Paused` → `Unpause`; else `NotPaired`.
    - `string Label(IReadOnlyList<SyncPluginState> states)`: one part per state, in list order, using the Global Constraints copy (`Synced`, `Paused`, `Offline`, `PausedCantUnpause`; `NotPaired` and `Unavailable` add nothing), joined by three spaces; `"Not paired"` when there are no parts.
    - `ReleaseAction OnRelease(IReadOnlyList<SyncPluginState> states)`: plugins in `Synced` → `Pause`; plugins in `Paused` → `ConfirmUnpause`; list order kept.
    - `IReadOnlyList<string> StillPaused(IReadOnlyList<SyncPluginState> fresh, IReadOnlyList<string> requested)`: the requested plugins whose fresh state is `Paused`, in requested order; a plugin missing from `fresh` is skipped.

- [ ] **Step 1: Write the failing tests**

```csharp
static SyncPluginState L(SyncState s) => new("Lightless", s);
static SyncPluginState P(SyncState s) => new("PlayerSync", s);

[Fact] MarkerPauseWhenAnySynced()          // [L Synced, P Paused] => Pause
[Fact] MarkerUnpauseWhenOnlyPaused()       // [L NotPaired, P Paused] => Unpause
[Fact] MarkerNotPairedOtherwise()          // [L Offline, P PausedCantUnpause] => NotPaired; [L Unavailable, P Unavailable] => NotPaired
[Fact] LabelListsEachKnownPlugin()         // [L Synced, P Paused] => "Pause · Lightless   Unpause… · PlayerSync"
[Fact] LabelNotPairedWhenNothingKnown()    // [L NotPaired, P Unavailable] => "Not paired"; [] => "Not paired"
[Fact] LabelOffline()                      // [L Offline, P NotPaired] => "Lightless offline"
[Fact] LabelCantUnpause()                  // [P PausedCantUnpause] => "Paused (can't unpause here) · PlayerSync"
[Theory, MemberData(AllPairs)] ReleaseActsPerPlugin(SyncState l, SyncState p)
    // all 6x6 combinations: Pause == names whose state is Synced; ConfirmUnpause == names whose state is Paused; order Lightless, PlayerSync
[Fact] StillPausedSkipsChanged()           // fresh [L Synced, P Paused], requested ["Lightless","PlayerSync"] => ["PlayerSync"]; fresh [L Paused], requested ["PlayerSync","Lightless"] => ["Lightless"]
```

- [ ] **Step 2:** Run `dotnet test TargetMe.Tests`. Expected: compile errors for the missing types.
- [ ] **Step 3:** Implement `SyncPauseRules.cs` with the interfaces above.
- [ ] **Step 4:** Run `dotnet test TargetMe.Tests`. Expected: all tests pass.
- [ ] **Step 5:** Commit "Add sync pause rules".

### Task 3: Sync service base and the Lightless adapter

**Files:**
- Create: `TargetMe/Integrations/Sync/ISyncService.cs`, `SyncReflection.cs`, `ReflectedSyncService.cs`, `LightlessSyncService.cs`
- Modify: `TargetMe/Core/TimedCache.cs` (add `void Clear()`)
- Modify: `TargetMe/Plugin.cs` (construct, subscribe `PluginInterface.ActivePluginsChanged` → `Reconnect()` on each service, unsubscribe in `Dispose`; expose `public IReadOnlyList<ISyncService> SyncServices { get; }`)
- Test: `TargetMe.Tests/TimedCacheTests.cs`

**Interfaces:**
- Consumes: `SyncState` (Task 2), `TimedCache<TKey,TValue>` (existing, `TargetMe/Core/TimedCache.cs`).
- Produces:
  - `enum SyncServiceStatusKind { NotLoaded, Connected, Disconnected, Unavailable }` and `readonly record struct SyncServiceStatus(SyncServiceStatusKind Kind, string? Missing = null)` in `ISyncService.cs`.
  - `interface ISyncService { string Name { get; } SyncServiceStatus Status { get; } SyncState GetState(IPlayerCharacter player, bool fresh = false); Task SetPaused(IPlayerCharacter player, bool paused); void Reconnect(); }`
  - `static class SyncReflection`:
    - `object? FindPluginInstance(string internalName)`: `PluginInterface.InstalledPlugins` → first with matching `InternalName` and `IsLoaded` → the field on the `IExposedPlugin` object (walk base types) whose `FieldType.FullName == "Dalamud.Plugin.Internal.Types.LocalPlugin"` → its `instance` member. Null when not loaded.
    - `object GetService(object plugin, string typeName)`: container is `Read(plugin, "_host")` or `Read(Read(plugin, "_lifecycle"), "_host")`, then `Read(host, "Services")`; calls its `GetService(Type)` through reflection with `plugin.GetType().Assembly.GetType(typeName)`. Throws `MissingMemberException(typeName)` when the type or service is missing.
    - `object? Read(object? instance, string member)`: property or field, public or not, walking base types.
    - `MethodInfo Method(Type type, string name, params Type[] parameters)` (instance or static, public or not) and `PropertyInfo Property(Type type, string name)`; both throw `MissingMemberException(type.Name, name)`.
    - `Exception Unwrap(Exception e)`: strips `TargetInvocationException` layers.
  - `abstract class ReflectedSyncService : ISyncService`, constructed with `(string name, string internalName)`. Abstract members the adapters implement:
    - `protected abstract void Bind(object plugin)`: resolve and store every member the adapter needs; throw `MissingMemberException` on any gap.
    - `protected abstract bool IsConnected { get; }`
    - `protected abstract object? FindPair(nint address)`
    - `protected abstract bool IsPaused(object pair)`
    - `protected abstract Task Pause(object pair)` and `protected abstract Task Unpause(object pair)`
    - `protected virtual bool CanUnpause => true`
    - The base keeps the bound plugin instance, a `TimedCache<ulong, SyncState>` (key `player.GameObjectId`, 250 ms), and `Status`.
- Produces in `LightlessSyncService : ReflectedSyncService` (`name "Lightless"`, `internalName "LightlessSync"`): the Lightless column of the spec table.

- [ ] **Step 0:** Write `TimedCacheTests.ClearDropsEntries()`: `Get("k", t, () => 1)`, `Clear()`, then `Get("k", t, () => 2) == 2` and `Count == 1`. Run `dotnet test TargetMe.Tests` (expected: compile error, no `Clear`), add `Clear()` (empties the dictionary), run again (expected: pass).
- [ ] **Step 1:** Write `ISyncService.cs` and `SyncReflection.cs` with the interfaces above.
- [ ] **Step 2:** Write `ReflectedSyncService`:
  - `EnsureBound()` runs before every public call: `FindPluginInstance`; null → status `NotLoaded`, drop the binding. A different instance from the bound one → `Bind` again inside try/catch; a `MissingMemberException` → status `Unavailable` with `Missing = e.Message`, logged as a warning once per instance. Bound → status `Connected` or `Disconnected` from `IsConnected`.
  - `GetState`: not bound or unavailable → `Unavailable`; disconnected → `Offline`; else (cache unless `fresh`) `FindPair(player.Address)` → null `NotPaired`, paused → `CanUnpause ? Paused : PausedCantUnpause`, else `Synced`. Any exception → log at debug and return `Unavailable`.
  - `SetPaused`: bound and connected, else throw `InvalidOperationException`; `FindPair` fresh, null → throw `InvalidOperationException("pair not found")`; call `Pause` / `Unpause`, turning any synchronous exception (unwrapped) into a faulted `Task` so callers only ever see faults through the task; then `Clear()` the cache so the next frame re-reads.
  - `Reconnect()`: forget the bound instance, clear the cache; the next call binds again.
- [ ] **Step 3:** Write `LightlessSyncService.Bind` and overrides using exactly the spec's Lightless column: types `LightlessSync.PlayerData.Pairs.PairManager`, `LightlessSync.WebAPI.ApiController`, `LightlessSync.Services.DalamudUtilService`, `LightlessSync.PlayerData.Pairs.Pair`; `DalamudUtilService.GetHashedCIDFromPlayerPointer(nint)` (static); `PairManager.EnumeratePairs()`; `Pair.MatchesKnownIdent(string)`, `Pair.IsPaused`, `Pair.UserData`; `ApiController.IsConnected`, `PauseAsync(UserData)`, `UnpauseAsync(UserData)`. `FindPair` hashes the address (empty or null hash → null) and returns the first pair matching. The `UserData` parameter type comes from `Pair.UserData`'s property type.
- [ ] **Step 4:** Wire into `Plugin.cs`: `SyncServices = [new LightlessSyncService()]` for now (Task 4 adds PlayerSync). `ActivePluginsChanged += OnPluginsChanged` calling `Reconnect()` on each; remove it in `Dispose` before windows are removed.
- [ ] **Step 5:** Run `dotnet build TargetMe.sln -c Debug`. Expected: 0 errors. Run `dotnet test TargetMe.Tests`. Expected: all pass.
- [ ] **Step 6:** In game (reload TargetMe first), run through `xiv_exec`:

```csharp
var svc = ((TargetMe.Plugin)PluginInstance("TargetMe")).SyncServices[0];
var me = Objects.LocalPlayer;
var before = svc.GetState(me, fresh: true);
var status = svc.Status;
svc.Reconnect();
new { status, before, afterReconnect = svc.GetState(me, fresh: true), statusAfter = svc.Status }
```

  Expected: `status.Kind` is `Connected` or `Disconnected` (never `Unavailable`, never `NotLoaded` while Lightless is loaded); `before` is `Offline` when disconnected or `NotPaired` when connected (your own character is never your pair); the same after `Reconnect()`. `xiv_log` shows no TargetMe warnings.
- [ ] **Step 7:** Commit "Add sync service base and Lightless adapter".

### Task 4: PlayerSync adapter

**Files:**
- Create: `TargetMe/Integrations/Sync/PlayerSyncService.cs`
- Modify: `TargetMe/Plugin.cs` (`SyncServices = [new LightlessSyncService(), new PlayerSyncService()]`)

**Interfaces:**
- Consumes: `ReflectedSyncService`, `SyncReflection` (Task 3).
- Produces: `PlayerSyncService : ReflectedSyncService` (`name "PlayerSync"`, `internalName "MareSempiterne"`).

- [ ] **Step 1:** Implement `Bind` and overrides from the spec's PlayerSync column: types `MareSynchronos.PlayerData.Pairs.PairManager`, `MareSynchronos.WebAPI.ApiController`, `MareSynchronos.Services.DalamudUtilService`, `MareSynchronos.PlayerData.Pairs.Pair`, `MareSynchronos.MareConfiguration.Models.PauseReason` (value `Manual` via `Enum.Parse`), `MareSynchronos.MareConfiguration.Models.PauseDuration` (for the `Nullable<PauseDuration>` parameter type); `PairManager._allClientPairs` read as `IDictionary`, pairs from `.Values`; `Pair.Ident`, `Pair.IsPaused`, `Pair.UserData`; `ApiController.IsConnected`, `PauseAsync(UserData, PauseReason, PauseDuration?)` called with `(user, Manual, null)`, `UnPauseAsync(UserData, bool)` called with `(user, false)`. `FindPair` matches `Ident == hash` (ordinal; empty hash → null).
- [ ] **Step 2:** Bind `UnPauseAsync` separately: if it's missing, keep the adapter usable and make `CanUnpause` false (the spec's `Paused (can't unpause here)`), logging the missing member once. Missing anything else → `Unavailable`.
- [ ] **Step 3:** Run `dotnet build TargetMe.sln -c Debug`. Expected: 0 errors.
- [ ] **Step 4:** In game, repeat Task 3 step 6 with `SyncServices[1]`. Expected: the same results for PlayerSync.
- [ ] **Step 5:** Commit "Add PlayerSync adapter".

### Task 5: Overlay style and shared text-input check

**Files:**
- Create: `TargetMe/Game/TextInput.cs`
- Modify: `TargetMe/Game/CameraFocus.cs:197-201` (use `TextInput.IsActive()`, delete its private `IsTyping`)
- Modify: `TargetMe/UI/HighlightOverlay.cs` (style parameter, label)
- Modify: `TargetMe/Plugin.cs:94-102` (hover overlay passes `OverlayStyle.From(Config)`)

**Interfaces:**
- Produces:
  - `static unsafe class TextInput { static bool IsActive(); }` in `TargetMe.Game`, the same check `CameraFocus.IsTyping` does today (`RaptureAtkModule` text input or `ImGui.GetIO().WantTextInput`).
  - `readonly record struct OverlayStyle(Vector4 Color, float Thickness, bool Line, bool Ring, bool Marker, bool Pulse, string? Label = null)` in `TargetMe.UI`, with `static OverlayStyle From(Configuration c)` mapping `OverlayColor, OverlayThickness, OverlayLine, OverlayRing, OverlayMarker, OverlayPulse`.
  - `HighlightOverlay.Draw(IPlayerCharacter target, IPlayerCharacter self, OverlayStyle style)` replaces the `Configuration` overload.

- [ ] **Step 1:** Write `TextInput.cs` and switch `CameraFocus` to it.
- [ ] **Step 2:** Change `HighlightOverlay.Draw` to read everything from `style` instead of `config`. When `style.Label` is set and the head point projects on screen (`WorldToScreen(target.Position + (0, MarkerHeight, 0))`), draw it centred `6 * GlobalScale` below that point: a rounded rect `(0,0,0,0.55)` with 4/2 px padding (same as the edge-arrow label), text in the style colour (pulse alpha applied).
- [ ] **Step 3:** Update `Plugin.Draw` to call `overlay.Draw(target, self, OverlayStyle.From(Config))`.
- [ ] **Step 4:** Run `dotnet build TargetMe.sln -c Debug` and `dotnet test TargetMe.Tests`. Expected: 0 errors, all tests pass.
- [ ] **Step 5:** In game, reload and check `xiv_log`. Expected: TargetMe loads, no exceptions. (The hover look itself is unchanged; Task 6 step 6 checks the drawing.)
- [ ] **Step 6:** Commit "Let the highlight overlay take a style and a label".

### Task 6: PauseHotkey, unpause popup and wiring

**Files:**
- Create: `TargetMe/Game/PauseHotkey.cs`, `TargetMe/UI/UnpauseConfirmWindow.cs`
- Modify: `TargetMe/Configuration.cs` (new "Sync pause" group), `TargetMe/Services.cs` (+ `INotificationManager Notifications`), `TargetMe/Plugin.cs`

**Interfaces:**
- Consumes: `HotkeyBinding`, `HoldTracker` (Task 1); `SyncPauseRules` and its types (Task 2); `ISyncService` (Tasks 3–4); `OverlayStyle`, `HighlightOverlay.Draw` (Task 5); `TextInput.IsActive()` (Task 5); existing `ViewerWindow.Hovered`, `ViewerTracker.Resolve`.
- Produces:
  - `Configuration`: `HotkeyBinding PauseHotkey { get; set; } = new();`, `Vector4 PauseColor = (1.0f, 0.31f, 0.64f, 1f)`, `Vector4 UnpauseColor = (0.30f, 0.90f, 0.45f, 1f)`, `Vector4 NotPairedColor = (0.55f, 0.55f, 0.55f, 0.8f)`.
  - `sealed class PauseHotkey : IDisposable`: `PauseHotkey(Configuration config, ViewerTracker tracker, ViewerWindow viewerWindow, IReadOnlyList<ISyncService> services, UnpauseConfirmWindow confirm)` (keeps `confirm` in a private field named `confirm`); `void Update()` (framework); `void Draw(HighlightOverlay overlay)` (UI draw); `bool HasTarget { get; }`; `bool Capturing { get; set; }`; `HotkeyBinding? PollCapture()` (Task 7 uses it); `static bool IsDown(int vk)`; `internal void Preview(IPlayerCharacter? target, IReadOnlyList<SyncPluginState>? states)` (test seam; `null` clears it).
  - `Plugin`: `public PauseHotkey PauseHotkey { get; }`.
  - `sealed class UnpauseConfirmWindow : Window`: `void Open(IPlayerCharacter player, IReadOnlyList<string> plugins)`; `Action<ulong, string, IReadOnlyList<string>>? OnConfirm` (object id, name, plugins).

- [ ] **Step 1:** Add the configuration properties and `INotificationManager` to `Services`.
- [ ] **Step 2:** Implement `PauseHotkey.Update()`:
  - `IsDown(vk)`: `[DllImport("user32.dll")] GetAsyncKeyState(vk) & 0x8000`, and false unless `GetForegroundWindow()` equals the game's main window handle (cache `Process.GetCurrentProcess().MainWindowHandle` once).
  - Active = binding not empty && `!Capturing` && logged in with a local player && `!TextInput.IsActive()` && `!ClientState.IsGPosing` && none of `WatchingCutscene`, `WatchingCutscene78`, `OccupiedInCutSceneEvent` && `!confirm.IsOpen`.
  - `held = binding.IsHeld(IsDown)`; `released = tracker.Update(held, active)`.
  - While `tracker.Holding` and the key isn't a mouse button: `Services.KeyState[(VirtualKey)binding.Key] = false` when `KeyState.IsVirtualKeyValid` for it.
  - Target (each frame while holding, and on the release frame): `viewerWindow.Hovered` resolved through `tracker.Resolve`, else `TargetManager.MouseOverTarget as IPlayerCharacter` that isn't the local player.
  - On `released` with a target: fresh states from every service (`GetState(target, fresh: true)`), `SyncPauseRules.OnRelease`; for each `Pause` name, independently of the others, start `SetPaused(target, true)` and attach a continuation that shows `Paused {Name} ({Plugin})` or, on fault, `Couldn't pause {Name} in {Plugin}` plus a log error with the unwrapped exception. Then `confirm.Open(target, ConfirmUnpause)` when it isn't empty.
  - Continuations post the toast back with `Services.Framework.RunOnFrameworkThread` and skip it after `Dispose` (a `disposed` flag; `PauseHotkey : IDisposable`).
- [ ] **Step 3:** Implement confirm handling in `PauseHotkey` (subscribe to `confirm.OnConfirm` in its constructor): find the player with `ObjectTable.SearchById(id) as IPlayerCharacter` whose name matches; missing → warning toast `{Name} is no longer nearby`; else fresh states → `SyncPauseRules.StillPaused(fresh, plugins)` → `SetPaused(player, false)` for each with `Unpaused …` / `Couldn't unpause …` toasts.
- [ ] **Step 4:** Implement `PauseHotkey.Draw(overlay)`: the target and states come from the `Preview` values when set (skip the draw if that target is no longer valid), else (while holding, with a target and a local player) `GetState(target)` per service. Style: `OverlayStyle.From(config) with { Color = PauseColor | UnpauseColor | NotPairedColor by SyncPauseRules.Marker, Line = false, Ring = true, Marker = true, Label = SyncPauseRules.Label(states) }`. `HasTarget` is true when it drew.
- [ ] **Step 5:** Implement `UnpauseConfirmWindow` (title `TargetMe##UnpauseConfirm`, flags `NoCollapse | AlwaysAutoResize | NoSavedSettings`, `ShowCloseButton = false`, centred on the main viewport on appearing with pivot 0.5): the question text from Global Constraints, `Unpause` (calls `OnConfirm`, closes) and `Cancel` (closes); Esc closes while the window is focused. Wire in `Plugin.cs`: add the window to `windowSystem`; construct `PauseHotkey` after `ViewerWindow`; call `PauseHotkey.Update()` in `OnFrameworkUpdate`; in `Draw`, call `PauseHotkey.Draw(overlay)` and draw the hover overlay only when `!PauseHotkey.HasTarget`; dispose `PauseHotkey` before `windowSystem.RemoveAllWindows()`.
- [ ] **Step 6:** Build, run tests, reload. In game, through `xiv_exec`, set a preview on your own character and take `xiv_screenshot`, once per marker kind:

```csharp
var p = (TargetMe.Plugin)PluginInstance("TargetMe");
p.PauseHotkey.Preview(Objects.LocalPlayer, new List<TargetMe.Core.SyncPluginState> {
    new("Lightless", TargetMe.Core.SyncState.Synced), new("PlayerSync", TargetMe.Core.SyncState.Paused) });
Defer("clear sync pause preview", () => p.PauseHotkey.Preview(null, null));
```

  Expected: ring and head marker in the pause colour, no line, label `Pause · Lightless   Unpause… · PlayerSync` under the marker. Repeat with `[Paused, NotPaired]` (green, `Unpause… · Lightless`) and `[NotPaired, Unavailable]` (grey, `Not paired`). Then run `xiv_exec_cleanup` and screenshot again. Expected: the marker is gone.
- [ ] **Step 7:** In game, open the popup: `Call(Get(p, "PauseHotkey.confirm"), "Open", Objects.LocalPlayer, new List<string> { "PlayerSync" })`, screenshot, then click **Cancel** with `xiv_ui_input`. Expected: the popup reads `Unpause Lucina Zero in PlayerSync?`, closes on Cancel, and `xiv_log` shows no TargetMe errors and no pause/unpause log lines. Do not click **Unpause**.
- [ ] **Step 8:** Commit "Add sync pause hotkey and unpause confirmation".

### Task 7: Settings section

**Files:**
- Modify: `TargetMe/UI/SettingsWindow.cs` (constructor takes `PauseHotkey` and `IReadOnlyList<ISyncService>`; `DrawIntegrations` gets a "Sync pause" section above Moodles)
- Modify: `TargetMe/Plugin.cs` (pass them in)

**Interfaces:**
- Consumes: `PauseHotkey.Capturing`, `PauseHotkey.PollCapture()`, `PauseHotkey.IsDown` (Task 6); `HotkeyBinding.Display`, `IsModifier`, `IsMouse` (Task 1); `ISyncService.Name`, `Status` (Task 3).

- [ ] **Step 1:** Implement `PauseHotkey.PollCapture()`: Esc (0x1B) down → stop capturing, return null. Otherwise the first key down among `KeyState.GetValidVirtualKeys()` (as `int`) plus 0x05 and 0x06, skipping 0x01–0x04, 0x1B and `HotkeyBinding.IsModifier` keys → stop capturing and return a binding with `Ctrl/Shift/Alt` from `IsDown(0x11/0x10/0x12)`. Nothing down → null and keep capturing.
- [ ] **Step 2:** Draw the section:
  - Header `Sync pause`, then a dim line: `Hold the hotkey over a player to see their sync state. Release to pause them, or to unpause after a confirmation.`
  - A button showing `binding.Display(vk => ((VirtualKey)vk).GetFancyName())`, or `Press a key… (Esc cancels)` while capturing; clicking it sets `Capturing = true`. While capturing, call `PollCapture()` each frame; a returned binding is assigned to `Configuration.PauseHotkey` and `Save()`d. A `Clear` button next to it sets an empty binding and saves.
  - When the binding `IsMouse`: dim line `Mouse buttons also reach the game.`
  - Three colour pickers (existing `ColorFlags`): `Pause`, `Unpause`, `Not paired`, each saving on change.
  - One line per service: `{Name}: Connected` / `Disconnected` / `Not loaded` / `Unavailable on this version (missing: {Missing})`.
- [ ] **Step 3:** Build and reload. In game, open settings → Integrations with `xiv_ui_windows` / `xiv_ui_input`, screenshot. Click the hotkey button and send a key with `xiv_ui_input` if it can deliver one; otherwise ask the user to press `F` once. Expected: the button shows `F`, `Clear` shows `Not set`, both statuses read `Connected` or `Disconnected`, no errors in `xiv_log`.
- [ ] **Step 4:** Commit "Add sync pause settings".

### Task 8: README and verification

**Files:**
- Modify: `README.md` (feature line at the top; new checklist items under "Checks in game")

- [ ] **Step 1:** Run `dotnet test TargetMe.Tests`. Expected: all pass. Run `dotnet build TargetMe.sln -c Release`. Expected: 0 warnings from TargetMe code.
- [ ] **Step 2:** In game, reload, run Task 3 step 6 for both services, Task 6 step 6 once, and check `xiv_log`. Expected: no exceptions. Run `xiv_exec_cleanup`.
- [ ] **Step 3:** Add to the README: one sentence on the sync pause hotkey (where to set it, what holding and releasing do, that it uses Lightless / PlayerSync internals and can break when they update). Add these checklist items:
  - Holding the hotkey over a synced pair shows the pause colour and `Pause · …`; releasing pauses them. Check in Lightless and in PlayerSync.
  - Same for someone you only share a syncshell with, in each plugin.
  - Releasing over a paused pair opens the popup; Unpause unpauses them, Cancel doesn't.
  - **PlayerSync:** a paused, online pair is still found (the label says `Unpause… · PlayerSync`, not `Not paired`).
  - The bound key doesn't do its in-game action while you hold it.
  - Opening chat mid-hold and releasing does nothing.
  - Holding over one pair, moving to another and releasing acts on the second one only.
- [ ] **Step 4:** Commit "Document the sync pause hotkey".
