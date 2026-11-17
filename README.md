# Sandspike (working title)

An arcade 2v2 beach volleyball game made in Unity, in a bright low-poly style. It's an early
prototype: you play full matches with a bot teammate against two bot opponents, with serving, rally
scoring, three difficulty levels and blocking at the net.

![A jump serve, just after the hit](docs/captures/toss-serve.png)

| | |
|---|---|
| ![A spike at the net, seen from the side](docs/captures/chars-closeup.png) | ![Blocking at the net](docs/captures/m3-block-pose.png) |
| A bot goes up for a spike | Jump at the net and hold Bump to block |

## The look

The style is soft, flat-coloured low-poly with a small fixed palette (see `docs/ART_BIBLE.md`). This
concept painting set the direction, and the game is still catching up to it.

![Concept art: soft toon beach court](docs/concepts/concept-a-soft-toon.png)

## How it plays

- **Wind-up hits:** hold bump, set or spike to wind up and build power, then let go to swing. A quick
  tap gives a softer touch. Hold until the ball passes you and you miss it.
- **Hit-now cue:** while you're holding a hit button, the ball glows and a ring appears whenever
  letting go would connect.
- **Charged jump:** hold jump on the sand to crouch, then release. A tap is a hop, and a full charge is
  a high jump for spiking.
- **Stamina:** charged jumps, hits, sprinting and dives use it up, and it refills over time.
- **Sprint and dive:** sprint to cover the court, and dive for a ball you can't reach. A dive with too
  little stamina still happens, but leaves you slow and weak for two seconds.
- **Blocking:** jump close to the net and hold Bump while the other team has the ball, and your arms go up as a block (never against your own team's ball).
  Move left or right in the air to slide it.
- **Toss serve:** hold Spike to toss the ball (a dotted arc and ring show where the toss will come down), then run under it, jump and hit it with Spike. Let the toss drop, or take longer than 15 seconds to toss, and it's a fault. A jump serve is flatter and faster the harder you charge the hit.
- **Floaty jump:** the jump launches fast and hangs near the top, so there's time to hit in the air.
- **Bots:** Easy, Normal and Hard. Harder bots react faster, read your spikes, dive, block and aim
  for open sand.
- **Scoring:** rally scoring to 11, 21 or 25, win by two. An optional point cap (15, 25 or 30) ends a deuce. A match is one set or best of 3 (the loser of a set serves first in the next).
- **Menus:** a title screen (Play, Practice, How to Play, Settings, Quit), a setup screen (choose players, then difficulty, points, point cap and sets, plus a difficulty override for each bot), pause (Esc, or Start / Options on a gamepad), a results screen, settings (music and effects volume, aim assist, fullscreen, the landing ring, ball trail and IN / OUT label, camera, controls; saved between runs) and a How to Play screen. Every menu works with keyboard, gamepad or mouse.
- **Practice:** a solo court with no score. Pause to pick a drill: Receive, Set, Spike, Block or Serve.
- **Camera settings:** 3rd-person height, distance, field of view and shoulder (left, centre or right), in Settings.
- **Rebinding:** Settings, Controls: pick a line, press the new key or button (Esc or Start cancels). A key already in use swaps with it. Keyboard and gamepad are saved separately, with a reset for each.
- **Two camera views:** in 3rd person you aim with the mouse cursor, and in 1st person with a crosshair.
- **Controllers:** supported throughout. Button hints switch to PlayStation names on a DualShock or DualSense.

## Controls

| Action | Keyboard + mouse | Controller |
|---|---|---|
| Move | WASD | Left stick |
| Aim | Mouse | Right stick |
| Jump (hold to charge) | Space | A / Cross |
| Spike (hold to wind up; on serve: hold to toss, then tap to hit) | Left click | Right trigger |
| Bump (hold to wind up) | Right click | X / Square |
| Set (hold to wind up) | E | Y / Triangle |
| Sprint | Left Ctrl | Left stick click |
| Dive | Left Shift | B / Circle |
| Block | Jump at the net, hold Bump (right click) | Jump at the net, hold Bump (X / Square) |
| Switch 1st / 3rd person | V | Right stick click |
| Show / hide control hints | H | Select / Create |

This is the default layout; the Controls screen in Settings changes any of these (not Aim, the Move stick, or Pause).

## Local multiplayer

Up to four people can play, with bots filling the other slots. Press **Play** and a join screen shows two team
panels with two slots each. Keyboard + mouse start in Team A's first slot.

- **Join:** a gamepad presses A / Cross; the keyboard (if it left) presses Enter or Space.
- **Move between free slots:** left stick, D-pad, or A / D. **Leave:** B / Circle or Backspace.
- **Start:** any joined player presses Start / Options, or Enter on the keyboard. A match needs at least one human.
- After a match, any human pressing Jump plays again with the same line-up. Hold Start / Esc for one second to return to the join screen.

Keyboard + mouse count as one player; each gamepad is one player, and one person's input never moves another person's player.
With exactly one human you can still switch between keyboard and gamepad mid-match.

**Split-screen:** 1 human is full screen, 2 are side by side, 3 or 4 get a quarter each (with 3, the spare quarter shows a wide view
of the court). Each person has their own 3rd / 1st person toggle, aim marker, foot ring, hit cue, charge ring, serve preview and control
hints, shown only in their own view. The score, touch dots and point banner are drawn once across the whole screen.

**Aim assist:** a gamepad player's aim is pulled 60% of the way to a sensible target when it is within 1.5 m of one: the deep corners
or the middle of the two opponents for a spike or serve, and the spot near the net at your teammate's x for a set or a first-touch bump.
It never applies to the mouse, and not while blocking. Turn it off per player with `aimAssist` on that player's `HumanBrain`.

### Testing a Steam Controller

The Steam Controller reaches the game through Steam Input, which presents it as a standard gamepad.
Add the built game to Steam as a non-Steam game (or keep Steam running with a desktop gamepad layout) and it plays like any other pad.
Inside the Unity Editor without that, it acts as a mouse and keyboard.

## Running it

1. Install [Git LFS](https://git-lfs.com) before cloning, because images and fonts are stored with LFS.
2. Open the project folder in **Unity 6000.6.8f1**. It uses URP and the Input System, and both come
   with the project.
3. Open `Assets/_Game/Scenes/Match.unity` and press **Play**. The title screen comes first: Play opens the setup screen, where you choose players and match options.

The EditMode tests run from **Window → General → Test Runner**.

## Build

In the Editor choose **Sandspike → Build Windows**. It builds the scene ticked in Build Settings to
`Build/Windows/Sandspike.exe` (the `Build/` folder is not committed) and logs the result and size in the Console.
It takes about 3 minutes.

## Project layout

- `Assets/_Game/`: all game code, scenes, prefabs and materials
  - `Scripts/`: ball, players, aiming, camera, input, UI and art code
  - `Tests/EditMode/`: tests for ball arcs, hitting, stamina, rules, movement, AI and blocking
- `docs/PLAN.md`: the design and milestone plan
- `docs/ART_BIBLE.md`: the style and colour palette
- `AGENTS.md`: instructions for AI coding agents working in this repo

## Status

Milestones 0 to 4 of the plan are done: court and cameras, hitting, rules and scoring, characters,
movement, AI difficulties, blocking and local multiplayer (up to 4 humans, split-screen). The game also has sound effects (CC0 recordings plus a few synthesized in code) and a synthesized
music loop. Milestone 6 (the shell) is built apart from the AudioMixer and time-of-day variants: title, setup, pause, settings, camera, controls, how to play, results, practice, sets and per-bot difficulty are in.
Controllers (including the split-screen line-ups) and the menus have only been tested with virtual gamepads and queued keyboard events so far, not with a real DualSense, a Steam Controller or a real mouse.
