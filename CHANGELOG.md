# Update log

Newest first. Each entry says what changed for the player and where the code lives.

## 2026-10-11

### A longer Fenumaa, a fourth atoll and island renaming (PR #5)
- **Fenumaa Atoll is longer and fuller:** it is now a long oval about 200 tiles from end to end (it was a 100-tile heart) and holds 15 islands instead of 8. New ones: Breadfruit Isle, Kalhuvaa Isle (a second long island), Ambu Vilu, Saruvaa Isle, Keyo Isle (wooded), Hiri Finolhu and Midhili Islet out in the lagoon. It has four channels through the rim instead of two.
- **A fourth atoll, Oivaru Atoll,** sits east of Thoravi: a lopsided kidney with 10 islands (Faalu, Rasgiri with coral ridges, wooded Weaver Isle, Ruh Kulhi Isle with mangroves, Dhoshi Vilu, long Kanbili Isle, Moonrise Isle, Fehi Finolhu, Booby Islet for the birds, and Neru Islet on a lagoon ring reef). The world grew to 576×400 tiles to fit it, and the camera zooms out far enough to see all of it.
- **Rename any island:** open an island (click its name on the map, the top bar chip, or Tab) and press **Rename**. Type a name and press Enter or click away to keep it; Esc cancels; an empty box brings back the map's name. The new name shows on the map, the panel, the top bar and building panels, and is saved with the game. The panel remembers the old name ("once Great Wood Isle"). Typing a name doesn't trigger hotkeys.
- **Fix:** sandy reef patches along the rim no longer count towards the nearest island's name, which had merged names such as "Palm–Dhooni" for islands that weren't actually joined.
- **Fix:** the sea's haze now recedes with zoom like the land's, so the far-out view isn't washed out. The minimap matches the wider world and fits its panel.
- Saves from before this update can't be loaded (the world changed size); the game starts fresh.

### A bigger world: three atolls and a lone island (PR #5)
- **Six times the map:** the world is now 416×352 tiles instead of 144×144. The home atoll, **Maarandu Atoll**, is about twice as wide and holds 12 islands instead of 3. Two new atolls sit across open ocean: **Thoravi Atoll** in the north, long and curved with a waist like a double chain and full of faro ring reefs, and **Fenumaa Atoll** in the south, heart-shaped with fewer, bigger islands. **Huvamaru**, a single long island with two lakes and no lagoon, stands between them. All names are invented, Maldivian in flavour, as Ahmed asked.
- **Every island type, each with its own look:** rim islands, long thin islands (a future airfield site), big wooded islands with dense forest, kulhi islands with mangroves around a brackish pond, vilu islands that ring a sheltered pool, white finolhu sandbanks, bird islands, and small islets sitting on ring reefs in the lagoons.
- **Ground types that matter:** kulhi wetland and ponds can't be built on and block roads (raise the ground to fill them in), but every kulhi, pond or seagrass tile within 3 tiles adds to a fishery's catch. Taro fields grow beside the kulhi. Only a fishery or a harbor can stand on a finolhu. Bird islands are protected from building and terraforming. Survey and build tooltips name the ground.
- **Getting around:** zoom out further (the camera reaches the whole world); atoll names replace island names when zoomed far out, and clicking one flies there. The island panel names the atoll. The minimap shows the whole world.
- **Speed:** the ground and plants are drawn in 64×64-tile chunks, so an edit rebuilds only the chunks it touches (about 4 ms instead of half a second) and far plants are skipped; the coastline field is recomputed only around the edit. Saves are packed and stay around 40 KB.
- Older saves can't be loaded into the new world; the game starts fresh with a notice. The design is in the "Island Sim: World Map and Ages" doc.

### Roadmap milestone 1: a real economy (PR #5)
- **Tighter gold:** the game starts with 800 gold instead of 1,200, compounds pay 20% less tax, and every worked building and harbor costs a little more upkeep. Doing nothing now slowly loses money.
- **Sand roads matter:** a fishery, lumber camp, quarry, sugar field or distillery with a road touching any side works 25% faster. Roads stay optional. The building panel says whether it has a road.
- **Workers walk to work:** residents fill jobs only within 10 tiles of their compound, on the same island, nearest first. A building with no one in reach stops, and the katheeb says so. When placing, the cursor shows how many residents live within walking distance.
- **You can lose:** if debts pass 300 gold the sultan takes the atoll, and if unrest reaches 100% the islanders depose you. Unrest rises while more than 30% of residents are hungry and falls when they are fed. It shows in the top bar from 5%, and the katheeb warns at 40% and when the treasury is empty. A closing screen offers a new atoll or loading a save.
- **Saving:** the menu has three save slots, an autosave every minute, and New game (click twice). The autosave loads when you come back.
- **Fixes:** the starting fishery is placed by reef shallows when there are any, catches more without reef (6 a minute instead of 3.6), and the capital starts with 40 fish. Its status now says "No reef in reach, so the catch is small" instead of "0 reef tiles nearby".
- The roadmap ("Island Sim Roadmap" doc) records Ahmed's decisions: roads give a bonus, toddy and coconut goods will replace rum, players can lose, rising sea level is a theme with ways to fight it, and the web game comes before the Unity port.

### New Anno/Tropico-style interface (PR #5)
- The whole screen was redesigned from the UI design doc: panels look like carved teak boxes with a whitewashed coral face and a red-black-ochre lacquer band, and an in-house icon set replaces text labels.
- **Top bar:** gold with its change per minute, then wood, coral stone, fish, sugar and rum for the island you are looking at, each with a trend arrow; residents; day and game speed. Hover a counter to see how much is made and used. A counter turns red and pulses when a good runs out.
- **Build bar:** six categories (Terraform, Homes, Food, Industry, Harbour, Roads, keys 1–6) open a row of build cards showing cost chips (red when you can't afford them) and a tooltip with what each building needs and makes. Survey and Demolish sit at the ends. The old left tool panel is gone.
- **Selection panel:** click a building to see its status, inputs → building → outputs per minute, workers and output, with Pause/Resume and a two-click Demolish. Compounds show residents and whether they have fish and rum.
- **Island panel:** click an island's floating name, the island name in the top bar, or press Tab, to see its warehouse with trends, residents, jobs and its boat. The old right-hand island table is gone.
- **Charter card:** the current goal with a progress bar; ▾ lists all goals; a met goal flashes the card.
- **The katheeb:** the island chief's messages replace toasts on the left, with a portrait. They fade after 6 seconds; click one to go to the place. The bell keeps the last 20. He now also warns when an island runs out of fish or sugar, has more jobs than residents, or a boat is stuck.
- **Minimap and compass:** a live map of the atoll with the camera's view and boats; click or drag to move. ⟲ ⟳ rotate, ⌂ returns to the capital, and the compass needle points north (click to face north).
- **At the cursor:** placing shows the cost and, when a tile is refused, the reason ("The 3×2 plot must be level"). Roads and terraforming show a running total while you drag. Right-click without dragging drops the tool.
- **Also:** F1 opens a help overlay that labels the screen; a settings panel holds graphics quality and a UI size from 90% to 150%; narrower screens get icon-only cards, and phones a short top bar and a scrolling bottom build bar.
- Keys changed: 1–6 now open build categories, and the compound moved to C. Saves keep paused buildings.

### Compounds, one continuous wave, clear roads (PR #5)
- Houses are now **compounds** (goathi): a 3×2-tile family plot (six old house tiles) inside a low coral wall with a gate. Inside is an old-style coral-stone house with lime-washed walls and a hipped thatch roof, a kitchen hut, a well, two joali seats, a coconut palm and a swept white-sand yard. The plot needs level ground and turns 90° by itself to fit. Compounds cost 60 gold and 6 wood and hold up to 6 residents on fish, 12 with rum. Larger plot sizes can come later.
- The game starts with three compounds instead of four houses, so it still opens with 12 residents.
- Waves are now one continuous motion shared by the sea and the beach. Each swell rolls in, steepens, tips over into foam at the waterline, runs up the sand as a thin sheet behind the same foam front, then drains back. Spent foam drifts in the shallows, and the waterline foam surges as each wave lands.
- Nothing grows on sand roads any more (no grass, bushes, trees or rocks), and nothing grows inside compound plots except the yard palm.
- Saves from before compounds start a new game.

### Maldives islands, sand roads and lapping waves (PR #5)
- Islands are now flat like real Maldivian islands: a ring of white beach around ground about a metre above the sea. Coral Isle has a few low coral ridges (height 2) and Palm Isle a couple of tiles; terraforming tops out at height 3.
- Basalt Isle is renamed Coral Isle. Quarries now cut coral stone from those low ridges (height 2+), and the charter goal follows.
- New **Sand road** tool (G, 2 gold per tile, works with the 1×1/3×3/5×5 brush). Roads clear trees, run up to buildings, and Demolish removes them for a 1 gold refund. They are saved with the game.
- Waves now lap the shore: small swells roll in and steepen in the shallows, a thin sheet of water slides up the beach behind a lacy foam edge and drains back, and the sand stays damp where the sea last reached. On High and Ultra the foam line follows the exact waterline.
- Saves from the hilly map start a new game, because the map changed.
- The Unity port (PR #3) still has the old hilly map.

### Easier camera rotation (PR #5)
- Middle-drag rotates the view, as do Shift + left-drag and Alt/Ctrl + left-drag (useful on trackpads and one-button mice). Right-drag still rotates and tilts.
- The ⟲ ⟳ header buttons turn 45° smoothly on a tap and keep turning while held.
- The in-game hint and README list every way to rotate.

## 2026-10-10

### Photorealistic web renderer (PR #5)
- Moved to Three.js 0.170 with a physical sky, sky lighting, ACES tone mapping and soft 4K shadows.
- Ground blends six textures (sand, grass, forest floor, rock, reef, soil) with normal maps, wet sand at the waterline, caustics on the sea floor and drifting cloud shadows.
- New water: see-through turquoise that darkens with depth, sky reflections, refraction, sun glints and foam on shores and reef crests.
- Palms, broadleaf trees, bushes, rocks and grass sway in the wind. Buildings use plaster, tile, thatch and wood textures.
- Bloom, ambient occlusion and tilt-shift. Lo/Hi/Ul graphics presets in the header, remembered between visits.
- Textures are generated by `tools/gen_textures.py`; the water normal map is from CesiumJS (see `NOTICE.md`).
- The page now needs to be served over HTTP (see README).
- Fixed: on High, most of the screen could go black because overbright caustics were smeared by the bloom.

### Unity 6 port (PR #3)
- A Unity 6 URP project in `unity/` with the same rules and map as the web version, a scene that sets itself up on first open, an IMGUI HUD and NUnit tests. Not yet opened in a real Unity editor.

### Design workflow docs (PR #4)
- `CLAUDE.md`, design docs in `docs/design/`, agent roles and the Unity/Blender MCP setup guide.

### Realistic graphics pass (PR #2)
- PBR terrain, a water shader, sky, instanced palms, and an art pipeline guide in `docs/ART_PIPELINE.md`.

### Natural atoll (PR #1)
- The atoll rim is irregular instead of a perfect ring, Palm Isle is bigger, and the map is 144×144.

### First 3D prototype (main)
- One tropical atoll holding Palm Isle, Basalt Isle and Sugar Cay, with raise, lower and level terraforming, harbors, boats, per-island warehouses, houses and five production buildings.
