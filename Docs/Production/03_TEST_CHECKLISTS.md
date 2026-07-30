# BCaT Desktop Test Checklists (Windows · macOS)

Used for stakeholder/institutional validation on available desktop machines.
Hardware-dependent Quest and lower-end-institutional checklists live in
`04_DEFERRED_TESTS_QUEST.md` and `05_DEFERRED_TESTS_LOWEND_HARDWARE.md`.

## A. Full exhibit walkthrough
- [ ] Main menu appears (standard mode); Begin Experience loads the house through the loading screen
- [ ] Main house navigation: WASD + mouse look + sprint; collision along the main route
- [ ] Crosshair: faint dot → bright dot+ring over interactables → hidden in menus
- [ ] Parlor/living-room exhibits: prompt "Press E …" appears only for the focused exhibit
- [ ] Video exhibits (each of the 6): open, "Loading video…" → playback, E/Esc closes, prompt returns
- [ ] Photo album/slideshow: opens, arrows navigate, E/Esc closes, cursor and controls restored
- [ ] Meshell article notebook: opens, page/article buttons work, Esc closes
- [ ] Privacy Law exhibit: proximity prompt, opens, page buttons, E/Esc closes
- [ ] External-link exhibits: browser opens; app keeps running; no launch while any menu/modal is open
- [ ] Spatial audio toggles: "Press E to listen/pause" verb updates with state
- [ ] Historic well / Big Mama's Garden / mural workstation / backyard exhibits reachable and intact
- [ ] Black Kitchen: portal prompt → loading screen → arrival at spawn
- [ ] Black Kitchen stations: exactly one prompt at a time, only selected station toggles, exclusivity holds
- [ ] Black Kitchen exit: aim at exit → E → reflection modal (Stay / Exit Now, Esc/S/Enter/E/L) → return to kitchen-return spawn
- [ ] No competing E anywhere: with two exhibits side by side, only the focused one activates
- [ ] No double activation from one key press (router cooldown)

## B. Desktop shell
- [ ] Escape opens pause menu (not while an exhibit modal is open — modal owns Escape)
- [ ] Pause menu: Resume / Settings / Exhibit Directory / Reset Position / Return to Main Entrance / Quit to Main Menu / Quit Application
- [ ] Pause menu blocks world interaction + movement; cursor free; prompts hidden
- [ ] Quit paths ask for confirmation; kiosk mode hides quit entirely
- [ ] Reset Position: player moves to nearest safe point; camera/cursor/controls sane; exhibits unaffected
- [ ] Return to Main Entrance: media stops, interfaces close, player back at entrance
- [ ] Exhibit directory lists exhibits grouped by area with availability; Transcripts panel opens (reports "no approved transcripts" until content is provided)

## C. Settings persistence
- [ ] Every control in Display/Graphics/Audio/Controls/Accessibility changes behavior immediately (no dead settings)
- [ ] Resolution + windowed/fullscreen switching works; previously-missing display falls back to primary
- [ ] Change tier + volumes + text size → quit → relaunch → all persisted
- [ ] Corrupt settings.json (edit to garbage) → app starts with defaults, file backed up as .corrupt
- [ ] Reset All To Defaults restores everything
- [ ] Invert Y and sensitivity affect mouse look correctly

## D. Quality tiers (record per machine)
For each of Desktop Low / Standard / High: avg FPS, min FPS + location,
startup time, BK transition time, peak RAM, visual notes (shadow distance,
vegetation distance, LOD popping). Confirm tier switch applies without
restart and persists.

## E. Repeated scene lifecycle (automated)
- [ ] `<app> -bcatSmokeTest 5` exits 0; report shows: scenes=1 after every return, handles ≤ 1, managed/reserved memory stable across cycles, no duplicate rigs
- [ ] Manual: 5× BK entry/exit — audio never overlaps, prompts never go stale, cursor state correct each time

## F. Kiosk mode
- [ ] `-kiosk` launches fullscreen, straight into the house (no menu)
- [ ] Settings via pause menu show only Audio + Accessibility; no quit buttons
- [ ] Fixed tier from kiosk.config.json is active regardless of saved settings
- [ ] Idle for timeout with a video open: video stops, UI closes, player returns to entrance, prompts/cursor restored
- [ ] Idle while long-form media playing (allowResetDuringMedia=false): reset deferred until media ends
- [ ] Repeated idle resets (3×) remain stable
- [ ] Ctrl+Shift+Q held ≈2 s quits; Ctrl+Shift+F10 opens admin panel (paths shown)
- [ ] Relaunch after quit returns to kiosk state

## G. Offline
- [ ] Disconnect network → launch: app starts, no hangs
- [ ] All 6 videos play offline (packaged StreamingAssets)
- [ ] Black Kitchen loads offline (local Addressables)
- [ ] Remove a packaged video file → exhibit shows "This media is currently unavailable…" and closes cleanly; player never locked
- [ ] External-link exhibit offline: browser opens to error page; app unaffected
- [ ] Reconnect network: no restart required for remote-fallback media

## H. Media error handling
- [ ] Break remoteBaseUrl (config asset) with no local file → error message within 20 s (watchdog), E closes
- [ ] Player.log contains structured `[MediaError]` lines (exhibit, path w/o query, platform, error)
- [ ] No raw exception text shown to visitors

## I. Accessibility
- [ ] Subtitles toggle ready (no tracks yet — verify no phantom text appears)
- [ ] Text size Normal/Large/XL rescales menus, prompts, directory, transcripts
- [ ] High contrast restyles shell UI + crosshair + prompt (never exhibit artwork)
- [ ] Persistent prompts widens focus tolerance (prompt easier to keep)
- [ ] Menus fully keyboard-navigable (Tab/arrows + Enter), visible focus states
- [ ] Reduced motion: shell honors it (no added shake/animation); transitions remain simple fades
