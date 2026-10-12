Custom plants for Island Sim

Put an FBX or a prefab here named after the plant it replaces. Add _1 ... _9 copies for variety
(palm, palm_1, palm_2 ... are mixed at random across the islands).

  palm      coconut palms            tree      broadleaf forest trees
  mangrove  kulhi mangroves          bush      undergrowth
  taro      taro beside wetlands     rock      coral rocks on ridges
  grass     grass tufts

- Any size or units: each model is scaled to the height of the plant it replaces and stood on the ground.
- LODs are used if the model has them (an LODGroup, or meshes named _LOD0, _LOD1 ...). A heavy model with no
  LODs is drawn only up close, with the built-in plant standing in farther away.
- Leaves (transparent or cut-out materials, or leaf/frond/grass textures with alpha) get the game's wind.
- Press Play again after adding a file. Delete it to go back to the built-in plant.

Free sources
- Poly Haven (CC0, fine to keep in the repo): island_tree_01, island_tree_02, island_tree_03 as tree, tree_1,
  tree_2; fern_02 or calathea_orbifolia_01 as bush; grass_medium_01 as grass; coast_rocks_01 as rock.
  Download the FBX at 1k or 2k.
- Unity Asset Store (free): "Coconut Palm Tree Pack" by Baldinoboy, "Mobile Palm Pack (URP)" by Next Spring.
  Select the pack's palm prefab, duplicate it (Ctrl+D), move the copy into this folder and name it palm. Asset Store files may not be shared publicly,
  so do not push them to the GitHub repo (it is public).
