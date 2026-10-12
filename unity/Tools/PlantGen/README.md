# Plant generator

Makes the game's own plant models in `Assets/IslandSim/Resources/IslandSim/Plants/` and their textures in `PlantTextures/`.

- **Coconut palms** (`palm`, `palm_1`, `palm_2`): `palm_tex.py` paints the frond (green and dry halves in one texture), trunk bark with its normal map, and coconut skin. `palm_geo.py` builds the curved trunk, arching fronds with V-folded, drooping leaflets, hanging dry fronds and coconuts, at three levels of detail.
- **Trees and bushes** (`tree`, `tree_1`, `tree_2`, `bush`, `bush_1`): generated with [ez-tree](https://github.com/dgreenheck/ez-tree) (MIT) using its photo bark and leaf textures. `gen.mjs` runs it headless and simplifies the branches with meshoptimizer; `build_trees.py` thins the leaves for the far levels (scaling the rest up to keep the canopy full), bends leaf normals outward for soft canopy light, and writes the OBJ files.
- `render.py` / `prev_palm.py` draw quick previews for checking a change without Unity.

```
npm install three@0.170.0 meshoptimizer && npm pack @dgreenheck/ez-tree && tar xzf dgreenheck-ez-tree-*.tgz
node --import ./register.mjs gen.mjs '[{"name":"tree","preset":"Ash Small","lods":[0.5,0.12,0.03]}, ...]'
python3 build_trees.py && python3 palm_tex.py ../../Assets/IslandSim/Resources/IslandSim/PlantTextures/ && python3 palm_geo.py ../../Assets/IslandSim/Resources/IslandSim/Plants/
```
