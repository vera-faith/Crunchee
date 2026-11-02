# Unity setup — the one-time part only Faith can do (~15 min, mostly downloads)

Unity is not installed on the Mac yet, and installing it needs a Unity account
sign-in, which is a Faith-only step. After this one-time setup, mlem can drive
Unity (open the project, build the scene, press Play, fix errors) remotely.

## Steps

1. **Download Unity Hub** — https://unity.com/download → install the .dmg
   (drag Unity Hub into Applications, open it).
2. **Sign in / create a Unity ID** inside Hub.
   - **Best: get the free Unity Student plan first** — unity.com/products/unity-student
     → sign in → verify enrollment through SheerID (UCSD counts, 16+). It's free,
     gives the **Unity Pro** editor for 1 year (renewable while enrolled) plus
     bonus assets/tools, and covers commercial release under a $1M threshold for
     solo student projects. Unity emails a license key; add it in Hub
     (Settings → Licenses → Add).
   - Fallback: the free Personal license is fine to start (activate it when Hub
     asks — Personal → "I don't use Unity in a professional capacity" if it asks
     for revenue details). You can switch to the Student/Pro license later
     without reinstalling.
3. **Install an editor** — Hub → Installs → Install Editor → pick the current
   **Unity 6 LTS** (or 2022.3 LTS). On the modules screen, also tick:
   - **iOS Build Support** (needed later for TestFlight)
   - (Apple Silicon Mac, so no other platform modules needed)
   Wait for the download (~10+ GB with iOS module — do it on good wifi).
4. **Open the project** — Hub → Projects → Open / "Add project from disk" →
   choose `~/Developer/Crunchee`. First open takes a few minutes (it generates
   Library/ and ProjectSettings/).
5. Tell mlem "Unity is ready" — from here the Butter Path build + Play test
   can be driven remotely: menu **Crunchee → Build Butter Path Scene**, fix any
   red Console errors together, press Play.

## Notes

- The project folder must stay a Unity project root: `Assets/` at the top level
  is what Unity looks for. Don't move Assets into a subfolder.
- `design/` and `web-prototype/` living in the same repo is fine — Unity ignores
  everything outside `Assets/`, `Packages/`, `ProjectSettings/`.
- If Unity asks "project was created with a different version" on first open,
  just continue with the installed editor — the scaffold has no version-locked
  settings yet.
