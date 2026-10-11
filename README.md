# Island Sim

A browser prototype of an Anno/Tropico-style city-builder set in a fictional Maldives-like archipelago, where terraforming is the core mechanic.

## Play

Serve the folder and open `index.html`, for example `python3 -m http.server` then http://localhost:8000. There is no build step; Three.js 0.170 loads from jsDelivr. Opening the file directly from disk will not work, because browsers block reading local textures.

Pick a graphics preset under the gear button at the top right: **Low** (phones), **High** (refraction, bloom, grass) or **Ultra** (adds ambient occlusion and tilt-shift). The same panel sets the UI size from 90% to 150%. Both are remembered.

## What's in it

- A 720×500-tile world with four atolls and a lone island, each with its own outline: Maarandu Atoll (home, a rounded triangle with 12 islands), Thoravi Atoll (a long, curved double chain full of ring reefs), Fenumaa Atoll (a long oval with 15 islands, big wooded and wetland ones among them), Oivaru Atoll (a lopsided kidney with 10 islands) and Huvamaru (one long island with two lakes and no lagoon). Names are invented.
- Any island can be renamed from its panel; names are saved with the game.
- Every Maldivian island type: rim islands, long thin islands, big wooded islands, kulhi islands with mangrove wetland and a brackish pond, vilu islands ringing a pool, bare finolhu sandbanks, protected bird islands, lagoon islets on faro ring reefs, and channels through the rim.
- Ground types: kulhi and ponds cannot be built on (raise them to fill them in) but act as fish nurseries, as does seagrass on the lagoon flats; taro grows beside the kulhi; only dhoni berths and jetties stand on a finolhu; bird islands are protected.
- Palm Isle (start), Coral Isle (low coral ridges for stone) and Kaashi Cay (coconut land behind lagoon shallows) keep their roles in the home atoll.
- Sand roads can be painted between buildings.
- Terraform with Raise, Lower, Level (to a picked height: deep, channel, reef, beach, land or ridge) and Smooth, using one round brush from 1 to 9 tiles across. Hold the mouse still to keep raising, lowering or smoothing; tiles the brush would refuse show red, and the cursor shows what one pass costs. Ctrl+Z undoes reshaping from the last 10 seconds. Not in the open ocean beyond the reefs. Raising costs stone from the nearest jetty; lowering returns some.
- Deep water carries boats, shallow reef blocks them but feeds fisheries. Dredge channels, build land bridges, or split islands.
- Each island has its own warehouse at its jetty; dhonis shuttle goods to the capital. Land bridges merge warehouses.
- Trading ships call at the capital's jetty (the trade post) every two in-game days. They buy the exports you allow (coir rope, dried fish, cowries; coconuts and fish if you choose) and sell rice.
- **Age 1, the Age of Cowries:** the game starts in the first of five planned ages (see the "Ages and Buildings" doc).
- Compounds (walled family plots of 3×2 or 4×3 tiles with a thatched coral house; the big one holds twice the families) need fish or rice, then coconuts and a mosque within 10 tiles. Each need met makes room for more residents and raises the tax, and sweet toddy raises it further.
- Buildings: fishing dhoni berth, coconut grove, toddy tapper, fish drying racks (fish and firewood into hiki mas), timber cutter, coral stone cutter (works the reef flat), coir rope yard, cowrie beach, island mosque and the Sultan's palace. Worked buildings can be paused. Residents walk up to 10 tiles to work, and buildings beside a sand road work 25% faster.
- The charter walks through the age, and the palace lists what it takes to end it: 60 fishers with all needs met, a rope yard, a trading ship's visit, 1,500 gold, 40 coir rope and 30 coral stone.

## Controls

The screen follows the UI design doc ("Island Sim UI Design"): a top bar with gold, the goods in the warehouse of the island you are looking at and residents; the charter card at the top right; the katheeb's messages on the left; the minimap and compass at the bottom left; the build bar at the bottom; and the selection panel at the bottom right. Press **F1** in the game for a labelled overlay.

- **Build:** pick a category (1 Terraform, 2 Homes, 3 Food, 4 Industry, 5 Civic, 6 Harbour, 7 Roads), then a card. Hover a card for what it needs and makes. The cursor shows the cost and, if a tile is refused, why. Right-click or Esc drops the tool.
- **Inspect:** click a building for its inputs, outputs, workers and actions (pause, demolish), or an island's floating name for its warehouse. Tab jumps to the next settled island.
- **Camera:** right or middle drag, Shift + drag, Q/E or the ⟲ ⟳ buttons rotate (hold to keep turning; two-finger twist on touch); wheel or pinch zooms; WASD pans; click or drag on the minimap to move; click the needle to face north.
- **Save:** the menu (top left) has three save slots, an autosave each minute, and New game. The autosave loads when you return.
- **Losing:** the atoll is lost if debts pass 300 gold, or if unrest reaches 100% because too many families went hungry.
- **Keys:** V survey, R raise, F lower, T level, Y smooth, [ and ] brush size, Ctrl+Z undo reshaping, G sand road, X demolish, H jetty, C compound, P plot size, Space pause, Esc drop tool / close / menu, F1 help.

## Update log

See [CHANGELOG.md](CHANGELOG.md) for what changed in each update.

## Graphics

- Physically based lighting with a Preetham sky, image-based lighting from that sky, ACES tone mapping and soft 4K shadows.
- Terrain blends seven texture layers (sand, grass, forest floor, rock, reef, soil, road) with normal maps; rock is projected triplanar on cliffs. Wet sand at the waterline, caustics on the sea floor, drifting cloud shadows.
- Water: layered normal maps, Fresnel sky reflection, depth-based colour absorption, screen-space refraction, sun glints, swells that roll in and break on the beach, swash that runs up the sand, and reef-crest foam.
- Instanced palms, broadleaf trees, bushes, rocks and grass that sway in the wind; plaster, tile, thatch and wood buildings.
- Post-processing: bloom, GTAO ambient occlusion and tilt-shift on Ultra.

Textures in `assets/textures` are generated by `tools/gen_textures.py` (run `python3 tools/gen_textures.py`; needs numpy and Pillow), except `water_normal.jpg`, which comes from CesiumJS (see `NOTICE.md`). Scanned CC0 textures from Poly Haven or ambientCG can replace any of them: keep the file names and pixel sizes (the six terrain layers must all be the same size), albedo in sRGB and OpenGL-style normal maps.
