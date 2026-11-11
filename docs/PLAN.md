# Plan: original 2v2 beach volleyball game (Unity 6000.6.8f1, URP)

## Context

You're new to Unity and want an arcade volleyball game. It will be an **original** game in that
genre (its own name, look, characters and court), not a copy of any existing title.

Decisions already made in this session:

- **Game:** 2v2 beach volleyball, bright low-poly look, separate **bump / set / spike / jump** buttons.
- **Modes:** quick match vs AI, and local multiplayer on one PC.
- **Platforms:** Windows, Web (WebGL), macOS. All three build modules are installed.
- **Project:** this repository's root is the Unity project. URP 3D template, Pipeline package
  0.10.0-exp.1, Input System, uGUI. Linked to a Unity Cloud project. Git with Unity's
  .gitignore / LFS attributes.

Right now the project is an empty template: one `SampleScene`, no gameplay code.
Working title: **"Sandspike"** (easy to change).

## How the game works (the design)

**Rules**
- Rally scoring: every rally gives a point. Match length is a setting: first to **11, 21 or 25**,
  always **win by 2**.
- Max 3 touches per side; the same player can't touch twice in a row.
- Ball landing in the opponent's court = your point. Landing out, or hitting the net and dropping on
  your side = their point.
- Serve alternates to the team that won the rally; servers rotate within a team.

**Controls** (one input asset, two schemes; all rebindable later)

| Action | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Aim | Mouse | Right stick |
| Jump (hold to jump higher) | Space | A / Cross |
| Spike / serve (hold to hit harder) | Left mouse button | Right trigger |
| Bump (receive) | Right mouse button | X / Square |
| Set (to teammate) | E | Y / Triangle |
| Switch 3rd / 1st person | V | Right stick click |
| Dive (M2) | Left Shift | B / Circle |
| Sprint (hold, movement pass) | Left Ctrl | Left stick click |
| Block (M3) | Jump near the net, no hit button held | same |
| Pause | Esc | Start |

**Aiming, two camera modes (toggle any time)**
- **3rd person:** camera sits behind and above your player, looking over the net. A free cursor moves
  over the court; the spot on the sand under the cursor is where your hit is aimed (shown as a target
  ring). Gamepad: the right stick moves that target ring.
- **1st person:** you look through the player's eyes with mouse look. A crosshair in the centre of the
  screen sets the direction the ball goes. Gamepad: right stick looks.

**Charging, stamina and timing**
- **Jump:** tap Space for a normal hop; hold to charge and release for a higher jump (up to a cap).
- **Spike / serve / bump / set:** every hit button charges. Hold to charge power, release to hit.
  A light tap gives a soft hit, and a full charge gives a hard one. A charged bump or set travels
  farther and faster, and a light one drops short and soft. Charge can start on the ground or in the
  air.
- **Stamina:** a bar per player. Charged jumps and charged spikes spend it in proportion to the
  charge; it refills over time, faster while standing still. With low stamina the charge cap drops,
  so you can't max-jump and max-spike every rally.
- **Playtest 4:**
  - **Jump is back to the original design (overrides the instant/boost jump).** Hold jump on the
    ground to crouch and charge, then release to jump. Height scales with charge: a quick tap is a
    normal hop, a full charge is the high jump, and stamina pays for the charge. Charging is quick
    (about 0.4 s to full), so a tap still feels immediate.
  - **"Hit now" cue.** While you're holding a hit button, the ball glows and the contact point shows a
    ring whenever releasing would hit it. That shows exactly when to let go.
- **Playtest 3 (overrides playtest 2 for hits):** **the hit happens when you let go of the button,
  never while you're still holding it.** Holding winds up and builds charge, and the arm pulls back
  while the contact point stays fixed. Releasing swings:
  - If the ball is within reach at that moment, it's hit right away.
  - If you release a little early, the swing waits and fires when the ball arrives. That's the
    quick-tap case.
  - If you're still holding when the ball passes you, you don't touch it, and it drops.
- **Playtest 2 (jump part still applies):** charging is a **wind-up**. While you hold a
  hit button the arm pulls back, but the contact point never moves. **Jump fires the instant you press.** Holding it while rising carries you
  higher, up to the cap, and that extra height costs stamina. A plain press and a plain hit with no
  holding just work.
- **Update after playtest (overrides the timing details below):** a press always does the action
  when the ball comes within reach. A tap with no charge is a normal, softer touch, and an early press
  waits for the ball. Timing only nudges power and accuracy a little. The only miss is being out of
  position, or pressing after the ball has already gone past. There are no on-screen grade texts.
- **Timing is about contact with your body, never about the ball landing.** Each hit type has a
  contact point on the player that moves with them: **forearms at waist height for a bump, hands
  just above the head for a set, the hitting hand at full overhead reach for a spike or serve.** The
  ideal moment is when the ball passes closest to that contact point, and you get graded on how close
  your button release is to that moment.
  - **Slightly off timing still connects. A complete miss doesn't.** Timing has a generous window.
    Inside it, the hit always happens and timing only nudges how good it is. Outside it, you miss:

    | Release vs. ideal moment | Result | Power / speed | Scatter around aim point |
    |---|---|---|---|
    | within 0.08 s | Perfect | 100% | none |
    | within 0.20 s | Good | 90% | up to 0.5 m |
    | within 0.35 s | OK | 80% | up to 1.0 m |
    | further off | **Miss** | no contact | the ball keeps falling |

  - **A miss means the ball drops.** It isn't touched, it lands on the sand, and if that's on your
    side you lose the point (scoring arrives in M2).
  - **Early presses are held, not wasted.** A release inside the window but before the ball arrives
    waits and fires when the ball reaches your contact point.
  - Because the contact point rides on your body, **jumping is part of a spike.** A slightly mistimed
    jump puts your hand lower when the ball arrives, so the spike comes out flatter and weaker. Way
    off and you miss it.
  - **Out of reach is also a miss.** If the ball never comes within reach of the contact point,
    pressing does nothing and the ball drops.
  - Final hit quality = charge × timing multiplier. Charge (and stamina) decides most of a spike's
    power. Timing is the smaller factor on top. All of these numbers are tunable.
- HUD shows the stamina bar, a jump-charge and spike-charge meter, and "Perfect / Good / OK / Miss" feedback.

**Ball: computed arcs, not free physics.** Each hit picks a landing target and a flight time, and the
ball follows that exact arc (gravity plus a net check). This is the standard arcade approach and it
gives three things for free: a landing marker on the sand, an AI that knows where the ball will land,
and consistent feel on all platforms including Web.

**AI teammates and opponents.** AI players use the same controller as humans; an AI "brain" presses
the same virtual buttons. Each rally the AI closest to the predicted landing spot receives, the other
moves to set or approach for a spike. Three difficulty levels change reaction delay, timing accuracy
and aim spread.

AI players have the same stamina, charge and timing rules as humans.

**Controller support (from M1, not only in multiplayer).** The whole game, menus included, is
playable on a controller alone, and you can swap between keyboard + mouse and a controller mid-match.
- Actions bind to Unity's generic gamepad layout, so any standard controller works without
  per-device code.
- **PS5 DualSense:** supported directly by Unity's Input System over USB or Bluetooth. Button prompts
  show PlayStation symbols when a DualSense is the active device, Xbox-style letters for other pads,
  and keys for keyboard + mouse.
- **Steam Controller:** it reaches games through Steam Input, which presents it as a standard
  gamepad. To test it, the build is added to Steam as a non-Steam game (or Steam is running with
  desktop gamepad config); inside the Unity Editor without that, it behaves as a mouse/keyboard.
  I'll write the short setup steps when we get to that test.
- Right-stick aim gets its own sensitivity and dead-zone settings; analog triggers work for spike
  charge. Rumble on spikes and landings where the controller and connection support it.
- Web builds get controllers through the browser's gamepad support.

**Local multiplayer.** 1 to 4 humans, **split-screen**, because every player now has their own
camera and aim. A PC has one mouse, so one player uses keyboard + mouse and the others use gamepads.
On the match setup screen each device presses a button to join and picks a side; empty slots are
filled by AI. That covers co-op (2 humans vs 2 AI) and versus.

**Camera.** One Cinemachine rig per human player with two modes: a 3rd-person camera behind the
player (fixed facing toward the net, so the cursor can roam) and a 1st-person camera at head height
with mouse look. Small shake on spikes and landings. In 1st person a ball-direction indicator at the
screen edge shows where the ball is when it's out of view.

## Code layout

All game content goes under `Assets/_Game/` so it stays separate from template files.

```
Assets/_Game/
  Scripts/
    Core/      GameBootstrap.cs, MatchManager.cs (Serve → Rally → PointScored → MatchOver),
               RulesEngine.cs (touches, in/out, faults — plain C#, no Unity types), MatchSettings.cs
    Ball/      BallController.cs (arc simulation, net/ground events), BallTrajectory.cs (pure math:
               landing point, time, arc solve), LandingMarker.cs
    Players/   PlayerController.cs (move, charged jump, hit windows), Stamina.cs, ChargeMeter.cs,
               IPlayerBrain.cs, HumanBrain.cs (Input System), AIBrain.cs,
               HitResolver.cs (hit type + charge + timing + aim point → arc; plain C#)
    Aim/       IAimSource.cs, CursorAim.cs (3rd person: cursor ray → court point),
               CrosshairAim.cs (1st person: look direction → court point), AimMarker.cs
    Input/     VolleyballControls.inputactions (+ generated C# class), PlayerJoinManager.cs
    Camera/    PlayerCameraRig.cs (3rd/1st person switch, split-screen viewports), BallIndicator.cs
    Feel/      Feel utility + hit-stop, squash/stretch, sand puffs (from the game-feel / vfx references)
    Audio/     AudioDirector.cs, synthesized placeholder clips, mixer with MusicVol / SFXVol
    UI/        HUD (score, serve indicator, touch count), Title, MatchSetup, Pause, Results
    Art/       Palette.cs (single source of every colour)
  Scenes/      Boot.unity, Match.unity
  Prefabs/     Player, Ball, Court, Net
  Tests/EditMode/  BallTrajectoryTests.cs, RulesEngineTests.cs, HitResolverTests.cs, StaminaTests.cs
```

Reused from the template: `Assets/InputSystem_Actions.inputactions` **UI map** (menus), the URP assets
in `Assets/Settings/`, and the existing `DefaultVolumeProfile`. Removed: `Assets/TutorialInfo/`,
`Assets/Readme.asset` (template readme only).

## Milestones (each ends with a play check and a git commit)

**M0 — Finish project setup** (the rest of the `new-unity-project` flow)
1. Commit the current Hub-generated changes as a restore point.
2. Packages via `unity command package add` (never hand-editing `manifest.json`):
   `com.unity.cinemachine`, `com.unity.test-framework`, `com.unity.services.authentication`.
3. Player identity: one small bootstrap script (services init + anonymous sign-in, never blocking),
   following the `build-live-game` skill's auth section.
4. Art bible first (per `craft-game-floors` → art-direction): style line, a 6-colour sunny beach
   palette + accent, mood line; written into `Palette.cs`.
5. Visual baseline (per `urp-postprocessing`, scene-lighting): global Volume with ACES tonemapping,
   bloom, light vignette; camera post-processing on; highest quality tier; palette sky and sun.
   `ui` skill picks the UI stack (expected: uGUI + TextMeshPro).
6. Verify: Editor console clean, lit screenshot captured.

**M1 — Court, ball, one player, both cameras.** Low-poly court, net, sand, simple backdrop (palms,
sea, horizon) from primitives; `BallTrajectory` + `BallController`; one human `PlayerController`
with move, charged jump, bump/set/charged spike, stamina; 3rd-person cursor aim and 1st-person
crosshair aim with the V toggle; landing and aim markers; full gamepad bindings with right-stick
aim and device-aware button prompts. EditMode tests for trajectory, hit
resolution (charge × timing) and stamina.

**Feel additions (after looking at genre reference footage; general ideas only, our own art):**
- **Readable ball:** a slightly oversized ball with a bright outline. Hard hits leave a speed trail
  whose length scales with power. → M5 (outline in M2, so rallies are readable from the start)
- **Higher, wider 3rd-person camera:** raise and pull back the default 3rd-person view so both
  sides of the court and the ball's arc are readable at once. → small tuning right after the
  current hit-feel fix, before M2
- **Dive:** a new action (keyboard: Shift, gamepad: B / Circle, both currently free)
  that throws the player a few metres toward the move direction. It extends bump reach low and far
  and costs some stamina, with a short get-up recovery. AI uses it too. → M2 for the player, M3 for AI
- **Clean HUD:** score at the top and a small marker over your player. Stamina and charge move to a
  compact ring at the player's feet, instead of screen-corner bars. → M2 (HUD rebuild)

**M2 status: done.** Rules, serve, dive, simple bots, outlined ball and the new HUD are in.
109 tests pass, and an all-bot match ran cleanly to 11–6.

**Touch identity (next pass after M2):** each touch should feel like its real-volleyball
counterpart. Arcs are defined by **apex height and speed**, not one shared flight-time formula:

| | Bump (pass/dig) | Set | Spike |
|---|---|---|---|
| Body | Crouched, arms locked straight together, platform at the waist | Hands up at the forehead, fingertips | Jump, arm cocked back, full overhead swing |
| Ball path | Rises high and soft (apex about 3.5–6 m), floats to the target | Very high, gentle and **accurate** (apex about 5–6.5 m), nearly hangs at the top near the net | Fast, flat-to-downward line (about 16–28 m/s), short and sharp |
| Charge changes | Distance and height (soft dig to a long pass) | Distance along the net only (accuracy stays high, scatter about a third of a bump's) | Speed and how steeply it goes down |
| Contact feel | Dull thud, ball pops up off the arms | Quick soft release, almost no spin | Hit-stop (about 60 ms), camera shake, loud crack, speed trail, heavy spin |
| Typical use | Receiving serves and spikes | Second touch, to put the ball up for the spiker | Third touch, over the net |

**Settled by playtest (these override the table and the list below, and this pass must not change them):**
- **Set:** lands at the aim point; apex 4.0–7.2 m by charge (about 1.5 s to 2.2 s of flight); the bot
  teammate sets full height and tracks the human.
- **Spike zone:** a tall cylinder (hand −0.6 m to +0.8 m, 1 m radius). It stays this size: the
  "tight spike zone" idea below is dropped.
- **Queue:** an early release waits for the ball with no time limit. The 0.6 s cap below is dropped.
  The "queued" pose is still wanted.

The visual and audio parts (sounds, trail) land in M5. Arcs, poses, hit-stop and shake land in this pass.

Also in this pass (from community devlog research, general mechanics only):
- **Explicit action queue.** A released hit waits until the ball comes into reach, for up to
  0.6 s (tunable). While it waits, the player shows a "queued" pose and the hit-now cue tints to
  match. After 0.6 s it expires silently. This formalises the early-release behaviour and is the main
  candidate fix for the open hit-feel issue.
- **Honest contact zones.** Bumps stay forgiving (reach about 1.0 m), sets are medium (about 0.8 m), and
  spikes are tight (about 0.6 m), so attacking takes positioning and isn't just a long reach.
- **Spike momentum.** Spike speed adds the attacker's horizontal speed at takeoff, so a run-up beats
  a standing spike.
- **Combo multiplier.** Spike speed ×1.0 on a first touch, ×1.1 on a second and ×1.2 on a third
  touch. That rewards bump → set → spike over spiking straight back.
- **Backset.** Aiming behind the setter sets the ball backwards, with its own pose.

**Movement pass (after touch identity, before M3):**
- **Sprint:** hold (keyboard Left Ctrl, gamepad left-stick click). About 25% faster, drains stamina
  quickly, and is meant for run-ups and slide attacks.
- **Less drift on standing jumps.** A jump from near standstill keeps little sideways momentum. Run-ups
  (and sprint) carry it.
- **Dive is always allowed, with burnout.** The "refuse dive below 15 stamina" rule goes. Diving with
  too little stamina still happens, but the player goes into **burnout** for about 2 s: slower,
  can't charge, with a visible effect (tinted player and a flickering ring).
- **Jump cancel.** While charging a jump, pressing dive or a hit button cancels the charge cleanly
  instead of jumping.
- **Serve preview.** While charging a serve, a dotted line on the court shows the predicted serve path
  and landing spot.
- **Serve fault test.** A test proves a legal serve never counts as a fault, including the net-clear
  assist.

**Open issue (tabled by the user):** hitting still feels off after playtests 1–4. Revisit it after M3,
once real rallies against AI make the problem easier to pin down. The explicit action queue and
honest contact zones above are the first things to try.

**M2 — Full rally and rules.** *Scope note:* rules can't be played without the other three players,
so M2 ships **simple bots** for your teammate and both opponents. They run to the predicted landing
spot and bump, set or send the ball back over the net through the same `IPlayerBrain` interface.
M3 turns them into the real `AIBrain`, with stamina, charge, difficulty levels and dives. `RulesEngine`, `MatchManager`, charged serve, scoring to 11 / 21 / 25
with win by 2, HUD (score, serve indicator, touch count, stamina bar, charge meters, timing
feedback). EditMode tests for rules (touch limits, in/out, win by 2 at each match length).

**M3 — AI.** `AIBrain` for teammate and both opponents, using the same stamina/charge/timing rules;
three difficulties. Result: a complete, winnable 2v2 match vs AI. This is the first fully playable
build.
- **Blocking (new, missing from the plan so far).** Jumping within about 1 m of the net with no
  hit button held raises the hands into a block. A ball that hits the block rebounds off it. As in
  beach volleyball, a block counts as one of the team's three touches. AI blocks on harder
  difficulties.
  - **The block zone is a shield, not a ball.** It's a flat, hand-wide zone in front of the raised
    hands, facing the way the blocker faces. Facing and where the ball meets the hands decide where
    the rebound goes: square to the net sends it straight back, angled hands deflect it sideways.
  - **Strafe while blocking.** In the air, left/right input shifts the hands sideways a little.
    On the ground near the net, the player sidesteps without turning, so facing can be set precisely.
- **Crosshair stays one size.** The 1st-person crosshair is a fixed screen size and never scales with
  aim distance. The ground aim ring may scale, the crosshair may not. A quick check covers it.
- **AI uses everything players have:** sprint, dives (with burnout), the combo multiplier and backsets.
  Difficulty changes reaction time, positioning and hit quality.

**M4 — Local multiplayer.** `PlayerJoinManager` (Input System `PlayerInputManager`) with split-screen,
join-and-pick-side setup screen, AI fills empty slots; keyboard + mouse for one player, gamepads for
the rest, right-stick aiming in both camera modes.
- **Aim assist (gamepad option, on by default for gamepads).** The aim point eases toward sensible
  targets: deep corners, the seam between opponents, and a setter spot for sets. That's less precise
  but easier for casual players, who can focus on positioning. Mouse aim is never assisted.

**Characters status: done early.** The primitive players are replaced by CC0 rigged low-poly
characters in team colours (`CharacterRig.cs`), with clip animation for idle, run and jump and
code-driven arm poses for bump, set, spike and dive. The primitive body stays in the prefab behind
the `useCharacterModel` toggle. Still open for M5: a toon or smoother look to match the concept art,
and hit and dive animations beyond arm poses.

**M5 — Characters, feel, effects, audio** (craft floors: character-motion, game-feel, vfx,
game-audio). Primitive-built players with team colours and idle/run/jump motion, hit-stop and camera
shake on spikes, sand puffs, ball trail, synthesized placeholder sounds and a music loop, mixer with
volume parameters.
- **Context dive poses:** forward, sideways and backward dives each get their own pose.
- **Outline check:** the ball's outline must stay clean at every camera distance. Optional rim
  shading on characters is decided by capture in M8, not by default.

**M6 — Shell.** Title screen, match setup (mode, difficulty, match length), pause, results screen,
volume settings, scene transitions (craft floor: game-shell).
- **Camera settings:** 3rd-person height, distance and FOV; shoulder position (left, centre or
  right); and camera bounds that keep the view inside the play area.
- **Rebindable controls** for every action, on keyboard and gamepad, with a reset to defaults.
- **Match rules option: point cap.** Win by 2 stays the default, with an optional cap (for example
  11 → 15, 21 → 25, 25 → 30) so a deuce can't go on forever.
- **How to Play** screen covering each touch, charging, the queue, dive and burnout, block, and
  the combo multiplier.

**M7 — Saves and builds.** Cloud Save for settings and win/loss stats with a local fallback, one
Remote Config kill switch. No leaderboard for now (a vs-AI match has no score worth ranking). Then
Windows, Web and macOS builds with `unity build`. Web-specific check via the `optimize-web` skill.

**M8 — Done gate.** `craft-game-floors` visual scorecard: score six axes from captures, fix anything
below 3, confirm zero console errors. Includes a **court clarity** check: lines, ball and players
read clearly against the sand and props from both camera views.

M0 through M3 is the core; I'd build those first and let you play before continuing.

## Things that need your yes (covered by approving this plan)

- Adding this line to a new `CLAUDE.md` in the project, so future sessions load the craft guidance:
  "When building gameplay, feel, audio, shell, or live features, load the `craft-game-floors` skill;
  if it isn't installed, ask the user whether to run `unity skill install <client>` (ask: never run
  it without a yes)."
- Deleting the template's `Assets/TutorialInfo/` and `Assets/Readme.asset`.

Not included: online multiplayer, a GitHub remote (you'd need one to pull the project onto your Mac;
easy to add later), store publishing, purchased art or music. **Later, not planned yet:**
achievements, character customization and cosmetics, player titles, and anything online-only (chat
moderation, server browser).

## How each step gets verified

- `unity recompile --project-path <project root>` after every script change (compile errors).
- `unity test` for the EditMode tests (trajectory math, rules).
- Play mode through the live Editor, following `unity-cli`'s `references/playmode-verification-loop.md`:
  enter Play mode, confirm frames advance, drive a rally with `unity command script eval`, read the
  Editor console, capture `unity command capture screenshot`.
- Art checks from art-direction: no `Default-Material` on any renderer, no `Color.` literals outside
  `Palette.cs`.
- Each milestone: `git status` shows no `Library/` or `Temp/`, every new file has its `.meta`, then commit.
- You play each milestone build in the Editor (press Play) and tell me how it feels; hit timing windows, jump
  height and charge time, spike power, stamina costs and refill rate, ball speed, mouse and stick
  sensitivity and AI difficulty are tuning values exposed in the Inspector.
- Controller checks you do with your hardware: DualSense (USB and Bluetooth) in the Editor from M1;
  Steam Controller against a Windows build launched through Steam from M3.
