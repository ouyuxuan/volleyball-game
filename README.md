# Sandspike (working title)

An arcade 2v2 beach volleyball game made in Unity, in a bright low-poly style. It's an early
prototype: right now you can move, jump and hit balls fed to you on a beach court. Rally scoring
and AI teammates and opponents come next.

![Third-person view of the beach court](docs/captures/camera-3rd-low.png)

## How it plays

- **Wind-up hits:** hold bump, set or spike to wind up and build power, then let go to swing. A quick
  tap gives a softer touch. Hold until the ball passes you and you miss it.
- **Hit-now cue:** while you're holding a hit button, the ball glows and a ring appears whenever
  letting go would connect.
- **Charged jump:** hold jump on the sand to crouch, then release. A tap is a hop, and a full charge is
  a high jump for spiking.
- **Stamina:** charged jumps and hits use it up, and it refills over time.
- **Two camera views:** in 3rd person you aim with the mouse cursor, and in 1st person with a crosshair.
- **Controllers:** fully supported. Button hints switch to PlayStation names on a DualShock or DualSense.

## Controls

| Action | Keyboard + mouse | Controller |
|---|---|---|
| Move | WASD | Left stick |
| Aim | Mouse | Right stick |
| Jump (hold to charge) | Space | A / Cross |
| Spike (hold to wind up) | Left click | Right trigger |
| Bump (hold to wind up) | Right click | X / Square |
| Set (hold to wind up) | E | Y / Triangle |
| Switch 1st / 3rd person | V | Right stick click |
| Feed a practice ball | R | Select / Create |

## Running it

1. Install [Git LFS](https://git-lfs.com) before cloning, because images and fonts are stored with LFS.
2. Open the project folder in **Unity 6000.6.8f1**. It uses URP and the Input System, and both come
   with the project.
3. Open `Assets/_Game/Scenes/Match.unity` and press **Play**.

The EditMode tests run from **Window → General → Test Runner**.

## Project layout

- `Assets/_Game/`: all game code, scenes, prefabs and materials
  - `Scripts/`: ball, players, aiming, camera, input, UI and art code
  - `Tests/EditMode/`: tests for ball arcs, hitting, stamina and timing
- `docs/PLAN.md`: the design and milestone plan
- `docs/ART_BIBLE.md`: the style and colour palette
- `AGENTS.md`: instructions for AI coding agents working in this repo

## Status

The project is at milestone 1 of the plan: court, ball, one player and both cameras. Next up are
rally rules and scoring, then AI, local multiplayer, characters, sound and menus.
