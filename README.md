# Sandspike (working title)

An arcade 2v2 beach volleyball game made in Unity, in a bright low-poly style. It's an early
prototype: you play full matches with a bot teammate against two bot opponents, with serving, rally
scoring, three difficulty levels and blocking at the net.

![A jump serve, just after the hit](docs/captures/toss-serve.png)

| | |
|---|---|
| ![A spike at the net, seen from the side](docs/captures/chars-closeup.png) | ![Blocking at the net](docs/captures/m3-block-pose.png) |
| A bot goes up for a spike | Jump at the net with no hit held to block |

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
- **Blocking:** jump close to the net without holding a hit button and your arms go up as a block.
  Move left or right in the air to slide it.
- **Toss serve:** hold Spike to toss the ball (a dotted arc and ring show where the toss will come down), then run under it, jump and hit it with Spike. Let the toss drop, or take longer than 15 seconds to toss, and it's a fault. A jump serve is flatter and faster the harder you charge the hit.
- **Floaty jump:** the jump launches fast and hangs near the top, so there's time to hit in the air.
- **Bots:** Easy, Normal and Hard. Harder bots react faster, read your spikes, dive, block and aim
  for open sand.
- **Scoring:** rally scoring to 11, 21 or 25, win by two.
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
| Block | Jump at the net | Jump at the net |
| Switch 1st / 3rd person | V | Right stick click |
| Show / hide control hints | H | Select / Create |

## Running it

1. Install [Git LFS](https://git-lfs.com) before cloning, because images and fonts are stored with LFS.
2. Open the project folder in **Unity 6000.6.8f1**. It uses URP and the Input System, and both come
   with the project.
3. Open `Assets/_Game/Scenes/Match.unity` and press **Play**.

The EditMode tests run from **Window → General → Test Runner**.

## Project layout

- `Assets/_Game/`: all game code, scenes, prefabs and materials
  - `Scripts/`: ball, players, aiming, camera, input, UI and art code
  - `Tests/EditMode/`: tests for ball arcs, hitting, stamina, rules, movement, AI and blocking
- `docs/PLAN.md`: the design and milestone plan
- `docs/ART_BIBLE.md`: the style and colour palette
- `AGENTS.md`: instructions for AI coding agents working in this repo

## Status

Milestones 0 to 3 of the plan are done: court and cameras, hitting, rules and scoring, characters,
movement, AI difficulties and blocking. Next up are local multiplayer, then sound, menus and polish.
There is no difficulty menu yet: set `difficulty` on the Match Manager in the scene. Controllers are
wired up but haven't been play-tested yet.
