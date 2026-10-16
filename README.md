# TargetMe

A Dalamud plugin for FFXIV that shows who is looking at you.

- **A clean list with no title bar.** Players targeting you right now are bright, with a tinted row and a filled dot. Players who looked earlier are dimmed, and players who are no longer nearby are grey. Each row shows how long ago they looked.
- **Emotes aimed at you** appear as small icons next to the name, with a ×N count for repeats. They fade out after a while.
- **Hover a name to find them:** a line from you to them, a ring at their feet and a marker over their head. If they're off-screen, an arrow at the screen edge points their way. Your focus target is never touched.
- **Right-click a name:** Send Tell (readies `/tell` in your chat input, Chat 2 included), Target, Focus Camera (the camera orbits them, TargetZoom-style), Dote, Examine, Adventure Plate, the game's context menu, View Moodles and Copy Name.
- **Optional alerts:** a game sound or your own sound file, a quiet time per player, and muting in duties.

The window is locked in place. Turn on "Allow moving and resizing" in settings (or use `/targetme move`) to drag or resize it, then lock it again.

## Commands

| Command | What it does |
| --- | --- |
| `/targetme` | Show or hide the list |
| `/targetme settings` | Open settings |
| `/targetme move` | Unlock or lock the list for moving and resizing |
| `/targetme stop` | Stop camera focus (Esc works too) |

## Install as a dev plugin

1. Build: `dotnet build TargetMe/TargetMe.csproj -c Debug`
2. In game, open `/xlsettings` → Experimental. Add `<repo>\TargetMe\bin\Debug\TargetMe.dll` to Dev Plugin Locations, then save.
3. Enable TargetMe in `/xlplugins` → Dev Tools.

## Tests

```bash
dotnet test TargetMe.Tests
```

The unit tests cover the logic that doesn't need the game: list ordering and cap, same-name rows, emote chips, cooldowns, relative times, overlay geometry, the Esc gate, the fallback `/tell` command and the Moodles cache.

### Checks in game

The following was checked through XIV Bridge: the plugin loads cleanly, the list has no title bar, and the move lock and its dashed border work. The rows' three states render, the context menu items appear in order, and Send Tell readies Chat 2. The ring, marker and edge arrow draw. Camera focus starts in front, and the camera is restored both on stop and when the plugin unloads mid-focus. The emote hook installs, the test sound plays, and every settings tab renders.

These need a second player, so check them yourself:

- [ ] Someone targets you: they appear in the "now" colour, and move to "earlier" when they untarget.
- [ ] They emote at you (/wave with you targeted): an emote icon appears next to their name and fades after the set time.
- [ ] Hover their name: the line, ring and marker point at them. Turn so they're behind you: the edge arrow appears.
- [ ] Right-click → Send Tell readies `/tell` to them in your chat input (checked with Chat 2).
- [ ] **Required before release:** Send Tell with the vanilla chat (Chat 2 off). The chat should switch to Tell with them, and the input should open. If the input stays closed, `TellLauncher` needs an extra call to open it.
- [ ] Right-click → Focus Camera orbits them, and Esc puts the camera back.
- [ ] While focused, open the chat input and press Esc: the chat closes, but camera focus keeps going.
- [ ] With sound on, the alert plays when they start targeting you.
- [ ] The settings cog appears in the list's top-right corner while you hover the list.
