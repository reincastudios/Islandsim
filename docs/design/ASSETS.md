# Assets

## First asset set (Blender)

| Asset | Variants | Triangles (LOD0) | Notes |
|---|---|---|---|
| Island terrain kit | Beach edge, cliff edge, reef rocks, sandbar | 500–2k per piece | Dressing only. The playable terrain stays generated from the height grid |
| Palm | 3 heights, 2 leans | 600–1.5k | Wind-ready: trunk and fronds separate, pivot at base |
| Pier | Straight, corner, end | 300–800 per piece | Modular, snaps on a 1-tile grid |
| Boat | Cargo sloop | 1.5–3k | Pivot at waterline centre, faces +Z |
| Fishing hut (Fishery) | 1 | 1–3k | On stilts, nets drying |
| Houses | 3 variants, plus a rum-tier upgrade | 1–3k | Thatch and timber; rum tier gets terracotta roofs |

Later: harbor, lumber camp, quarry, sugar field, distillery, props (crates, barrels, nets, carts).

## Rules (Blender to Unity)

- **Scale:** 1 tile = 1 m in Unity = 1 m in Blender (the sim tile is 1 Unity unit). A house is about 0.8 × 0.8 tiles footprint.
- **Axes:** model with Z up in Blender, face −Y; export FBX with "Apply Transform" so it lands facing +Z in Unity.
- **Pivot:** bottom centre of the footprint.
- **Naming:** `SM_<Asset>_<Variant>` for meshes, `M_<Material>` for materials, `T_<Asset>_<Map>` for textures.
- **LODs:** LOD0 as above, LOD1 at 50%, LOD2 at 15%.
- **Materials:** one shared trim-sheet atlas per asset family where possible. Colours follow MOOD.md.
- **Export:** FBX to `unity/Assets/IslandSim/Art/Models/<Asset>/`, textures to `unity/Assets/IslandSim/Art/Textures/<Asset>/`. Keep the `.blend` source in `art/blender/`.
- **Check:** render a three-quarter preview next to a 1.7 m scale figure before export, and save it to `art/previews/`.

## Wiring into the game

`Scripts/View/Models.cs` builds the procedural greybox models. Authored prefabs replace them per building type once approved; the greybox stays as a fallback.
