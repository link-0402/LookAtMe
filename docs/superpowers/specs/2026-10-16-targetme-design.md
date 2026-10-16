# TargetMe — Design

Date: 2026-10-16
Status: approved in chat, awaiting spec review

## Purpose

A Dalamud plugin (API 15) that shows who is targeting your character. It is written from
scratch, covers everything Peeping Tim does, and has a much better UI: a header-less list
that sets current viewers apart from earlier ones, shows the emotes they aim at you, lights
them up in the world when you hover their name, and lets you tell, target or point the
camera at them.

References (read for behaviour only, no code copied):
- Peeping Tim — `github.com/lnjanos/peepingtim` (manifest: `SamplePlugin/yuck.json`)
- TargetZoom — `github.com/sadoniaveletoria/TargetZoom` (camera focus)

## Feature parity with Peeping Tim

| Peeping Tim | TargetMe |
|---|---|
| Track players whose target is you; active / earlier / unloaded states; last-seen time; 15 most recent | Same. The cap is configurable (default 15). History lasts for the session (cleared on logout). |
| Left click a name: target them | Same |
| Hover a name: set them as focus target | **Replaced** by the world overlay. The focus target is never touched. |
| Right-click: Send Tell (message box), Dote, Adventure Plate, Examine, game context menu, Moodles buffs | Same items, plus **Target** and **Focus Camera**. Send Tell no longer opens a window of its own; it readies the tell in the regular chat input instead (see "Send Tell"). Each optional item can be switched on or off in settings. |
| Sound alert: on/off, volume, custom file (wav/mp3/aiff/wma/aac), cooldown per viewer, play while window closed, mute in duties | Same. The default sound is a game sound effect (`<se.1>`–`<se.16>`, selectable) instead of a bundled wav. A custom file is played with NAudio. |
| Hide window in duties; open window on login | Same |
| Colour settings | Same, extended for the new UI |
| CrystalSync Discord forwarding (IPC, colour, cooldown) | Not included (removed at the user's request) |
| `/ptim`, `/ptimconfig` | `/targetme` (toggle window), `/targetme settings`, `/targetme move` (toggle the move/resize lock), `/targetme stop` (stop camera focus) |
| Hidden "Stalk" feature (hard-disabled in Peeping Tim) | Not included |

## New behaviour

### Viewer window

- No title bar, ever. Flags: `NoTitleBar | NoScrollbar | NoCollapse | NoFocusOnAppearing`,
  plus `NoMove | NoResize` while locked.
- Locked by default. "Allow moving and resizing" in settings (or `/targetme move`) unlocks
  it. While unlocked it draws a dashed accent border and a resize grip, and the window can be
  dragged anywhere on its background.
- Background opacity is configurable (default 0.75).
- One row per viewer: status dot, name, emote chips, time since they last looked (right-aligned:
  "now", "45s", "3m", "1h").
  - **Targeting you now:** name in the "active" colour, filled dot, a soft row background
    tint in the active colour.
  - **Earlier, still loaded:** name in the "earlier" colour (dimmed), hollow dot.
  - **Earlier, not loaded:** name in the "away" colour (grey), hollow dot. Overlay, Target,
    Focus Camera, Dote, Examine and the game menu are unavailable.
  - Order: targeting now first, then by last seen, newest first. Capped at N rows.
- Empty state: a single dim line "No one is looking at you."
- A small cog button appears in the top-right corner only while the window is hovered and
  opens settings.
- Left click a row: target them. Right click: the context menu.

### Emote chips

- Emotes that another player aims at you (their emote target is your character) are attached
  to that player's row as chips: the emote's game icon (Lumina `Emote.Icon`, loaded with
  `ITextureProvider.GetFromGameIcon`), with the emote name and "N s ago" as a tooltip.
- If the player is not in the list yet, they are added (they are targeting you by definition).
- A chip lives for a configurable time (default 15 s, 0 = keep until the session ends) and
  fades out over its last 2 s. At most 5 chips per row; the oldest drops first. Repeating the
  same emote refreshes that chip and shows a ×N count.
- Toggle: "Show emotes" (default on).

### Hover overlay

While a row is hovered (or its context menu is open), and the player is loaded:
- A line from your character (chest height) to theirs.
- A ring on the ground at their feet (a projected 3D circle, radius 0.7 yalms).
- A downward marker above their head.
- If they are off-screen or behind the camera: an arrow at the screen edge pointing toward
  them, with their name.
- Drawn on ImGui's background draw list, so plugin windows stay above it.
- Settings: colour (default warm magenta `#FF4FA3`), line thickness, and on/off switches for
  line, ring and marker, plus a gentle pulse (on by default).

### Context menu

Order: **Send Tell**, **Target**, **Focus Camera** (shows **Stop Focusing** when focused on
that player), separator, Dote, Examine, Adventure Plate, Game Context Menu, View Moodles
(only when Moodles IPC is loaded and they have statuses), separator, Copy Name.

### Send Tell

There's no custom message window. Send Tell readies a tell to `Name@World` in the regular chat
input and focuses it, so you only type the message and press Enter:
- **Vanilla chat:** do what the game's own "Send Tell" context menu entry does. Switch the
  chat channel to Tell with that player as the recipient
  (`RaptureShellModule.SetContextTellTarget(name, world, worldId, accountId, contentId, reason,
  setChatType: true)`), then open and focus the chat input (`AgentChatLog`).
- **Chat 2:** Chat 2 follows the game's channel and tell target, so the same switch readies
  its input too. If Chat 2 is loaded and does not pick it up, fall back to the next step.
- **Fallback:** if the channel-switch function isn't available on this game version, send
  `/tell Name@World` with no message. The game treats that as "switch to tell with this
  person" in both chats.
- The exact function and how Chat 2 behaves are checked in the running game through XIV Bridge
  before being relied on.

### Camera focus (TargetZoom-style)

- Hooks the world camera's vtable functions for camera target (index 18), camera position (16)
  and can-change-perspective (23), the same layout Cammy/Hypostasis use. While focused, the
  camera orbits the chosen player.
- On start: save your zoom, angles and min-zoom; set zoom to the configured distance (default
  4 yalms); optionally swing the camera to face them; lower min-zoom for close-ups.
- Stops on: Esc (not while typing), the menu item again, `/targetme stop`, the player
  unloading or leaving range, zoning, cutscenes, and optionally combat. On stop, it restores
  your saved zoom and angle (or puts the camera behind you, configurable).
- If the camera can't be found or hooking fails, Focus Camera is hidden and a warning is logged.

## Architecture

```
TargetMe/
  Plugin.cs                  composition root, commands, lifecycle
  Services.cs                [PluginService] statics
  Configuration.cs           all settings + Save()
  Core/                      pure logic, no Dalamud references (unit-tested)
    ViewerRecord.cs          name, world, content id, state, first/last seen, emote chips
    ViewerList.cs            ordering + cap
    EmoteChips.cs            add/refresh/expire/fade maths
    Cooldowns.cs             per-key cooldown gate (sound)
    RelativeTime.cs          "now / 45s / 3m / 1h"
  Tracking/
    ViewerTracker.cs         per-frame scan, owns ViewerRecords, raises ViewerStarted
    EmoteWatcher.cs          EmoteController.PlayEmote hook -> (instigator, emoteId, target) -> tracker
  Game/
    PlayerActions.cs         target, dote, examine, plate, game menu, copy name
    TellLauncher.cs          ready a tell in the chat input (vanilla / Chat 2 / fallback)
    CameraFocus.cs           camera hooks + start/stop/restore
    ChatSender.cs            send a chat command like typed input (sanitised, length-checked)
  Alerts/
    SoundAlert.cs            game SE or NAudio file, volume, duty mute
  Integrations/
    MoodlesIpc.cs            optional IPC + status types
  UI/
    ViewerWindow.cs
    ViewerRow.cs             one row: dot, name, chips, time
    ViewerContextMenu.cs
    HighlightOverlay.cs
    MoodlesPopup.cs
    SettingsWindow.cs        tabs: General, List & Colors, Overlay, Sound, Camera, Integrations
TargetMe.Tests/              xUnit, compiles Core/*.cs directly
```

### Data flow

1. `Framework.Update` → `ViewerTracker.Update()` scans `IObjectTable` for players whose
   `TargetObjectId` is the local player. It creates or updates records, sets `IsTargeting`,
   refreshes `IsLoaded` and the last known object id (throttled to every 100 ms), and raises
   `ViewerStarted` when a player goes from not-targeting to targeting.
2. `ViewerStarted` → `SoundAlert`, gated by `Cooldowns`.
3. Emote hook → `EmoteWatcher` keeps only events whose target is the local player →
   `ViewerTracker.AddEmote(instigator, emoteId)`.
4. UI draw reads `ViewerList.Visible(records, cap)`; `HighlightOverlay` gets the hovered
   record from the window each frame and draws it.
5. Records are keyed by `Name@HomeWorldId`. They are cleared on logout.

### Emote hook

Hook `FFXIVClientStructs` `EmoteController.PlayEmote(uint emoteId, PlayEmoteOption* option)`
("plays an emote for a character that's not the local player"). Its address comes from
ClientStructs (`EmoteController.Addresses.PlayEmote`), so the signature is maintained upstream.
In the detour, `controller->OwnerObject` is who is emoting and `option->TargetId` is their
target. Only player characters whose target is the local player are kept. Before relying on it,
check through XIV Bridge that the address resolves and the hook installs. If it doesn't, emotes
are disabled, a warning is logged, and settings show "Emote detection unavailable on this game
version".

### Error handling

- The optional Moodles IPC is probed every time it's used. If one isn't
  there, its feature is hidden and nothing is shown in chat.
- Every hook is created in a try/catch. A failed hook disables only its feature.
- Game agent calls (Examine, Adventure Plate, context menu) run on the framework thread and
  check for null agents and objects first.
- Dispose order: stop camera focus (restores the camera) → dispose hooks → unregister events
  and commands → remove windows.

## Testing

- `TargetMe.Tests` (xUnit): ordering and cap; state transitions of `ViewerRecord`; emote chip
  add, refresh, ×N, expiry, fade alpha, and the 5-chip limit; cooldown gate; relative time
  formatting.
- In game through XIV Bridge (dev plugin, Debug build):
  - The plugin loads with no errors in dalamud.log.
  - The window draws with no title bar, locked; the move toggle unlocks it.
  - The emote hook resolves (logged on load).
  - Settings tabs render.
  - Send Tell readies `/tell` to a test name in the chat input (vanilla chat, and Chat 2 if
    it's installed) without sending anything.
- Checks only the user can do (needs a second player): a real viewer appears and is coloured
  correctly, an emote chip appears, the hover overlay points at them, and Focus Camera / Tell
  work.

## Out of scope

- Peeping Tim's disabled "Stalk" feature.
- History saved across sessions (session-only was chosen).
- Publishing (repo.json, GitHub release). That can follow once it works in game.
