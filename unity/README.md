# Island Sim (Unity)

The Unity 6 port of the Island Sim atoll prototype. Same atoll (generated bit-for-bit from the same noise as the web build), same rules, rendered with URP.

## Open it

1. Install **Unity 6** (6000.0 LTS or newer) through Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and pick this `unity/` folder.
3. On first open the project sets itself up: it switches to linear colour space, creates a URP asset in `Assets/IslandSim/Settings`, a lit material in `Assets/IslandSim/Resources/IslandSim`, and the scene `Assets/IslandSim/Scenes/Atoll.unity`. If anything looks off, run **Island Sim > Set Up Project** from the menu bar.
4. Open `Assets/IslandSim/Scenes/Atoll.unity` (or **Island Sim > Open Atoll Scene**) and press **Play**.

Pressing Play in any scene also works: a bootstrap creates the game if the scene has none.

## Controls

| Input | Action |
|---|---|
| Left-drag (Survey tool) or middle-drag | Pan |
| Right-drag | Rotate and tilt |
| Mouse wheel | Zoom |
| WASD / arrows, Shift for faster | Pan |
| Q / E | Turn |
| V R F T X | Survey, Raise, Lower, Level, Demolish |
| H, 1-6 | Harbor, House, Fishery, Lumber camp, Quarry, Sugar field, Distillery |
| B | Brush size 1×1, 3×3, 5×5 |
| Space | Pause |
| F5 / F9 | Save / load (`Application.persistentDataPath/islandsim_save.json`) |

## Layout

```
Assets/IslandSim/
  Scripts/Core/      Pure C# rules: atoll generation, islands and warehouses, boats, economy,
                     terraforming, charter goals, save data. No UnityEngine dependency.
  Scripts/View/      Unity side: terrain mesh with a baked colour map, water, instanced vegetation,
                     procedural building and boat models, RTS camera, input, IMGUI HUD.
  Resources/IslandSim/
                     IslandWater.shader (depth-tinted lagoon, foam, glint) and IslandOverlay.shader
                     (cursor and placement ghosts). Both run under URP and the built-in pipeline.
  Editor/            One-time project setup (URP asset, scene, build settings).
  Tests/Editor/      EditMode tests for the rules (Window > General > Test Runner).
```

`Sim` is the single source of truth. The views read it every frame and rebuild only what its dirty flags say changed, so the rules can be tested and tuned without a scene.

## Where this goes next

This port keeps the prototype's procedural art so the game is playable today. The art pipeline in [`docs/ART_PIPELINE.md`](../docs/ART_PIPELINE.md) (on the graphics branch) is the plan for replacing it: authored building prefabs in `Models.cs`, a splat-mapped terrain shader in place of the baked colour map, and a water system such as Crest or KWS in place of `IslandWater.shader`. The HUD is IMGUI for zero setup and should move to UI Toolkit once the layout settles.
