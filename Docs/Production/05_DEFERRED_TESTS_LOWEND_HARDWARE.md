# Deferred User-Testing Package — Lower-End Institutional Hardware

**Status: NOT TESTED ON REPRESENTATIVE HARDWARE.** The desktop builds have
only been validated on the development machine (Apple M4 MacBook Air). No
claim is made about lower-end institutional performance until the project
owner completes this protocol; final minimum hardware specifications remain
pending these results.

## Required build
- Windows: `Builds/Windows64/BlackHomeplaces/` (copy whole folder)
- macOS: `Builds/macOS/BlackHomeplaces.app`

## Target machine class (record actuals)
Older institutional laptop · integrated graphics (Intel UHD/Iris, AMD Vega) ·
8 GB RAM where available · 1080p display · balanced/battery power profile.
Record: CPU, GPU, RAM, storage type, OS version, power state (plugged/battery).

## Protocol

### 1. Quality tiers
Run the walkthrough below once on **Desktop Low** and once on
**Desktop Standard** (Settings → Graphics → Quality tier).

### 2. Walkthrough route (each tier)
Main entrance → parlor/living-room exhibits → open one video exhibit (full
clip) → photo album / slideshow → article notebook → Privacy Law exhibit →
external-link exhibit (confirm browser opens) → backyard, well, garden →
Black Kitchen: enter, play 2 stations, exit via reflection modal → pause menu:
settings, exhibit directory, reset position, return to main entrance → quit.

### 3. Record per tier
- Average FPS during normal navigation: ______ (evaluation target ≈ 30+)
- Minimum FPS and where: ______
- Startup time (icon → controllable): ______ s
- Black Kitchen enter / exit transition times: ______ / ______ s
- Video start delay (packaged, offline): ______ s
- Task Manager / Activity Monitor peak RAM: ______ MB
- Frame pacing: smooth / occasional hitches / constant stutter
- Thermal/power throttling observed after 20 min: yes/no, notes
- Long session (45+ min): memory at start ______ MB vs end ______ MB

### 4. Stability checks
- [ ] No crash during the full route
- [ ] Repeat Black Kitchen entry ×5 — stable, no growing load times
- [ ] `-bcatSmokeTest 5` run passes (report in persistentDataPath/BCaT)
- [ ] Media never hard-locks the player (worst case: unavailable message + close works)
- [ ] Kiosk mode (`-kiosk -bcatKioskTimeout=120`): idle reset returns to entrance with media stopped, cursor/controls restored

## Acceptance criteria
Stable navigation and reliable interaction/media on Desktop Low at ≈30 FPS or
better on the representative machine, no crashes, no continuous memory growth
across the session, kiosk reset reliable.

## Results template
```
Date/tester:              Machine (CPU/GPU/RAM/OS):
Tier: Desktop Low   — avg FPS:      min FPS(where):      startup:      peak RAM:
Tier: Desktop Std   — avg FPS:      min FPS(where):      startup:      peak RAM:
BK enter/exit s:          Video start s:        Long-session RAM start→end:
Throttling notes:         Crashes/failures:
Kiosk reset OK:           Smoke test result:
Recommended minimum spec (owner's judgment):
```
