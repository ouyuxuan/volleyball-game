# Sandspike (working title) — 2v2 beach volleyball, Unity 6000.6.8f1 URP

An original arcade volleyball game. It must stay original: no other game's name, characters, art,
music, levels or look.

## Which model does what

- **Fable (`claude-fable-5-2`)**: planning, design and architecture decisions, reviewing finished
  work, and genuinely hard problems (a bug that resists two attempts, tricky math or physics).
- **Sonnet 5.5 (`claude-sonnet-5-5`)**: writing and editing code, scene and asset setup, tests,
  builds, commits and other routine work.

If you are running on Fable and there is code to write, hand it to Sonnet (a subagent or session
with a complete brief), then review what comes back. If you are running on Sonnet and get stuck or
face a design decision the plan doesn't settle, stop and say so rather than guessing.

## The plan

The approved plan is `docs/PLAN.md` (milestones M0–M8). Work one milestone at a time; each ends with
a play check and a git commit. Don't change the design decisions in it without asking the user.

## Working in this project

- The Unity CLI is `unity` (`%LOCALAPPDATA%\Unity\bin\unity.exe`). Load the `unity-cli` skill before
  using it. Pass `--project-path <project root>` on Editor-driving commands.
- If a Unity Editor is open on the project, drive it live (`unity status`, `unity command …`).
  Never hand-edit `.unity`, `.prefab` or `.asset` files while an Editor is reachable, and never
  close the user's Editor yourself.
- Add packages with `unity command package add` (see the `unity-package-management` skill), never
  by editing `Packages/manifest.json`.
- Game content lives under `Assets/_Game/`. Every colour comes from `Assets/_Game/Scripts/Art/Palette.cs`.
- UI is uGUI + TextMeshPro or UI Toolkit, never `OnGUI`.
- After script changes run `unity recompile`; run `unity test` for the EditMode tests.
- Commit every `.cs`/asset together with its `.meta`. `Library/`, `Temp/`, `obj/`, `Build/` are never committed.
- When building gameplay, feel, audio, shell, or live features, load the `craft-game-floors` skill;
  if it isn't installed, ask the user whether to run `unity skill install <client>` (ask: never run
  it without a yes).
