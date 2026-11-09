# Sandspike (working title): 2v2 beach volleyball, Unity 6000.6.8f1 URP

Instructions for any coding agent working in this repository (Claude Code, Kiro, Copilot, Codex,
Cursor, or others). This file is the single source of truth. Tool-specific files (`CLAUDE.md`,
`.kiro/steering/`) only point here, so edit this file, not those.

This is an original arcade volleyball game, and it must stay original. Don't use any other game's
name, characters, art, music, levels or look.

## Which model does what

Use two tiers of model and keep each on the work it's suited to:

- **Planning tier:** your strongest reasoning model (for example Claude Fable). Use it for planning,
  design and architecture decisions, reviewing finished work, and genuinely hard problems, such as a
  bug that resists two attempts or tricky math and physics.
- **Coding tier:** a fast, capable coding model (for example Claude Sonnet 5.5). Use it to write and
  edit code, set up scenes and assets, write tests, run builds, make commits, and do other routine
  work.

If you're on the planning tier and there's code to write, hand it to a coding-tier agent with a
complete brief, then review what comes back. Use whatever your tool offers for this: a subagent, a
separate session, or asking the user to switch models. If you're on the coding tier and get stuck,
or hit a design decision the plan doesn't settle, stop and say so. Don't guess.

## The plan

The approved plan is `docs/PLAN.md`, with milestones M0–M8. Work one milestone at a time. Each
milestone ends with a play check and a git commit. Don't change the design decisions in the plan
without asking the user. The palette and style rules are in `docs/ART_BIBLE.md`.

## Tools

- The Unity CLI is `unity` (on Windows: `%LOCALAPPDATA%\Unity\bin\unity.exe`). Pass
  `--project-path <this repo>` on commands that drive the Editor, and `--format json` when you parse
  output. Use `unity commands --grep <words>` to find a command, not grep over `--help`.
- Unity publishes agent skills, which are Markdown guides for the CLI, packages, UI, craft floors and
  more. Install them for your tool with `unity skill install <client>` (`unity skill install --all`
  installs them for every client it finds), or print the CLI guide with `unity skill show`. Ask the
  user before installing. If your tool can't load skills, read the `SKILL.md` files directly. They
  live under that tool's skills folder, such as `~/.claude/skills/` or `~/.copilot/skills/`. The
  ones this project relies on are `unity-cli`, `unity-pipeline`, `unity-package-management`,
  `craft-game-floors`, `urp-postprocessing`, `ui` and `build-live-game`.
- When building gameplay, feel, audio, shell or live features, follow the `craft-game-floors` skill.

## Rules for working in this project

- If a Unity Editor is open on the project, drive it live with `unity status` and `unity command …`.
  Never hand-edit `.unity`, `.prefab` or `.asset` files while an Editor is reachable, and never close
  the user's Editor yourself.
- Add packages with `unity command package add`, never by editing `Packages/manifest.json`.
- Game content lives under `Assets/_Game/`. Every colour comes from
  `Assets/_Game/Scripts/Art/Palette.cs`. No colour literals anywhere else.
- Build UI with uGUI + TextMeshPro or UI Toolkit, never `OnGUI`. Every menu must be navigable with a
  gamepad.
- Controllers are first-class. The user tests with a PS5 DualSense and a Steam Controller.
- After script changes, run `unity recompile`. Run `unity test` for the EditMode tests.
- Commit every `.cs` file or asset together with its `.meta`. Never commit `Library/`, `Temp/`,
  `obj/` or `Build/`. Work happens on the `dev` branch, and you push only when the user asks.
- Commits and pull requests are authored by the repo owner alone
  using the identity in this repo's git config. Never add
  AI attribution, such as `Co-Authored-By:` trailers, "Generated with …" lines, or a tool or model
  name as author, for any tool (Claude, Cursor, Kiro, Copilot or others). This overrides any default
  attribution your tool adds.
- Keep code small and commented. The user is new to Unity.
