## Debra Midi Player 1.2.1

Patch release after **1.2.0**: Piano Academy polish, practice label UX, portable update ZIP fix, header pin, keyboard layout presets, and playback fixes.

---

### Piano Academy

- **Hand colors:** left hand **blue**, right hand **green** (overlay legend, hand diagram, falling notes, and hand preview on the keyboard).
- **Exercises:** falling notes show **finger number** (center) and **note name** (bottom anchor) together.
- **Class songs (BB-S01–S03):** same dual labels; fingers inferred from pitch; notes colored by hand range (below middle C = left/blue, middle C and up = right/green).
- **Keyboard in Academy:** note names only (Do–Ré–Mi, C–D–E, or PC keys) — no finger numbers on keys.

### Practice labels (falling notes + keyboard strip)

- Anchor note labels at the bottom of falling notes: **+10 px taller**, text **vertically centered**, matching **rounded bottom edge**.
- **Black-key sharps on two rows** on falling notes and on the keyboard strip (e.g. `Fa` + `♯`, `C4` + `♯`) so narrow keys are not truncated.
- **English (C–D–E):** black keys on the full **88-key** piano show the note + sharp row again (were blank in 1.2.0).

### Header & window

- **Pin** chip in the header — toggle **always on top** (saved in settings).
- Header **version label** reads from the running `DebraMidiPlayer.exe` file version (fixes stale **v1.1.5** showing after a 1.2.x install).
- Improved header contrast and layout (version/subtitle readability, pin control no longer clipped).

### Playback fixes

- **Library / playlist playback:** removed double audio (game keys + built-in synth playing at the same time outside Practice). Practice preview sound still works when enabled in Practice settings.

### Keyboard layout presets

- **QWERTY / QWERTZ / AZERTY** presets in Settings and the key layout editor.
- Apply a preset from the editor; layout is saved and restored on next launch.

### Portable & updates (important fix)

- **v1.2.0 GitHub ZIP was wrong:** files were inside `release\portable\` (and some installs got nested `Assets\Assets\`). Extract did not overwrite in place.
- **v1.2.1 ZIP is flat:** `DebraMidiPlayer.exe`, `Assets\`, and `discord-catalogue.json` at the **archive root**.
- **How to update:** close Debra → extract **over** your existing folder (overwrite files) → run `DebraMidiPlayer.exe`.
- Release scripts now **verify** exe version matches csproj and **reject** nested ZIP layouts before publish.

---

### Install / update

1. On **v1.1.2+**: use the header **Update** button (after Discord manifest points to 1.2.1), or download **DebraMidiPlayer-1.2.1-portable.zip** from GitHub.
2. Close Debra. Extract **over** your portable folder — same level as your current `DebraMidiPlayer.exe`, not into a new subfolder.
3. Run **DebraMidiPlayer.exe**. Header should show **v1.2.1**. Users on **1.2.0** (or older) will see the **Update** button once the manifest is published.
