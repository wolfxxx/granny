# Sporty Granny's Island

A small 3D platformer made with **Unity 6 (URP)** and **Blender**. Run, sprint and jump through a sunny island as a sporty grandma: front-flip off double jumps, bounce on giant mushrooms, climb a spiral trail of floating islands and collect all 25 golden stars.

**▶ Play in your browser: https://wolfxxx.github.io/granny/**

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD / arrow keys | Left stick |
| Sprint | Shift | Left stick click / right trigger |
| Walk | Ctrl | |
| Jump (press again in the air for a flip) | Space | A |
| Look | Mouse | Right stick |
| Zoom | Scroll wheel | |
| Free the cursor | Esc | |
| Show / hide help | H | |

Click the game once to capture the mouse (this also enables sound in the browser). Granny can't swim: fall in the water and she's put back on dry land.

## What's in the world

- Island terrain with meadows, beaches, dirt paths, a pond with stepping stumps and a mountain
- Granny's cottage and garden (a bouncy mushroom will get you onto the roof)
- A spiral trail of 14 floating islands up to 37 m, with mushroom boosts on the way
- 25 collectible stars, a star counter and a celebration when you get them all
- Animated stylized water, procedural sky and drifting clouds, floating pollen, dust and sparkle effects
- Sound effects and birdsong synthesised at runtime (no audio files)

## Repository layout

```
docs/      WebGL build served by GitHub Pages
unity/     Unity project (open this folder with Unity 6000.6 or newer)
blender/   GrannyProps.blend: source models for the trees, rocks, mushrooms, cottage, islands, star…
```

Inside `unity/Assets/GrannyWorld/`:

- `Scripts/`: gameplay: `GrannyController` (movement, coyote time, jump buffering, double-jump flip, bounce pads, respawn), `OrbitCamera`, `Collectible`, `BouncePad`, `GameHUD`, `Fx`, `GameAudio` (procedural sound)
- `Editor/GrannyAnimationBuilder.cs`: generates Granny's Humanoid animation clips (idle, walk, run, jump, fall, flip tuck, land) from Mecanim muscle curves and wires the Animator Controller
- `Editor/GrannyWorldBuilder.cs`: generates the whole scene: terrain, splat textures, grass, props, sky trail, stars, water, lighting, post-processing, player and camera
- `Editor/GrannyWebBuild.cs`: WebGL build set up for static hosting (gzip + decompression fallback)
- `Shaders/`: stylized water and a minimal particle shader for URP

## Rebuilding from source

The Granny character is the **"Fitness Grandma" from [Mixamo](https://www.mixamo.com)** and is **not included** in this repository: Mixamo's terms allow using characters in games but not redistributing the raw files. The playable build in `docs/` includes her normally.

1. Download *Fitness Grandma* from Mixamo as FBX for Unity (T-pose, with skin).
2. Save it as `unity/Assets/GrannyWorld/Models/Granny/SportyGranny.fbx`. The existing `.meta` file already holds the Humanoid rig and material settings.
3. Open `unity/` in Unity and run **Tools → Granny World → Build Everything**. This extracts her textures, regenerates the animations and rebuilds `Scenes/GrannyWorld.unity`.
4. Press Play, or use **Tools → Granny World → Build WebGL** to make a new web build.

Want more natural motion? Mixamo animations for the same character retarget straight onto her Humanoid rig. Drop them into `Animations/Granny.controller`.

## Credits

- Character: Fitness Grandma by Mixamo (Adobe)
- Everything else (props, terrain, animation, code, shaders, sound) was made for this project with Unity, Blender and [Claude Code](https://claude.com/claude-code)
