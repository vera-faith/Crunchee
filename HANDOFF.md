# Crunchee — handoff notes (Nov 7, 2026)

Read this first if you're picking up the project cold. This describes the current working branch, not the older TestFlight build.

## What it is now
- 3D ASMR squishy game in Unity 6 (6000.6.7f1), Built-in Render Pipeline (NOT URP), iOS target.
- Two playable themes, each an endless upward helix with no finish platform:
  - **Bakery Spiral** (`Assets/Scenes/ButterPath.unity`): plain/wax butter, flour bags, vanilla-extract bowls and croissants, with bakery display columns, clouds, floating ingredients and tools.
  - **Classic Butter** (`Assets/Scenes/ClassicButter.unity`): yellow butter sticks with plain, sugar and 1–5 layer wax coatings, plus simpler cream/yellow decor.
- Twenty path slots per turn; fifteen reusable item templates per theme. Landings spawn ahead and remove items safely behind. With `ahead = 25` and `keepBehind = 5`, the expected active count is 31.
- The camera slowly orbits with the course, not with the bunny's facing. The bunny can turn freely.
- The ten progress pips loop with total cracked-item count. Falling respawns at the last landed squishy rather than sending a high player back to the start.

## Verified in this branch
- Commit `ba4f7fc` — taller labeled butter sticks, readable physical wax plates, contact-centered chips, improved exposure and coating effects.
- Commit `f1682c8` — both endless themes, bakery items, distinct synth sounds, theme-specific sky materials, map picker and recycling.
- Headless `Crunchee.EditorTools.CaptureTool.BuildAndCaptureAll` builds both scenes and writes phone-portrait shots to `Captures/Bakery/` and `Captures/Classic/`.
- Both scenes passed geometry/reference assertions and simulated landings through item 75 with exactly 31 active items.
- The older TestFlight build **1.0.0 (2)** was verified on the owner's iPhone on Nov 5. These new visuals have NOT yet been device-tested or uploaded.

## How the game is built (important)
- Scenes are GENERATED. Do not hand-edit `.unity` files; change the builder and rebuild.
- **Crunchee → Build Butter Path Scene** builds Bakery only.
- **Crunchee → Build Classic Butter Scene** builds Classic only.
- **Crunchee → Build All Themes** builds both while preserving shared mesh GUIDs and separate `bakerySky.mat` / `classicbutterSky.mat` assets.
- `Assets/Scripts/Game/SquishyThemes.cs` is the theme sequence/palette source of truth. Add future themes there first.
- `Assets/Editor/ButterPathBuilder.cs` builds the selected scene, templates, initial steps, environment chunks, bunny, camera, audio and UI.
- `Assets/Scripts/Game/EndlessSquishyPath.cs` owns helix positions, bounded recycling, environment-loop movement and course yaw.
- `Assets/Editor/ButterPathBuilder.BakeryItems.cs` builds the flour/vanilla/croissant gameplay items.
- `Assets/Editor/ButterPathBuilder.Wax.cs` builds butter, labels, sugar crystals, layered wax shells and Voronoi plates.
- **Crunchee → Build iOS (Xcode project)** exports all available scenes from `MapCatalog` to `Builds/iOS`. Build both themes first so both scene files exist.

## Capture and validation
- Headless:
  `Unity -batchmode -projectPath <repo> -executeMethod Crunchee.EditorTools.CaptureTool.BuildAndCaptureAll -quit -logFile Logs/batch.log`
- If the project editor is open, create `Temp/crunchee-capture-all.flag`. `CaptureTool.Tick` waits until Play mode and compilation are finished, then builds/captures both.
- `Temp/crunchee-capture.flag` captures Bakery only. `Temp/crunchee-export-sounds.flag` exports all synthesized sounds, including Flour, Vanilla and Croissant, to `Captures/Audio/`.
- Capture assertions check 15 templates, 30 initial steps, player/camera/environment references, closed 20-slot turns, theme-specific sky assets and bounded recycling through item 75.

## Visual and ASMR rules
- Butter tops use physical separated plates, not drawn crack-web decals. One layer is thinner/clearer; five layers are visibly thicker and more opaque, with larger separation and deeper/longer cracks.
- Wax plates flex again under footsteps and chips originate at the actual contact height/position.
- Sugar has white crystals plus persistent glitter and a granular burst. Flour uses a soft powder burst. Vanilla uses an amber liquid surface and splash. Croissant crust cracks without using wax audio.
- World labels use `Crunchee/LabelText`, which is depth-tested; do not revert to the default font material that drew labels through other objects.
- All sounds remain original procedural synth in `AsmrSynth.cs`; no ripped reference audio. Nov 7 retune matched to the owner's TikTok reference (@minetteasmr): wax cracks are dense Poisson swarms of sub-millisecond resonator clicks with power-law sizes over a crunchy low body (thicker wax = longer/lower/chunkier, plus chunk snaps and a body thump), EVERY footstep on wax crackles (a ~0.32-0.45 s click swarm that eases but never goes silent), and unwaxed squishies are gummy (resonant ~480-700 Hz "boing" pulsing with a ~20-30 Hz jelly wobble, mid-focused 300-800 Hz, highs roll off fast).
- Landing = soft base squish, then the coating crack about 0.07 seconds later and louder. Music ducks under cracks.
- The background music is an original ~34-second cozy tune (`CozyTune`, 112 BPM, 16 bars, seamless) composed in code: a sustained near-pure-tone ocarina/whistle lead with trills, slurred slides and slow-blooming vibrato over a held bass pedal and soft breathing pad — NO drums and no decaying piano, so the ASMR always sits clearly on top.
- Pudding Bunny design remains locked: glossy translucent pudding, huge round head, caramel cap/drips, long droopy lop ears with no pink inner ear, big glossy eyes, tiny pink nose, open smile/tongue, caramel collar, stubby limbs, cream tail. No fur/plush and no food-material redesign.

## Still needs device review
- Actual iPhone framing, performance, text size, sugar/wax transparency and contact animation timing.
- New flour/vanilla/croissant sound mix and haptic feel on hardware.
- The current endless item/environment loops are procedural and repeat; more theme variants can be added later without changing the recycler.
- The old straight-hall/candy builder code remains for reference but is not used by either active theme.

## Git rules for this repo
- GitHub `vera-faith/Crunchee` (private), working branch `devin/visual-overhaul`.
- NEVER commit `Assets/_Recovery/`.
- Make real grouped commits when work is verified. No backfilled, empty or artificial contribution commits.
- Do not push unless the owner explicitly asks.
