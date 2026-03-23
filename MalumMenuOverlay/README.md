# MalumMenu External Overlay

An external overlay for **Among Us** that works alongside the MalumMenu BepInEx plugin.
The overlay provides a floating GUI; the BepInEx plugin executes the actual cheats inside the game process.

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

> **Both components must be running at the same time.**  
> The overlay is just a GUI — the BepInEx plugin is what actually executes the cheats inside Among Us.

### Step 1 — Install the BepInEx plugin into Among Us

Follow the installation guide in the [main README](../README.md#️-installation).  
In short: download the latest MalumMenu release zip, extract it into your Among Us game folder, and launch the game once to finish the BepInEx setup.

### Step 2 — Start Among Us

Launch Among Us as you normally would (with the BepInEx plugin installed).

### Step 3 — Run the overlay

Start `MalumMenuOverlay.exe` **after** Among Us is already running.  
The overlay positions itself over the game window automatically.

### Step 4 — Toggle cheats from the overlay menu

Press **INSERT** to show the overlay menu, click any toggle to enable a cheat.  
The overlay writes the current toggle state to `%APPDATA%\MalumMenu\state.txt`; the BepInEx plugin reads this file every few frames and applies the changes inside the game.

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
├── CheatState.cs        – All toggle flags + profile save/load + IPC state writer
├── ProcessDetector.cs   – Finds the Among Us process / window rect
├── GameMemory.cs        – Low-level ReadProcessMemory helpers + PlayerSnapshot
├── MenuRenderer.cs      – Draws the interactive cheat menu (GDI+)
└── OverlayRenderer.cs   – Draws ESP tracers, player labels, and minimap
```

### How cheats are applied (IPC bridge)

The overlay cannot modify the game directly because it runs in a separate process.
Instead it uses a simple file-based IPC channel:

1. **Overlay side** — `CheatState.Set()` / `Toggle()` / `DisableAll()` write all current toggle
   states to `%APPDATA%\MalumMenu\state.txt` atomically (via a `.tmp` rename) on every change.

2. **BepInEx side** — `CheatToggles.KeybindListener.Update()` polls the file every 6 frames
   (~100 ms at 60 fps). When the file's last-write timestamp advances it reads the new values
   and applies them to the `CheatToggles` fields that Harmony patches and cheat methods read
   every frame.

This means any cheat toggle you click in the overlay is reflected inside the game within
roughly one tenth of a second.

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
- The overlay **requires** the MalumMenu BepInEx plugin to be installed in Among Us.
  Without it the toggles have no in-game effect.
- Some cheats (sabotage, meeting control, etc.) are shown as toggles in the UI and are
  already wired up through the IPC bridge. ESP tracers and the minimap require the
  `GameMemory.ReadPlayers()` stub to be replaced with the correct IL2Cpp pointer offsets
  for the Among Us build you target (use dnSpy or Cheat Engine to find them).
