# Plant generator

Makes the game's own Maldivian plant models in `Assets/IslandSim/Resources/IslandSim/Plants/` and their textures in `PlantTextures/`.

- **Coconut palms** (`palm`, `palm_1`, `palm_2`, and the leaning beach palm `palm_3`): `palm_tex.py` paints the frond (green and dry halves in one texture), the weathered grey trunk bark with its normal map, and coconut skin. `palm_geo.py` builds the tall curved trunk, a full crown of arching fronds with V-folded, drooping leaflets, hanging dry fronds and coconuts, at three levels of detail.
- **Screw pine** (`tree_4`): `pandanus.py` builds the stilt roots, candelabra branches and spiral tufts of strap leaves, plus their textures.
- **Trees and bushes** (`tree` sea hibiscus, `tree_1` tropical almond, `tree_2` Alexandrian laurel, `tree_3` ironwood, `bush`/`bush_1` beach naupaka, `bush_2` octopus bush): branches from [ez-tree](https://github.com/dgreenheck/ez-tree) (MIT, which also supplies the oak and willow bark), shaped per species in `trees_jobs.json`. `leaf_tex.py` paints each species' twig of leaves. `gen.mjs` runs ez-tree headless and simplifies the branches with meshoptimizer; `build_trees.py` thins the leaf cards for the far levels (scaling the rest up to keep the canopy full), bends leaf normals outward for soft canopy light, and writes the OBJ files.
- **Canopy depth**: every leaf and frond vertex stores how deep in the canopy it sits in the whole-number part of its u coordinate (0 open, 15 deep). The game's foliage shader (`IslandLit`, `_FOLIAGE`) reads it back, so inner and lower leaves sit in shade without a second UV set.
- `render.py`, `prev_palm.py` and `prev_obj.py` (renders the finished OBJ files) draw quick previews for checking a change without Unity.

```
npm install three@0.170.0 meshoptimizer && npm pack @dgreenheck/ez-tree && tar xzf dgreenheck-ez-tree-*.tgz
P=../../Assets/IslandSim/Resources/IslandSim/Plants/ T=../../Assets/IslandSim/Resources/IslandSim/PlantTextures/
node --import ./register.mjs gen.mjs trees_jobs.json && python3 build_trees.py $P
python3 leaf_tex.py $T && python3 palm_tex.py $T && python3 palm_geo.py $P && python3 pandanus.py $P $T
for b in oak willow; do cp package/src/lib/assets/bark/${b}_color_1k.jpg $T/isim_bark_$b.jpg; cp package/src/lib/assets/bark/${b}_normal_1k.jpg $T/isim_bark_${b}_normal.jpg; done
```
