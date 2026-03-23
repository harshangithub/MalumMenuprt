# MalumMenu External Overlay

A standalone external overlay for **Among Us** that does not require BepInEx or DLL injection.

## Features

| Category | Cheats |
|---|---|
| **Player** | NoClip, Fake Revive, Invert Controls, Speed Boost, Moon Walk, Teleport to Cursor/Player, Kill/Eject/Report |
| **ESP** | Show Player Info, See Roles, See Ghosts, No Shadows, Task Arrows, Reveal Votes, Show Lobby Info, See Disguises |
| **Camera** | Zoom Out, Spectate, Freecam |
| **Tracers** | Crew / Impostor / Ghost / Body tracers; Color-based, Distance-based, Team-based modes |
| **Minimap** | Show Crew / Impostors / Ghosts; Color-based dots |
| **Doors** | Open / Close all doors; Spam modes; Auto-open on use |
| **Sabotage** | Reactor, Oxygen, Comms, Lights, Unfixable Lights, Mushroom |
| **Meeting** | Call / Skip / Close Meeting; Vote Immune |
| **Host** | Force Start, No Game End, No Options Limits; Protect / Roles menus |
| **Chat** | Always Chat, Unlock Characters, Bypass URL Block, Longer Messages, Lower Rate Limits |
| **Vents** | Use Vents, Walk Vent, Kick Vents |
| **Passive** | Unlock Features, Free Cosmetics, Avoid Bans, Copy Lobby Code, Stealth Mode |
| **Animations** | Shields, Asteroids, Empty Garbage, Scan, Cams In Use, Pet |
| **Config** | RGB Mode |

## How to Build

Requires **.NET 8 SDK** on Windows (or `EnableWindowsTargeting=true` on Linux/macOS).

```bash
# From the repository root:
dotnet build MalumMenuOverlay.sln -c Release

# Or from the project folder:
cd MalumMenuOverlay
dotnet build -c Release

# Produce a self-contained executable:
dotnet publish -c Release -r win-x64 --self-contained true -o bin/publish
```

The output `.exe` is written to `MalumMenuOverlay/bin/Release/net8.0-windows/MalumMenuOverlay.exe`
(or `bin/publish/MalumMenuOverlay.exe` when using `dotnet publish`).

## How to Use

1. Start **Among Us** first.
2. Run `MalumMenuOverlay.exe`.
3. The overlay will automatically position itself over the game window.

### Keybinds

| Key | Action |
|---|---|
| `INSERT` | Show / hide the cheat menu |
| `DELETE` | Disable all active cheats |
| `F10` | Save current toggle state to `MalumProfile.txt` |
| `F9` | Load toggle state from `MalumProfile.txt` |

Custom per-toggle keybinds can be set in `MalumProfile.txt` (auto-created on first F10 press).

## Architecture

```
MalumMenuOverlay/
├── Program.cs           – Entry point (STAThread WinForms app)
├── OverlayForm.cs       – Transparent click-through Form; Win32 window-style management
├── CheatState.cs        – All toggle flags + profile save/load
├── ProcessDetector.cs   – Finds the Among Us process / window rect
├── GameMemory.cs        – Low-level ReadProcessMemory helpers + PlayerSnapshot
├── MenuRenderer.cs      – Draws the interactive cheat menu (GDI+)
└── OverlayRenderer.cs   – Draws ESP tracers, player labels, and minimap
```

### Click-through behaviour

When the menu is **hidden**, the overlay window has `WS_EX_TRANSPARENT` set so all
mouse/keyboard events pass directly to the game. When the menu is **shown** (INSERT),
`WS_EX_TRANSPARENT` is removed so the menu responds to mouse clicks.

### Memory reading

`GameMemory.cs` provides generic `ReadProcessMemory` wrappers. The `ReadPlayers()`
stub returns an empty list by default; replace it with the correct IL2Cpp pointer
offsets for the Among Us build you target (use dnSpy or Cheat Engine to find them).

## Notes

- The overlay is excluded from most screen-recording tools because it is a layered
  top-most window, not a DirectX surface — ideal for streaming just the game.
- No DLL injection or BepInEx is required to run the overlay.
- Some cheats (sabotage, meeting control, etc.) require writing to game memory or
  sending network packets and are shown as toggles in the UI; wire them up in
  `GameMemory.cs` / a new `GameActions.cs` once you have the correct offsets.
