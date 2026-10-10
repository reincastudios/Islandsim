# Getting Island Sim to look like Anno or Tropico

The browser prototype now uses physically based materials, a depth-tinted water shader with shoreline foam, a sky dome, filmic tone mapping and instanced vegetation. That is about as far as a single HTML file with procedural art can go. The Anno 1800 and Tropico 6 look comes from three things the prototype doesn't have yet: hand-authored assets, texture-splatted terrain and post-processing. This is the workflow to get there.

## 1. Pick the engine

| Option | Why choose it | Watch out for |
|---|---|---|
| **Unity 6 (URP)** — recommended | Huge asset store with tropical and city-builder kits, good terrain tools, solid mobile and PC builds, C# | Licence terms; URP needs tuning for large instanced cities |
| Unreal Engine 5 | Best out-of-the-box realism (Lumen, Nanite, Water plugin) | Heavy for a top-down builder, C++/Blueprints, bigger builds |
| Godot 4 | Free and open, light, good for a small team | Fewer ready-made assets; terrain and water need plugins or custom work |
| Stay on the web (Three.js or Babylon.js) | Instant sharing, no installs | You hand-build every system Unity or Unreal give you; heavier asset streaming |

Keep the current prototype as the design reference: the simulation (heights, islands, warehouses, boats, economy) ports almost line for line to C# or GDScript.

## 2. Terrain that can be terraformed

- Keep the tile height grid as the source of truth, and generate a smooth mesh from it in chunks (for example 32×32 tiles) so edits only rebuild one chunk.
- Texture it with a splatmap: four to eight PBR layers (wet sand, dry sand, grass, jungle floor, rock, cliff) blended by height, slope and a painted mask. Recompute the mask when the player raises or lowers land.
- Add decals and detail meshes (grass clumps, pebbles, shells, roots) with GPU instancing so the ground never looks flat.
- Anno-style trick: slightly exaggerate cliffs and give coastlines a beach rim automatically.

## 3. Water, the signature of a tropical game

- A dedicated water shader with depth-based colour (turquoise over sand to navy over deep water), animated normal maps, screen-space reflections, refraction and caustics on the seabed.
- Shoreline foam from a signed distance field of the coast, so foam updates when the coast changes.
- Unity: Crest Ocean or KWS Water; Unreal: the built-in Water plugin; Godot: a custom shader.

## 4. Assets

- **Style guide first:** one page of reference images (Anno 1800 New World, Tropico 6, real Maldives and Polynesia photos), a palette, and building proportions. Every asset follows it.
- **Modelling:** Blender. Model buildings as modular kits (walls, roofs, porches, props) so variants are cheap. Target 2k–8k triangles per building with 2–3 levels of detail.
- **Texturing:** Substance Painter, or Blender with a trim-sheet workflow so many buildings share one texture atlas.
- **Vegetation:** SpeedTree, or a paid palm and jungle pack. Use wind animation in the vertex shader.
- **Free sources while prototyping:** Poly Haven (PBR textures and HDRIs), Quixel Megascans (free with Unreal), Kenney and Quaternius (stylised placeholder models).
- **Life and motion:** citizens walking between buildings, carts, gulls, smoke from chimneys, flags and palms in the wind, boats with wakes. This matters more for the Anno feeling than raw polygon counts.

## 5. Lighting and post-processing

- One strong sun, a sky HDRI and baked or real-time ambient occlusion.
- Post stack: ACES tone mapping, gentle bloom, colour grading LUT (warm highlights, teal shadows for Tropico), depth of field that blurs the far edges in close-up (the tilt-shift look), ambient occlusion and a light vignette.
- A day–night cycle and weather (tropical storms) later; both feed back into gameplay.

## 6. Suggested order of work

1. Port the simulation and terraforming to the chosen engine with grey-box art.
2. Terrain chunks, splatmap and the water shader; this is where terraforming starts to look good.
3. Lighting and the post-processing stack.
4. One fully finished building set (house, harbor, fishery) to lock the art style.
5. Vegetation, props and ambient life.
6. The rest of the buildings, UI art and audio.

## 7. Team or budget

- Solo or small team: buy one coherent tropical asset pack and one city-builder kit, then customise colours and props to make them yours.
- With budget: a contract 3D artist for the building kit and a technical artist for terrain and water shaders give the biggest jump in quality.
