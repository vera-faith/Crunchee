# Crunchee

3D ASMR squishy-path game for iOS. A map is one really long path — each step is a new
ASMR item with its own squish, effect and sound.

## Where everything lives

- `Assets/` — the Unity project (Built-in Render Pipeline, iOS). Scripts in `Assets/Scripts/`, scenes in `Assets/Scenes/`.
- `Assets/Editor/ButterPathBuilder.cs` — builds Map 1 in one click (see below).
- `design/` — `spec.md` (locked design), `build-plan.md`, `characters/` (locked character
  concept art: Pudding Bunny, Tofu Cub, Peach Jelly Duck, Matcha Mochi Bear),
  `demo/` (concept art, UI mockup).
- `web-prototype/` — a playable browser prototype of the Butter Path (Three.js, no build step).

## First run (Unity)

1. Open this folder directly in Unity 6000.6.7f1.
2. Menu **Crunchee → Build All Themes** — generates both endless helix scenes and their shared meshes. Use **Build Butter Path Scene** for Bakery only or **Build Classic Butter Scene** for Classic only.
3. Open `Assets/Scenes/ButterPath.unity` (Bakery) or `Assets/Scenes/ClassicButter.unity` and press Play. WASD/arrows + Space in the editor; joystick + JUMP on device.

## Endless themes

- **Bakery Spiral** loops plain/wax butter, flour bags, vanilla-extract bowls and cracking croissants through a decorated bakery helix.
- **Classic Butter** loops yellow butter sticks with plain, sugar and 1–5 layer wax coatings. More layers add physical shell thickness, opacity, larger cracks, flakes and a deeper/longer crack sound.
- `Assets/Scripts/Game/SquishyThemes.cs` owns the reusable theme definitions and sequences. `EndlessSquishyPath.cs` owns helix placement, bounded ahead/behind recycling, environment-loop chunks and the slow course-driven camera orbit.
- The ten progress pips loop as the total cracked-item count grows; there is no finish platform. Falling respawns at the last landed squishy.
