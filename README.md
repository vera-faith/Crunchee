# Crunchee

3D ASMR squishy-path game for iOS. A map is one really long path — each step is a new
ASMR item with its own squish, effect and sound.

## Where everything lives

- `Assets/` — the Unity project (URP, iOS). Scripts in `Assets/Scripts/`, scenes in `Assets/Scenes/`.
- `Assets/Editor/ButterPathBuilder.cs` — builds Map 1 in one click (see below).
- `design/` — `spec.md` (locked design), `build-plan.md`, `characters/` (locked character
  concept art: Pudding Bunny, Tofu Cub, Peach Jelly Duck, Matcha Mochi Bear),
  `demo/` (concept art, UI mockup).
- `web-prototype/` — a playable browser prototype of the Butter Path (Three.js, no build step).

## First run (Unity)

1. Unity Hub → New project → 3D (URP), name it Crunchee — or open this folder directly as the project.
2. Menu **Crunchee → Build Butter Path Scene** — generates the whole map:
   start platform → 10 wax-coated butters (1→10 layers, low-opacity wax) → finish,
   bird's-eye camera, placeholder bunny, joystick (bottom-left) + JUMP (bottom-right).
3. Press Play. WASD/arrows + Space in the editor; joystick + JUMP button on device.

## Map 1 — Butter Path (locked design)

10 butter sticks. Each has 1 more wax layer than the last, so the crack grows
audibly step by step. Jump on a butter → wax cracks → you sink a bit into the butter → next.
Wax is low-opacity: butter always shows through, each layer just a slightly whiter cast.
Camera is bird's-eye-ish (~55°) so the squishies read clearly.
