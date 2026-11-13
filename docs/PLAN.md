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

**Feel additions (general ideas only, our own art):**
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

**Touch identity status: done.** Bump and spike arcs, momentum, combo and contact feel are in, and
each touch has its own pose and swing on the character (bump crouch and lift, set push, spike whip and
follow-through, and the pose holds while a released hit waits for the ball). Not done: a separate
backset pose (the player turns to face the aim, so a backset looks like a normal set), and sounds and
the ball trail (M5).

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
- **Jump cancel.** While charging a jump, pressing Dive cancels the charge and dives instead of
  jumping. Hit buttons never cancel it, because holding jump and spike together is how a jump-spike
  is charged.
- **Serve preview.** While holding Spike to toss, a dotted line on the court shows the toss arc and a
  ring shows where the toss will come down (it used to preview the serve; the serve is now hit by the
  server after the toss).
- **Serve fault test.** A test proves a legal serve never counts as a fault, including the net-clear
  assist.
- Movement pass status: done

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
    After playtest: the zone is 1.2 m wide and reaches from the top of the net up to the fingertips,
    so a high jump still stops a ball that crosses just over the tape. The blocker also steers the
    rebound: it lands at the aim point (cursor, crosshair or right stick) when that is on the
    attacker's side, kept 1-4 m past the net and inside the side lines. Facing and the strike point
    only decide the rebound when the aim is on the blocker's own side.
  - **Strafe while blocking.** In the air, left/right input shifts the hands sideways a little.
    On the ground near the net, the player sidesteps without turning, so facing can be set precisely.
- **Crosshair stays one size.** The 1st-person crosshair is a fixed screen size and never scales with
  aim distance. The ground aim ring may scale, the crosshair may not. A quick check covers it.
- **AI uses everything players have:** sprint, dives (with burnout), the combo multiplier and backsets.
  Difficulty changes reaction time, positioning and hit quality.

**M3 status: done.** The block has its own arm pose (both arms up in a V, leaning with the strafe; `CharacterRig`). `SimpleBotBrain` is now the real AI (`AIDifficulty`, `AIProfile.Table`,
`MatchManager.difficulty`), with reaction delay, sprint, dives, aimed attacks, tips and bot blocking. Blocking works for humans and bots
(`BlockShield`, `PlayerController.IsBlocking / BlockCenter / BlockNormal`, `HitType.Block`, a block is a touch but not a double-touch).
The 1st-person crosshair was already a fixed screen size (nothing scales it), so no change was needed. 173 EditMode tests pass.
Final profile table:

| | Easy | Normal | Hard |
|---|---|---|---|
| Reaction delay (s, +-8% per ball) | 0.45 | 0.26 | 0.145 |
| Spike read chance | 0.25 | 0.3 | 0.5 |
| Move speed x | 0.85 | 1.0 | 1.0 |
| Mistake chance (1.5 m off) | 0.25 | 0.12 | 0.05 |
| Attack aim spread (m) | 1.2 | 0.7 | 0.3 |
| Spike charge | 0.2-0.6 | 0.4-0.8 | 0.6-1.0 |
| Sprint / dive | no / no | yes / if stamina >= 15 | yes / always |
| Blocks | never | 40% | always |

**Dig model (after M3).** A spike is in the air for only 0.3-0.45 s, so reaction delay alone made digging close to all-or-nothing.
Each bot now rolls a *spike read chance* per ball. On a read, the back defender walks to where the hitter is aiming while the set is
still in the air (`CoverAttack`), and reacts to the spike in 0.05 s instead of its normal delay (`BotPlanner.ReactionFor`). Tune the
read chance to change how many spikes a level digs; the reaction delay covers everything else.

Measure with the dig rate (spikes dug / spikes landing in court), not the rally median: rallies end in steps of three hits
(4, 7, 10...), so the median jumps a whole step when the dig rate moves a few percent. All-bot matches, about 70 points each:

| | Easy | Normal | Hard |
|---|---|---|---|
| Spikes dug | 20% | 54% | 70% |
| Median hits per rally | 4 | 7 | 13 |
| Target median | 3-6 | 5-9 | 7-14 |

Still open: bot blocks are rare and seldom end a rally. Mistakes seldom go out (the targets sit 1 m inside the lines), so rallies
mostly end on an undug spike. These numbers are bot against bot at the same level, not against a human.

**Feel pass (target feel set by the user, 2026-11-12; next pass, before M4).**
This is the feel the user wants for serves, jumps, spikes, blocks, hit timing, motion and sound.
Only mechanics, timing and feedback ideas are described here: our art, characters, names and sounds
stay our own. The block and sound rows are the least certain and should be tuned by playtest.

*Target feel, with target timings*

| Topic | What happens | Timing |
|---|---|---|
| Serve | "Your serve, hold to toss" with a 15 s serve clock. The server walks freely inside a lit serve zone behind the baseline. Holding the hit button grows a dotted vertical line (the toss height), with a small gauge at the feet. Releasing throws the ball up and forward, and a ring on the sand shows where the toss will come down. The server runs under it, jumps and hits. A toss that drops is a fault. | Toss hold about 0.6–0.8 s. Toss in the air about 2.2 s. Bot servers hit about 0.9–1.1 s after the toss. |
| Jump | Hold to charge, release to jump, like ours. A round gauge fills at the feet while the player keeps shuffling. Takeoff is explosive, then the player hangs. A white vertical streak joins the player to the ground, so height is readable. | Gauge fills in about 0.6 s. Most of the height is reached in the first 0.1 s. Airtime is at least 0.85 s on a full jump. |
| Spike | In the air, holding the hit button charges at the ball: rings close around the ball, with an aim reticle on it and a combo badge (×2, ×3) beside it. On release there is a white burst and speed lines, the reticle rides the ball down, a marker shows the landing spot, and "in" or "out" appears there. The camera tilts up with the jump and keeps player and ball in frame. | Takeoff to contact about 0.4 s. Contact to sand about 0.25 s. |
| Block | The blocker charges a jump at the net and hangs with the whole chest above the tape. Two kinds of block: hands simply in the way give a soft touch, and the ball drops slowly back on the attacker's side where the attackers can still save it; pressing the hit button as the ball arrives swats it straight down for a point (a stuff). An attacker's charge is visible to everyone: the rings and the combo badge appear on the ball as they wind up, which tells the blocker when to go. Jumping too early is the usual mistake, because the set hangs longer than expected. The hit button's hint reads "block" whenever a jump at the net would block. | Jump charge about 0.3–0.6 s. Blocker airtime about 1.0 s. A stuff lands within about 0.3 s. |
| Hit timing | The ball always has a landing ring on the sand and a trail in the colour of the team that last touched it. The player's foot ring changes colour shortly before the right moment, and the ball gets an aura when a hit would connect. Sets hang long at the top. | Windows look generous. Nothing is frame-tight. |
| Motion | The ball squashes and stretches with speed. Characters lean and crouch before a jump, flash on a hit, and kick up dust when they run, land and dive. The camera eases between the serve view and the play view. | Camera move after a serve takes about 1.3 s. |
| After a point | Whistle, score pop, a banner naming the scorer, short callouts over players' heads (praise on the winning side, consolation on the other), then sometimes an instant replay in slow motion from a low side camera, ending in a wipe. | Whistle about 0.4 s after the ball lands. |
| HUD | A list of actions with their keys sits in the bottom-right corner. Actions that can't be used right now are greyed out, and the main hit button's label changes with context (toss, bump, touch). | |

*What to build (does not change settled behaviour)*
- **Jump streak and landing dust.** A vertical streak from the sand to every airborne player, and dust
  puffs on takeoff, landing, dives and sprint. This helps both spiking and blocking.
- **Landing ring for every ball,** not only serves, in the last toucher's team colour, with a matching
  ball trail. "In" and "out" text at the landing spot.
- **Spike charge shown at the ball.** While a spike is held in the air, the charge ring closes around
  the ball itself and the combo badge sits beside it, so the eyes never leave the ball. The existing
  hit-now cue becomes the ball's aura. The foot ring changes colour just before the hit-now moment.
- **Hit feedback:** a white burst and speed lines on spikes, a flash on the hitter, and ball squash
  and stretch scaled by speed.
- **Smoother character motion:** a lean and crouch while a jump charges, arm anticipation before each
  swing and an eased follow-through after it, and an eased return to idle. No snapping between poses.
- **Camera:** tilt up to follow the player's jump and keep the ball in frame, and ease between the
  serve view and the rally view.
- **Jump cue for blockers:** the attacker's streak and a short rising sound mark the moment to jump.
- **Context control hints:** grey out actions that can't be used now, and show "Block" in place of
  "Spike" when a jump at the net would block.
- **Landing ring shows urgency.** The ball's landing ring shifts from white through yellow and orange
  to red as the ball nears the sand, so the ring alone tells you how long you have.
- **Touch counter.** Three small dots under the score fill as the team with the ball uses its touches.
- **Everyone's aim on the sand.** A small ring in the team colour marks where each bot intends to send
  the ball, so you can read a teammate's pass or set before it is hit.
- **Hit flash in the team colour,** and the ball keeps bouncing and rolling on the sand after a point.
- **Point banner with credit.** It names the scorer, or the player whose error gave the point away,
  and calls out break point, set point and match point.
- **Point sequence:** whistle, score pop, then the next serve. Instant replay is optional and comes
  last (M6).

*Status of the list above:* done: jump streak and dust, landing ring for every ball (with urgency colour, team rim, trail,
IN / OUT label), spike charge at the ball (ring, combo badge, aura, foot ring brightening), hit burst and ball squash and
stretch, camera tilt in the jump, context control hints, touch counter, score pop and point banner (reason, last touch, match
point), bots' aim on the sand. Not built in this step: character lean and crouch, arm motion smoothing, the hit flash on the
character, the jump-cue sound for blockers, the ball rolling after a point, instant replay, and callouts over heads. The
serve-to-rally camera ease is skipped because the third-person camera has the same view in both. A match is one game, so the
banner says "match point" and never "set point"; there is no break point yet.

*Needs the user's yes (each changes something settled or playtest-approved)*
1. **Toss serve. Approved and done.** Hold Spike to fill the toss meter (0.8 s to full, no stamina cost),
   release to toss, then run under it, jump and hit with Spike. Toss peak 3.6 m (tap) to 6.0 m (full), landing
   0.8 to 2.5 m ahead, straight toward the net. The server walks freely behind the baseline (at least 0.1 m
   back) before the toss; the serve clock is 15 s. A dropped toss, or the clock reaching 0, is a serve fault:
   the receivers score and the serve rotates (HUD text "fault"). Only the server can touch the toss and it is
   never blocked. A jump serve is flatter by spike charge, capped at 22 m/s. The preview shows the toss.
   Bots (all-bot matches, about 40 points each): Easy toss charge 0.2 standing; Normal 0.6, jump serve half
   the time; Hard 0.8, always jumping. Measured: serve faults 0% on every level (0 of about 43, 28 and 46
   serves), aces 2.3% Normal (1 of 43) and 0% on Easy and Hard.
2. **Floatier jump. Approved and done.** Still charge-then-release, same apex heights (0.9 m tap, 1.6 m full).
   Gravity is 30 m/s^2 while the vertical speed is at least 2.2 m/s and 0.35 times that (10.5) below it, up
   and down. Airtime is 0.72 s for a tap (was 0.54 s) and 0.90 s for a full jump (was 0.72 s). Note: the
   launch is not quicker to 80% of the apex (tap 0.19 s against 0.15 s before) because the higher gravity
   slows the early rise; the gain is the hang, with about 0.34 s spent above 80% of a tap apex against 0.24 s.
3. **One context hit button.** The alternative is one button that bumps on the ground, spikes in the
   air and tosses on serve, plus a separate set button. Ours has separate bump and spike buttons.
   Recommendation: keep ours, because it already works and suits a gamepad.
4. **Soft and active blocks.** Hands that are only in the way (no button) give a soft block: the ball
   drops slowly back on the attacker's side, 1–2 m from the net, and the attackers can save it. Pressing
   Spike while blocking, within about 0.15 s of the ball reaching the hands, is a stuff: fast and
   straight down toward the aim point. Today every block is the fast aimed kind, with no button.
5. **Show every player's spike charge on the ball,** opponents' included, so blockers can time their
   jump. Today only your own charge is shown, at your feet.

*Sound list (M5 audio, moved up to this pass if the user wants it sooner)*

| Sound | When | Character |
|---|---|---|
| Countdown hits | Three, one second apart, before the first serve | The loudest sounds in the match, low and punchy, about 0.5 s decay |
| Start whistle | About 1.2 s after the last countdown hit | Narrow tone near 2.6 kHz, about 0.4 s |
| Serve clock tick | Each second of the serve clock | Very short, quiet blip |
| Toss | Toss release | Short soft blip |
| Bump | Contact | Dull, short thud, mostly low frequencies |
| Set | Contact | Softer and shorter than a bump |
| Spike | Contact | Full-band crack, the sharpest hit sound, louder with charge |
| Spike charge | While charging in the air | Rising whoosh over the last 0.25 s before contact |
| Jump and landing | Takeoff, touchdown | Light whoosh up, sand thud down |
| Block | Ball meets hands | Hard slap, between a bump and a spike |
| Ball on sand | Landing | Soft thump |
| Point whistle | About 0.4 s after the ball lands | Same whistle, about 0.5 s; a different pattern for a fault |
| Crowd | After a point, bigger for spikes and blocks | A cheer that swells for about a second |
| Callouts | Good plays | Short voice-like chirps |
| Music | Whole match | A steady, upbeat loop under everything, quieter than the hits |

All of these are synthesized placeholders first (`AudioDirector`), through the mixer's SFX and Music
groups, with pitch and volume varied a little on every play.

*Sound status:* placeholder sounds and the music loop are in (`AudioDirector`, `SoundSynth`; nobody has listened to
them yet). Skipped: callouts (voice-like chirps, not asked for in this pass). Not true to the list: the countdown
and start whistle play at the start of a match but do not hold up the first serve (no gameplay change), and the net
rustle is wired but never fired in the test matches. Volumes are two fields (`sfxVolume`, `musicVolume`) until the M6
mixer replaces them.

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
- **Match setup, in one screen:** stage, team size, points per set, sets (single set or best of 3),
  a deuce toggle, one difficulty for all bots plus a per-bot override, and a "join" slot for a second
  player. Sets are new: a match is one set today.
- **Pause menu:** resume, settings (audio, video, display), controllers (drop in or out), how to play,
  back to menu, exit game.
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
