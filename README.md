# Look At Me

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FFXIV that shows who is looking at you: everyone targeting your character right now, the people who looked earlier. Additional functionality to pause sync partners with a simple hotkey on hover, as well as inspecting viewer's moodles and opening a messenger window to them. 

## What it does

- **Viewer list.** Players targeting you right now are bright, with a tinted row and a filled dot. Players who looked earlier are dimmed, and players who are no longer nearby are grey. Each row shows how long ago they looked.
- **Emotes aimed at you** appear as small icons next to the name, with a ×N count for repeats. They fade out after a while.
- **Hover a name to find them:** a clear line from you to them, a ring at their feet and a marker over their head. If they're off-screen, an arrow at the screen edge points their way.
- **Right-click a name:** Send Tell (readies `/tell` in your chat input, Chat 2 included), Open in Messenger (with XIV Instant Messenger), Target, Focus Camera (the camera orbits them, TargetZoom-style), Dote, Examine, Adventure Plate, the game's context menu, View Moodles and Copy Name. Dote, Examine, Adventure Plate, the game's menu, Moodles and Messenger can be hidden in settings.
- **Optional alerts:** a game sound or your own sound file, a quiet time per player, and muting in duties.
- **Sync pause hotkey** for Lightless Sync and PlayerSync users (see below).

## Installation

The plugin is distributed through my shared [DalamudPlugins](https://github.com/link-0402/DalamudPlugins) repository.

1. Open `/xlsettings` → **Experimental**.
2. Under **Custom Plugin Repositories**, add this URL and click **Save**:
   ```
   https://raw.githubusercontent.com/link-0402/DalamudPlugins/main/repo.json
   ```
3. Find **Look At Me** in `/xlplugins` and install it.

Moodles, XIV Instant Messenger, Lightless Sync and PlayerSync are optional. Their menu items and settings only appear while they're loaded.

## Using it

The list opens with `/lookatme`. It is locked in place: turn on **Allow moving and resizing** in settings (or use `/lookatme move`) to drag or resize it, then lock it again. The settings cog appears in the list's top-right corner while you hover it.

| Command | What it does |
| --- | --- |
| `/lookatme` | Show or hide the list |
| `/lookatme settings` | Open settings |
| `/lookatme move` | Unlock or lock the list for moving and resizing |
| `/lookatme stop` | Stop camera focus (Esc works too) |

### Sync pause hotkey

Set a key in settings → **Integrations**. Hold it over any player to see whether you sync with them in Lightless Sync or PlayerSync, and release it to pause them. If they're already paused, releasing asks before unpausing them. With **Block instead of pause** on, releasing blocks them instead (Block Pairing in PlayerSync, Blacklist Pair in Lightless); undo that in the sync plugin itself.

Neither sync plugin offers a public way to do this, so Look At Me reaches into their internals. An update to either of them can switch the hotkey off until Look At Me is updated. Settings show whether each one is connected.
