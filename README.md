# Auto Timelapse: from Unbuilt or Saves, with Camera Presets, Video & Screenshots

A Cities: Skylines II mod that automatically creates timelapses of your city — either by progressively unbuilding it in reverse, or by cycling through your existing save files. Captures both **screenshots** and **video** from multiple **camera presets** at any time of day, with optional **cinematic recording** via OBS, **Carto map exports**, and **crash-resilient long sessions**.


![License](https://img.shields.io/badge/License-Custom-orange)

---

## Three timelapse modes

### Current Screenshots Session

Generates screenshots of your current game on the press of a button for all your presets.

### Auto Historic Timelapse (destructive)

Generates a **reverse timelapse** of your current city by gradually demolishing it in reverse build order. At each step, the mod:

1. Identifies the most recent roads/tracks (by build order)
2. Optionally marks adjacent buildings as construction sites (cranes, scaffolding)
3. Optionally runs the simulation for a few seconds (cranes animate, traffic moves)
4. Captures screenshots and/or videos from every camera preset, at every configured hour
5. Caputres cinematics if any is set, with OBS.
6. Destroys those roads and their adjacent buildings
7. Loops to the next batch

Reverse the resulting image sequence to obtain a true historic timelapse of how your city was built.

 **Destructive mode** — make a separate save copy first and do not save the game afterwards.

### Saves Screenshots Timelapse

Loads each of your saved games in chronological order (filtered as you wish), and captures screenshots/videos from each. Perfect if you have **100+ saves** of the same city across your gameplay history.

Optionally marks recent roads' areas as construction sites in each save, so the timelapse shows visible building activity.

 **Backup your saves first** — some assets/mods may corrupt old saves when they are opened.

---

## Features

### Camera presets

- Save and restore unlimited camera positions (position, zoom, rotation)
- Each preset can optionally capture full **Photo Mode** properties (DoF, exposure, ISO, focus distance, time of day, weather)
- Rename or delete presets via the in-game preset manager panel
- **Import / export** presets as JSON
- "Add current view as preset" button works from anywhere in-game

### Capture quality

- Choose between **screen resolution**, **Full HD (1080p)**, **QHD (1440p)**, **4K** or **8K** screenshots
- Auto-hide UI during capture for clean shots
- All highlight outlines suppressed during capture (no blue hover marks on screenshots/videos)

### Time and weather control

- Multi-hour capture: define a list of hours (e.g. `"6, 12, 18, 22"`) and the mod takes a screenshot at each one, for each preset, for each save
- **Force clear weather** mode with 3 options: always force, force except in photo mode, or off entirely
- Thanks to the photo mode of the base game, you can set the weather as you wish in details for any preset or cinematic to record/take a screenshot for.

### District filtering (Auto mode)

- Restrict destruction to specific districts: type names comma-separated (`"dist1,dist2,dist3"`)
- Districts are processed **in priority order** as listed
- Underground roads (tunnels) are not counted in progress but still destroyed

### Save filtering (Saves mode)

- Filter by **city name** (substring match)
- Filter by **save name prefix**
- **Skip N saves between each** processed save (thin out long histories)
- **Max saves** to process (0 = no limit)
- **Sort order**: most recent first, or oldest first
- **Resume from a specific save** by name (useful after interruption)

### Video recording (OBS integration)

- Records short video clips at each step / save / preset / hour via **OBS Studio WebSocket**
- Configurable host, port, password
- Adjustable recording duration per clip (3–10 seconds recommended)
- Adjustable simulation speed during recording (paused, x1, x2, x3)
- Test OBS connection button
- Auto-reconnects after a game restart mid-session
- **OBS 30.1+ recommended** for per-save video folder organization

### Cinematic recording

- Plays your in-game **Cinematic Camera** sequences and records them via OBS automatically for each step or saves.
- Specify which cinematics to record per save (comma-separated names)
- Each cinematic uses its own time, weather, and simulation speed settings
- Cinematics are recorded **in addition** to the per-preset clips
- Cancel a cinematic mid-playback with ESC

### Carto export integration

- Optional integration with the [Carto](https://mods.paradoxplaza.com/mods/87428/Windows) mod
- Auto-triggers a Carto export at each captured save (Saves mode) or each step (Auto mode)
- Produces GeoJSON / Shapefile / GeoTIFF in your Carto output folder
- Use QGIS or another GIS tool to render PNG maps from the exported data
- Configure Carto first to avoid any popup or problem in a Screenshot Session.

### Robustness for long sessions

- **Crash watchdog**: a background process monitors the game and automatically relaunches it if it crashes. The session resumes from where it stopped.
- **Restart game every N saves**: free memory and avoid leaks during very long overnight runs
- **Return to main menu between saves** (optional): ensures a fully clean World state between saves to avoid edge-case crashes
- **Reminder timer**: shows a notification every N minutes when no session is active to remind you to capture current city screenshots
- **Session persistence**: all state is saved on disk, sessions survive crashes
- **Action on completion**: do nothing, exit game, or shutdown the computer — perfect for unattended overnight sessions

Note: after 2-3 consecutive crashs the mod stop to try again and start the game normally. In all the case, crash are not normal in Vanilla game, these robustness options are only for highly modded games.

### UI and feedback

- Three custom React panels in-game: **Preset Manager**, **Session Progress**, **Auto Timelapse Progress**
- Live progress: phase, current save, current preset, current hour, screenshots captured, ETA
- Hotkeys during sessions:
  - **ESC** — stop session / cancel cinematic
  - **SPACE** — pause / resume
- Quick buttons: open screenshot folder, open videos folder, exit photo mode, open forum, send feedback email
- Auto-backup of camera presets in each session's folder (so you can recover your config later)

### Custom output folders

- Override the default screenshot folder (e.g. point to a different drive for space)
- Override the default video folder separately (videos take a lot of disk)
- Folders are auto-organized by city → session → preset → hour

### Debug actions

A full debug menu lets you test individual functions:

- OBS: record 5-second test, set record directory test
- Cinematics: list available, play first configured
- Camera: apply first preset, dump photo properties
- Time/Weather: set to 12h / 22h, force clear weather, restore defaults
- Auto Mode: count visible edges (total or per district), destroy/mark recent roads, move camera to most recent
- Saves: list filtered saves to log
- Carto: check availability, trigger export
- Lifecycle: restart game, start/stop watchdog, quit game
- Capture: take a screenshot now
- etc

---

## Installation

1. Subscribe to the mod on [Paradox Mods](https://mods.paradoxplaza.com/) *(link will be updated when published)*
2. Enable it in your Playset
3. Launch the game
4. Open the mod settings via Options → Auto Timelapse & Camera Presets

For video features, install [OBS Studio](https://obsproject.com/) (30.1+ recommended) and enable its WebSocket Server under Tools → WebSocket Server Settings.

For map exports, also install the [Carto](https://mods.paradoxplaza.com/mods/87428/Windows) mod.

---

## For other modders

This mod can be used as a **runtime dependency** by other mods (let me know if you have any issue with that). This is the preferred way to build upon this work.

If you want to copy specific code snippets, please read the [LICENSE](LICENSE.txt) carefully. Attribution is required, and some uses (publishing a competing mod, commercial use) require prior permission via Discord/email.

**Recommended approach**: contact me on Discord (`@microscraft`) before starting work on a similar mod — there may be a way to integrate your feature directly into this one as an extension and as a contributor.

---

## License

This project is licensed under a **custom license** based on CC BY-NC-SA principles. See [LICENSE.txt](LICENSE.txt) for the full text.

---

## Support & feedback

- See license or microscraft on discord.

For bug reports, please include:
- Your Cities: Skylines II version
- The log file of the mod (available in the mod directory)
- A description of what you were doing when the issue occurred
- Publish this on the forum topic please

There's also a built-in **Debug** tab in the mod settings to help diagnose problems.

---

## A small request

If you publish a timelapse video or any content created with this mod, please mention the mod name or link it. It motivates me to keep maintaining it!

I'm also looking to contact people working in the marketing department (or similar) of a public transport company (buses, tram, etc.) for a separate project. If that's you or someone you know, please reach out via the "Send email" button in the mod settings. Thanks!

---

## Credits

Created and maintained by `@microscraft`.

Thanks to the Cities: Skylines II modding community for tools, documentation, and inspiration — particularly the modding Discord for their work and community presence.

---

## Disclaimer

This mod modifies game state in ways that are intentionally destructive (Auto Historic Timelapse) or unusual (multi-save cycling). **Always back up all your saves before using it.**

- Do not save the game during or after an Auto Historic session — the city is in an inconsistent state by design.
- Some mods or assets may corrupt old saves when they are opened; copy your saves folder before running long Saves Timelapse sessions on 100+ old saves.

The author is not responsible for any data loss or corrupted saves resulting from misuse.