# Testing

## Unit tests

```bash
dotnet test LookAtMe.Tests
```

They cover the logic that doesn't need the game: list ordering and cap, same-name rows, emote chips, cooldowns, relative times, overlay geometry, the Esc gate, the fallback `/tell` command, the Moodles cache, hotkey matching, the hotkey's press/release handling and the sync pause rules.

## Checks in game

The following was checked through XIV Bridge: the plugin loads cleanly, the list has no title bar, and the move lock and its dashed border work. The rows' three states render, the context menu items appear in order, and Send Tell readies Chat 2. The ring, marker and edge arrow draw. Camera focus starts in front, and the camera is restored both on stop and when the plugin unloads mid-focus. The emote hook installs, the test sound plays, and every settings tab renders.

These need a second player, so check them yourself:

- [ ] Someone targets you: they appear in the "now" colour, and move to "earlier" when they untarget.
- [ ] They emote at you (/wave with you targeted): an emote icon appears next to their name and fades after the set time.
- [ ] Hover their name: the line, ring and marker point at them. Turn so they're behind you: the edge arrow appears.
- [ ] Right-click → Send Tell readies `/tell` to them in your chat input (checked with Chat 2).
- [ ] Send Tell with the vanilla chat (Chat 2 off). The chat should switch to Tell with them, and the input should open. If the input stays closed, `TellLauncher` needs an extra call to open it.
- [ ] Right-click → Focus Camera orbits them, and Esc puts the camera back.
- [ ] While focused, open the chat input and press Esc: the chat closes, but camera focus keeps going.
- [ ] With sound on, the alert plays when they start targeting you.
- [ ] The settings cog appears in the list's top-right corner while you hover the list.

### Sync pause hotkey

Checked through XIV Bridge (Lightless 3.3.0.0, PlayerSync 1.15.5.9, both disconnected): both plugins' internals resolve, the marker draws in all three colours with its label, the unpause popup is centred and closes on Cancel and Esc, and the settings section records a hotkey. Nobody was paused or unpaused during those checks. These need paired players online:

- [ ] Holding the hotkey over a synced pair shows the pause colour and `Pause · …`; releasing pauses them. Check in Lightless and in PlayerSync.
- [ ] Same for someone you only share a syncshell with, in each plugin.
- [ ] Releasing over a paused pair opens the popup; Unpause unpauses them, Cancel doesn't.
- [ ] **PlayerSync:** a paused, online pair is still found (the label says `Unpause… · PlayerSync`, not `Not paired`).
- [ ] The bound key doesn't do its in-game action while you hold it.
- [ ] Opening chat mid-hold and releasing does nothing.
- [ ] Holding over one pair, moving to another and releasing acts on the second one only.
