# Island Sim (Unity)

The Unity 6 port of the current Island Sim web game (the Age of Cowries). Same 720 × 500 archipelago, generated tile for tile from the same noise as the web build, the same rules, the same terraform panel and HUD, and the same look, rendered with URP.

## Open it

1. Install **Unity 6** (6000.0 LTS or newer) through Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and pick this `unity/` folder.
3. On first open the project sets itself up: linear colour space, a URP asset in `Assets/IslandSim/Settings` (HDR, four shadow cascades, the opaque and depth textures the water refracts, and an ambient-occlusion feature that Ultra switches on), the game's settings asset, and the scene `Assets/IslandSim/Scenes/Atoll.unity`. If anything looks off, run **Island Sim > Set Up Project**, and **Island Sim > Reimport Textures** if the ground looks flat or grey.
4. Open `Assets/IslandSim/Scenes/Atoll.unity` (or **Island Sim > Open Atoll Scene**) and press **Play**.

Pressing Play in any scene also works: a bootstrap creates the game if the scene has none. The game reads the keyboard and mouse through the Input Manager; if the project is set to the Input System only, install the Input System package (the code then uses it) or set **Project Settings > Player > Active Input Handling** to **Both**.

## Controls

The same as the web game (F1 in game shows them):

| Input | Action |
|---|---|
| Click | A building or an island's name for details |
| Left-drag (Survey) | Pan |
| Right or middle drag, or Shift + drag | Rotate and tilt |
| Wheel · WASD / arrows | Zoom · pan |
| Q / E, or the compass | Turn (the needle faces north, the house returns to the capital) |
| 1–7 | Build categories: Terraform, Homes, Food, Industry, Civic, Harbour, Roads |
| R / F / T / Y · G | Raise / lower / level / smooth · sand road |
| [ ] · B · Ctrl+Z | Brush size 1–9 · cycle sizes · undo reshaping (10 s) |
| P · X · V | Compound plot size · demolish · survey |
| Tab | Next settled island |
| Space | Pause |
| Esc | Drop the tool, close a panel, then open the menu |

Saves: three slots and an autosave every minute, as web-format JSON in `Application.persistentDataPath` (the menu lists them). Settings: graphics Low / High / Ultra and UI size.

## Layout

```
Assets/IslandSim/
  Scripts/Core/      Pure C# rules ported line for line from index.html: atoll generation, islands and
                     warehouses, boats, traders, economy, terraforming, the charter, saves. No UnityEngine.
  Scripts/View/      Unity side, ported from the web renderer:
                       World, Geo, Models   tile-space geometry kit and every building, boat and plant model
                       Mats                 the web material table and the ground texture array
                       TerrainView          64-tile chunk meshes, splat layers, shore distance field
                       WaterView            the refracting sea
                       VegetationView       instanced trees, palms, mangroves, bushes, taro, rocks, grass
                       BuildingView         buildings merged per chunk, jetties, pips, dhonis, trading ship
                       CursorView           brush ring, placement ghost, range square, selection outline
                       Atmosphere           sun, sky, haze, reflections, post effects, quality presets
                       RtsCamera, InputBridge, Hud (UI Toolkit), Icons, IslandGame (the main loop)
  Resources/IslandSim/
    Shaders/         IslandLit (all parts, with cloud shadows and wind), IslandTerrain, IslandWater,
                     IslandSky, IslandCursor, IslandOverlay. URP only.
    Textures/        the web game's PBR textures; Fonts/ Young Serif and Familjen Grotesk (OFL)
    UI/              Hud.uss and the panel theme
  Editor/            Project setup (URP asset, SSAO, scene, build settings) and texture import settings.
  Tests/Editor/      EditMode tests for the rules (Window > General > Test Runner).
```

`Sim` is the single source of truth. The views read it every frame and rebuild only what its dirty rectangles say changed, so the rules can be tested and tuned without a scene. The world is built in the web game's tile space (x east, y up, z south) and mirrored into Unity's left-handed space once, so every model and shader matches the web code line for line.
