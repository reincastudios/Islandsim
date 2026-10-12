Plants for Island Sim

The game ships its own realistic plants here: coconut palms (palm, palm_1, palm_2), forest trees (tree, tree_1,
tree_2) and bushes (bush, bush_1), each with two lower levels of detail in *_LOD1 and *_LOD2. Their textures are in
../PlantTextures. They were made with unity/Tools/PlantGen. Delete a model to go back to the simple built-in plant.

To use a model of your own instead, put an FBX or prefab here with the same name (and remove the OBJ files of that
name). Slots: palm, tree, mangrove, bush, taro, rock, grass, plus _1 ... _9 for variety, mixed at random.

- Any size or units: each model is scaled to the height of the plant it replaces and stood on the ground.
- LODs come from an LODGroup, meshes named _LOD0, _LOD1 ..., or separate files name_LOD1, name_LOD2. A heavy model
  with no LODs is drawn only up close, with the built-in plant standing in farther away.
- Leaves (transparent or cut-out materials, or leaf/frond/grass textures with alpha) get the game's wind.
- Press Play again after changing a file.

Other free sources: Poly Haven (CC0) island_tree_01..03, fern_02, grass_medium_01, coast_rocks_01; Unity Asset Store
"Coconut Palm Tree Pack" (Baldinoboy). Asset Store files may not be shared publicly, so never push them to the
public GitHub repo.
