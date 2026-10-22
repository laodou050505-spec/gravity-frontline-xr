# Modular English UI — Gravity Frontline

User direction confirmed 2026-09-08: live numbers are rendered by Unity. Generate all feasible fixed graphics and text using Tripo Nano Banana. Cover must be English and composed of independent modules.

## Implemented

- Four independent title modules: GRAVITY FRONTLINE, key art, START GAME, EXIT GAME. Static cover text is baked into generated PNGs.
- Controller ray hover changes START to green and EXIT to coral, with slight scale feedback and optional haptic pulse. Only a hit-tested button press activates a menu action. Desktop mouse and keyboard focus are supported without displaying keyboard hints.
- Four generated combat panels; live numbers alone are Text components. IDLE / LOCKED / HOLDING and brief state feedback use generated sprites.
- Two generated results panels, with Unity values aligned to image rows; combat panels hide while results display.
- Desktop and Android use independent layout sizes. Android canvas follows the head in world space. Menu ray positions and directions transform through the XR tracking origin.
- NPOT texture resizing disabled to preserve proportions. HUD material is stereo compatible and avoids scene-depth occlusion of fixed labels.

## Assets

All four new atlases were generated with Tripo model `banana` (Nano Banana), status success, 5 credits each (20 total). Task IDs, source output URLs and request metadata are recorded in the original `Art/Tripo/ModularEnglish/*/tripo_task_*.json`; a concise manifest is `Work/ModularEnglishUI/generation-results.json`.

The existing title key art is reused from task 87afac78-b454-4555-a0cd-412afd29fec3. Source: Tripo. UI delivery files are in `Assets/GravityWarden/Resources/GravityUI`. Crops merely split generated atlases into independently placeable images; lettering is not recreated by code.

## Verify and rebuild

- `GravityRepulsion.Editor.GravityModularUiBuild.PrepareAndBuildMac` imports required textures, configures the title and combat scenes, validates the original project and builds `Builds/macOS/GravityModularReview.app`.
- Desktop `-gravityUiEvidence -gravityUiOutput <absolute-dir>` creates actual Unity camera renders of title/combat/results.
- Desktop `-gravitySmoke` exercises the existing combat loop including results, grab, collision, repulsion and ultimate through wave 3.
- For Android, copy Assets/Packages/ProjectSettings to the established English mirror `/Users/ld/Documents/Codex/2026-08-13/y-2/work/gravity_repulsion_pico_build`, then run `GravityRepulsion.Editor.GravityExistingSceneUpgrader.BatchBuildAndroid`.
- Final desktop renders are in `Evidence/ModularEnglishFinal`. They are Unity camera renders, not headset or emulator screenshots.

Tripo image generation is account-wide concurrency 1 by default; generate serially, with backoff for 429. See https://developers.tripo3d.ai/zh/docs/rate-limits .

## Final delivery

APK SHA-256: `b74aaf08cd04cc57d17d7ad188c129841b1c60153cbaf4681a3e81ab18db9fff`. Android build/config verification and desktop smoke through wave 3 passed. Covered installation to emulator-5554 succeeded; app was not launched because another project was foreground. No physical headset validation.
