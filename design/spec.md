# Crunchee — Spec v2 (3D ASMR game)
*Idea from Faith, Oct 31, 2026. Reference: two Roblox gameplay videos by @minetteasmr she sent that day.*
*v2 correction from Faith (Oct 31, 17:17): this is a 3D immersive game, deliberately DIFFERENT from her other projects — do not reuse the HaloHalo/Yun approach. Quality bar is ABOVE the Roblox reference: better visuals, better step-on effects, best-in-class ASMR sounds.*

## What it is
A 3D immersive App Store game: climb / explore worlds made of giant squishies. Every object you step on squishes, reacts with a visual effect, and plays its own ASMR sound. Original characters and art (not Pusheen, not the Roblox models) — same *idea*, much higher quality.

## The quality bar (Faith's #1 priority)
1. **Visuals better than the reference** — soft glossy 3D materials, real lighting/shadows, pastel worlds you feel inside of, not flat Roblox blocks. Shiny, clean, game-looking.
2. **Step-on effects** — every landing: deep squash-and-stretch deformation, ripple/wobble that settles, particles unique to the material (sugar sparkles, honey drips, wax flakes, gloss shine), camera micro-bounce, matched haptic.
3. **Sounds people actually love in ASMR** — close-mic'd, layered, no cheap synth beeps. Each material + each coating gets its own sound design (see Coatings).
Sounds are original recordings / produced audio — no ripped Roblox/TikTok audio.

## Core loop
Pick a character → pick a themed map → move/jump through it → reach the end → unlock characters, coatings, maps.

**Map design (Faith, Oct 31 17:59 — simplified, this is the actual design):**
A map is **one really long path**. Each step on the path is a new ASMR item, and each step has its own squish/effect/sound. As simple as that. No tower sections, no branching — a long satisfying sequence of steps.
The character starts on a plain start platform, can **move around freely in any direction**, and there is a **jump button**. Jump/step on an item → its effect fires → continue to the next step.

**First stretch built: the Butter Path (10 butters).**
10 butter sticks in a row along the path. Every butter is wax-coated, and **each step has 1 extra layer of wax than the step before** (step 1 = 1 layer … step 10 = 10 layers), so you can *hear* the difference as you go:
- More layers = thicker, louder, longer crack; more wax flakes
- **Wax is LOW-OPACITY (Faith, Oct 31 18:04):** 1 layer = a thin white cast over the butter, butter clearly visible underneath; each extra layer is a slightly whiter cast, but the butter ALWAYS still shows through — never a fully opaque white block. (Visual model: ~0.12 opacity per layer, capped ~0.75.)
- After the wax cracks, the bunny **sinks a bit into the soft butter** underneath, then continues to the next step

**Camera + controls (Faith, Oct 31 18:04):**
- POV is **bird's-eye-ish** (high ~55° follow) so you can see the squishies on the path properly
- On-screen controls: **virtual joystick bottom-left** (move), **big JUMP button bottom-right**
- UI mockup: `crunchee-demo/crunchee-butter-path-ui-mockup.png`
This layer-progression is a reusable pattern: any coating can stack layers to build sound intensity along a path.

Controls (mobile 3D): drag / virtual joystick to move freely, on-screen **jump button**, third-person camera following behind/above like the reference videos.

## MUST-HAVE FEATURES (Faith, Oct 31)

### 1. Butter squishy — with coatings
A butter stick squishy you can step/bounce on. It comes in 4 coated variants, coating changes look, effect AND sound:
- **Wax coated** — matte cracked shell; stepping cracks the wax with a crisp crackle, flakes fly, soft butter squishes underneath
- **Sugar coated** — sparkling crystal crust; crunchy crystalline crunch, sugar sparkles burst
- **Honey coated** — glossy amber drip; slow sticky squish, honey strings stretch and snap, deep sticky pull sound
- **Nail polish coated** — hard glossy shiny shell; sharp glossy tap/click, high-shine reflection, then soft squish

### 2. ASMR keyboard
A giant keyboard you walk across. Every key is pressable, sinks down, springs back, and has a really nice clicky sound. Coated key variants change the click:
- **Wax coated keys** — crackly muted click
- **Sugar coated keys** — crunchy sparkly click
- **Honey coated keys** — sticky soft click with a pull-off sound
- **Nail polish coated keys** — hard glossy ultra-clicky clack
Different rows / keys can carry different coatings so walking the keyboard plays a little ASMR melody of clicks.

### 3. Every squishy has its own ASMR
Base set (from the reference + more): paw pad (soft pop), melon (juicy thud), strawberry mochi (sticky squish), donut (airy poof), chocolate (snap), grapes (pop cluster), cake sponge (deep soft bounce), macaron (click), butter (see above), keyboard keys (see above). Coating system multiplies every base object × {plain, wax, sugar, honey, nail polish}.

## Maps — themes developed together with Faith
NOT themed around HaloHalo. Faith + mlem will develop the theme list together. Starting directions she named:
- **Bakery** — butter, croissants, cake, jam, keyboard? no — bakery props
- **Holidays** — seasonal: e.g. Christmas (gingerbread, candy canes, ornaments), Halloween, etc.
More themes TBD together before build locks. Each theme = new 3D environment, object set, color/lighting mood, and sound palette.

## Characters — pick from multiple (feature, not just one hero)
Players can choose their character. **ROSTER LOCKED Oct 31 17:34 — the 4 Faith chose:**
1. **Pudding Bunny** — cream flan bunny with glossy caramel drip, floppy ears
2. **Tofu Cub** — v3 LOCKED Oct 31 17:43 ("yes perfect"): same original design (rounded cube silhouette, pom-pom ears, arms/feet, face — v2 changed too much and was dropped), texture changed only: frosted soft-tofu, milky semi-opaque, silky satin sheen, no bubbles, no gummy transparency
3. **Peach Jelly Duck** — swapped from Peach Jelly Cat Oct 31 17:37 at Faith's request: translucent bubbly peach jelly duck, orange beak, webbed feet; v2 (17:43): wings FOLDED down against its sides instead of spread out
4. **Matcha Mochi Bear** — pale green powdered mochi bear, cream swirl on top
Concept images in `asmr-characters/`. Not chosen (dropped): Mochi Cat (original), Fat Otter / Fat Panda / Fat Raccoon (the Pinterest-board round Faith said nvm to), Strawberry Milk Puppy, Butter Hamster.
Style rule from Faith's picks: characters are made OF food/squishy material (pudding, jelly, mochi), glossy — not fur animals.
Characters squash-and-stretch when they land, matching the squishies.

## Tech direction (new for this project)
- **3D engine: Unity** is the recommended route for this quality bar on iOS (real 3D physics/deformation, particle effects, lighting, App Store pipeline) — to confirm with Faith; native SceneKit/RealityKit is the fallback but is slower to reach this polish. This project does NOT use the SwiftUI+SpriteKit stack of HaloHalo.
- 3D models: simple rounded primitive-based models with high-quality materials/shaders can hit the glossy look without AAA modelling; deformation via scale/spring animation, no complex soft-body physics needed.
- Audio: produced ASMR library built per material × coating; spatial/volume variation so repeated steps don't sound identical (pitch/volume micro-variation).

## v1 scope (to lock with Faith after themes + characters)
- Character select (3 characters)
- 1 polished map first (theme TBD together) with butter + keyboard features included, full coating system
- Then additional themed maps as updates / unlocks
Explicitly not v1: multiplayer, IAP, ads, accounts.

## Open decisions
1. Faith's notes on the 3 character images (keep / change what)
2. Theme list + first map (develop together)
3. Engine confirm: Unity

## Name — Crunchee
Faith proposed **Crunchee** (Oct 31). Checked Oct 31 via Apple's iTunes Search API across US / PH / CN / JP / GB: **zero apps named exactly "Crunchee"** (PH/CN/JP: no results at all; US/GB: only unrelated fuzzy match "The Salad Kitchen"). Web search found no game/app brand named Crunchee; nearest is an Android game "Crunchy Butter: Escape Game" (different name; butter-parkour concept — worth noting, not a name conflict).
Final gate remains App Store Connect record creation (names can be reserved without a public listing — the HaloHalo lesson). Fallback if ASC rejects the exact name: **"Crunchee: ASMR Squish"**, home-screen name stays Crunchee.
Peach Jelly Duck v2 (folded wings) locked by Faith Oct 31 17:47.
