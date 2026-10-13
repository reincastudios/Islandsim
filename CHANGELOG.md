# Update log

Newest first. Each entry says what changed for the player and where the code lives.

## 2026-10-13

### Unity: the sea and the waves on the beach read as one body of water (PR #6)
- The sand now shelves gently under the sea for three to four tiles off every coast (TerrainView.Shelf, look only), and the shelf floor turns pale sand as it rises. The beach runs down to meet the water almost flush instead of ending in a lip.
- The water measures its true thickness from the camera depth at every quality setting. Over the shelf it fades from clear at the waterline to turquoise as it deepens, so there is no line where the sea stops and the sheet of water on the sand starts. Thin water is glassier and mirrors the sky less.
- The static white outline at the waterline is mostly gone between waves. Foam now belongs to the waves: a lacy net of bubbles (FoamNet in IslandCommon.hlsl, warped Voronoi strands torn into patches) spreads over the shallows after each wave lands and rides up the sand with the sheet, drawn from the same coordinates on both, so the lace runs unbroken across the waterline.

### Unity: coral sand, a greener Palm Isle and waves that lap the beach (PR #6)
- Beach sand is new: warm coral-white with fine grain, soft wind ripples and a few tiny pale coral and shell bits, in place of the old yellow sand with dark specks. The generator is unity/Tools/TexGen/sand_tex.py. Wet sand now darkens to a warm damp tan instead of grey-blue.
- Palm Isle is much greener. New games fringe its beaches with coconut palms and put palm groves in its clearings, with shade trees in the thick interior (the other islands are unchanged). In any game, Palm Isle's forest also draws more second trees and undergrowth.
- Waves lapping at the shore: the run-up edge now scallops into cusps that drift along the beach, each wave leaves a bubbly foam line where it stopped, foam streaks pull back with the draining water, and the just-drained sand glistens before it dries. The extra shore lookups only run on the draining sheet.


### Unity: fixes found by playing in the editor (PR #6)
- **Brush ring:** the terraform and planting ring never drew. Its mesh had fewer vertices than its triangles referenced, and Unity logged an error every frame. It now uses its own indexed grid, so the ring and its red refused tiles show again.
- **Missing text:** the names on cards (Raise, Lower, Timber cutter and so on) and the advisor notes were invisible because buttons got no text colour. Buttons now default to the dark ink colour.
- **Stuck stroke:** if a mouse release was missed, the brush stroke stayed open. It kept raising land and blocked every later click. An open stroke now closes as soon as no button is held.
- **Popovers:** the menu, settings and log opened behind the advisor notes and the charter card. They now come to the front. Warning notes use a softer tint. The Settings box is wider, so the UI size buttons no longer wrap into a column of digits.
- **Where:** `RingGrid` in `Scripts/View/CursorView.cs`, `Pointer` in `IslandGame.cs`, `TogglePop` in `Hud.cs`, and `Button` and `.msg.bad` in `Hud.uss`.

## 2026-10-12

### Unity: richer top and build bars, HUD sized to the screen (PR #6)
- **What changed:** the top bar now has a honey inlay line along its bottom edge, above a deeper carved coral-stone frieze. The island name is a honey plaque, and the resource counters sit in a recessed well with bold numbers. The build bar is now a teak board in the same frame as the panels, with cream labels and honey icons. The selected tab is a raised coral-stone tile with a honey edge. Tabs, icons and labels are a little larger.
- **Fixed:** the HUD was drawn at a fixed physical size, so it looked small on bigger screens and in the Game view. It now scales with screen height (sized for about 1000 px, from 0.8× to 2×), times the UI size chosen in Settings.
- **Where:** `Resources/IslandSim/UI/Hud.uss` (`#top`, `#res`, `.isl-chip`, `#bar`, `.tab`), `FitScale` and `BuildTop` in `Scripts/View/Hud.cs`, and the new `ui_frame_wood.png` plus a taller `ui_frieze.png` from `unity/Tools/UiGen/ui_tex.py`.

### Unity: wood-and-coral-stone HUD, a better terraform panel, and tree planting (PR #6)
- **What changed:** the HUD is redesigned in clean Maldivian woodwork and coral-stone carving. The top bar is planed teak. Panels are framed in teak with a honey inlay line and brass corner studs round a pale coral-stone field. A carved coral-stone frieze (the lozenge-and-eye chain cut round old mosque walls) replaces the lacquer stripes under the top bar and every panel header. The build bar is a light framed panel with teak tabs.
- **Terraform panel:** the default Unity slider is gone. Brush size is now a row of nine round pips that grow with the brush (click one, or drag along them; − and + still work, and so do [ and ]). The size shows in the label, and the Ctrl+Z hint sits under it. Level to is a tidy 3 × 2 grid with each height under its name, as on the web version.
- **Plant trees (new, key N):** a fifth Terraform tool that paints trees with the brush for 3 gold each. Pick Coconut palm, Shade tree or Mangrove in the panel, or press N again to cycle. Palms grow from the beach up, shade trees on land above the beach, and mangroves only in a kulhi. Buildings, roads, existing trees, ponds, taro fields and bird islands are skipped. The brush ring turns green, refused tiles show red under it, and the cursor tag shows the cost and tree count. Planted trees count for timber yards and groves like wild ones, and shade trees spread on their own.
- **Where:** `PlantTree` and the Plant branches in `Scripts/Core/SimTerraform.cs`, `Tool.Plant` in `Defs.cs`, the panel in `Scripts/View/Hud.cs` (`RenderCards`, `SetPlant`), new tree, mangrove and sapling icons in `Icons.cs`, styles in `Resources/IslandSim/UI/Hud.uss`, and the textures `ui_wood`, `ui_wood_light`, `ui_frieze` and `ui_frame` drawn by `unity/Tools/UiGen/ui_tex.py`.

### Unity: thicker, jungle-like vegetation (PR #6)
- **What changed:** the islands are much greener and denser, like the real Maldives photos. Where the forest is thick, a second, younger tree or palm shares many tiles. Undergrowth crowds round the trunks, bushes thicken toward the forest edge, and beach naupaka forms a green wall where the sand meets the trees.
- **Gameplay:** unchanged. This is look only: the sim still counts one tree per tile for lumber, coconuts and clearing, so nothing about building or production moves.
- **Speed:** more plant instances, but the same draws per chunk, since every extra plant is instanced with its kind. The extra bushes use their full detail only within about 35 tiles of the camera.
- **Where:** `RebuildChunk` and `TreesAround` in `unity/Assets/IslandSim/Scripts/View/VegetationView.cs`.

### Unity: no more frosted-white trees (PR #6)
- **What changed:** forest trees, bushes and grass no longer look washed out or frosted white from the game camera, and tree canopies look full from above instead of showing gaps.
- **Why it happened:** leaf cards seen at a glancing angle, which from a high camera is most of them, picked up a strong reflection of the bright sky, the same sheen water gets at the horizon. Foliage now takes only the sky's soft ambient light plus the sun, so it keeps its own green. The forest trees' leaf cards also stood upright, so from above you saw them edge-on. They now tip outward from the canopy at 55 degrees, which reads as leaves from both the side and above.
- **Where:** `IslandShadeFoliage` in `IslandCommon.hlsl` (now used by every alpha-tested leaf, frond and grass material), `tilt` in `unity/Tools/PlantGen/build_trees.py`, and regenerated tree and bush models. Same triangle counts.

### Unity: Maldivian island vegetation (PR #6)
- **What changed:** the plants now match an uninhabited Maldivian island. Coconut palms are taller and slimmer with fuller, brighter crowns, and there is a new palm leaning out over the beach. The forest is now sea hibiscus, tropical almond, Alexandrian laurel, ironwood and screw pine (on stilt roots, with orange fruit). The bushes are beach naupaka, which forms the bright green wall along the shore, and silvery octopus bush. Each species has its own painted leaves in rich tropical greens, and each plant gets a slight colour shift so a stand never looks stamped.
- **Fixed:** some palm trunks were bent out of shape. The trunk's gentle wobble picked a new random phase at every point along it, so trunks zig-zagged; it now uses one phase per palm. The same fix keeps each palm's three levels of detail identical, so nothing pops as you zoom.
- **Shading:** the game's own leaves now light like leaves: sunlight glows through them when you look toward the sun, the side away from the sun stays soft instead of going dark, and leaves deep inside a canopy or under a palm crown sit in shade. The depth is baked into the models, so it costs nothing per frame. Leaves have a light waxy sheen.
- **Speed:** a tree is about 5,000 to 7,300 triangles up close, 1,300 to 2,200 in the middle distance and 450 to 800 far away; a bush about 3,500 to 5,000, 1,300 to 1,800 and 500 to 730; a palm about 2,700, 600 and 340. Same three-level scheme and draw counts as before.
- **Where:** models in `unity/Assets/IslandSim/Resources/IslandSim/Plants/`, textures in `PlantTextures/`, made by `unity/Tools/PlantGen` (new `leaf_tex.py`, `pandanus.py` and `trees_jobs.json`). The foliage lighting is `_FOLIAGE` in `IslandLit.shader` and `IslandCommon.hlsl`.

### Unity: realistic palms, trees and bushes (PR #6)
- **What changed:** the islands now grow realistic plants in place of the simple built-in ones. There are three coconut palms (leaning, curved trunks with leaf-scar rings and a swollen base, arching fronds whose leaflets fold into a V and droop, a few dry fronds hanging below, coconuts), three forest trees and two bushes with photo bark and leaf textures. They are mixed at random, sway in the wind, cast shadows and pick up the island light.
- **Speed:** every plant has three levels of detail, chosen per 64-tile chunk by distance (a palm is about 2,200 triangles up close, 480 in the middle distance and 260 far away). Green and dry fronds share one texture so each palm type costs at most three draws per chunk.
- **Where:** models in `unity/Assets/IslandSim/Resources/IslandSim/Plants/`, textures in `PlantTextures/`, made by `unity/Tools/PlantGen` (palms painted and modelled in Python; trees and bushes from the MIT-licensed ez-tree generator with its CC0 bark and leaf photos). Each model can still be replaced with your own. Separate `_LOD1`/`_LOD2` files and name-matched textures are now understood by the loader.

### Unity: drop-in realistic plants (PR #6)
- **What changed:** palms, forest trees, mangroves, bushes, taro, rocks and grass can each be replaced by a model from outside the game, such as a Poly Haven scan, a free Asset Store palm pack or your own Blender model. Put the FBX or prefab in `unity/Assets/IslandSim/Resources/IslandSim/Plants/` named after the plant (`palm`, `tree`...), with `palm_1` to `palm_9` for variety, mixed at random.
- **How it fits in:** each model is scaled to the built-in plant's height whatever units it was made in, so it drops in with no setup. It keeps its own LODs and is drawn instanced per 64-tile chunk like the built-in plants. Its leaves get the game's wind and its textures go through the game's lit material. A heavy model with no LODs is drawn only within about 110 tiles of the camera, with the built-in plant standing in farther away, so the frame rate holds.
- **Code:** `Scripts/View/PlantModels.cs`, `VegetationView` (custom kinds, LOD pick, stand-ins), `Mats.Custom` (leaf detection, wind). The folder's README lists free sources.

### Unity: your own Blender models in the game (PR #6)
- **What changed:** an FBX dropped into `unity/Assets/IslandSim/Resources/IslandSim/Models/`, named after a building (`house`, `fishery`, `palace`, `harbor`...), now replaces that building's built-in model. It is placed, turned, batched and shadowed like the built-in one, and its colours, textures and normal maps are carried onto the game's own lit material so it sits in the same light and haze. Compounds can have one model per plot size (`house_3x2`, `house_4x3`), or one `house` stretched to fit; the capital can have its own jetty (`harbor_capital`).
- **Rules for a model:** 1 Blender metre is 1 tile, origin at the centre of the plot on the ground. The folder's README lists the names. Import settings are set automatically (readable meshes, Blender axes baked in). Code: `Scripts/View/CustomModels.cs`, `Mats.Custom`, `IslandSimModels` in the setup script.
- **Unity is now the main version.** New features go into Unity first; the web version stays as a shareable demo.

### Unity: clicks on the island work again (PR #6)
- **What changed:** in Unity the game drew and ran, but clicking, dragging and scrolling on the island did nothing. The check for "is the pointer over the HUD?" also counted the empty screen behind the HUD, so every click was treated as a HUD click. It now only counts real HUD panels and buttons (`Scripts/View/Hud.cs`, `Over`).

### The current game, rebuilt in Unity 6 (PR #6)
- **What it is:** the Unity project in `unity/` now plays the same game as the web version: the Age of Cowries with its goods, buildings, traders, palace and charter, the 720 × 500 archipelago of four atolls and Huvamaru, the terraform panel (round 1–9 brush, Raise, Lower, Level to a height, Smooth, Ctrl+Z), 3×2 and 4×3 compounds, and the same HUD.
- **Same rules, checked:** the rules were ported line for line to C# (`Scripts/Core`). A side-by-side run of the web code and the C# code builds the same map tile for tile and the same buildings, boats and stock after the same moves. EditMode tests cover a new game, growth, terraforming with undo, the open ocean, saves and the charter.
- **Same look:** every building, boat and plant model, the material table and textures, the splat-mapped ground with wet sand and swash, the refracting sea with foam and caustics, cloud shadows, swaying palms, the physical sky, haze that recedes as you zoom out, bloom and vignette, and the Low / High / Ultra presets (Ultra adds ambient occlusion and tilt-shift). Buildings are merged per 64-tile chunk as in the web game, and plants are instanced and dropped with distance.
- **Same HUD:** lacquer top bar with the warehouse of the island in view, the charter card, the katheeb's advisor feed and log, minimap and compass, build bar with cards, brush, level and plot pickers, building and island panels (trade post, palace list, rename), cursor tag, tooltips, F1 help, three save slots plus an autosave, settings and the game-over box. Built in UI Toolkit with the web game's fonts and icons.
- **Not yet checked in the editor:** this session had no Unity editor, so the scripts were compile-checked against Unity's reference assemblies and the shaders were written against URP 17 but not compiled. The first open in Unity 6 may show shader or layout fixes to make.
- Code: `unity/Assets/IslandSim/Scripts/View/` (one file per part, listed in `unity/README.md`), shaders in `Resources/IslandSim/Shaders`, HUD styles in `Resources/IslandSim/UI/Hud.uss`.

### Wetlands, ponds and sandbanks are back (PR #5)
- **Fixed:** since the Oivaru Atoll update a lost line break had commented out the line that lays ground types on the map, so no island had its mangrove wetland (kulhi), brackish pond, taro fields, seagrass meadows, shifting sandbanks (finolhu) or protected bird nesting ground. Mangroves never grew, and dhoni berths never got their nursery bonus.
- They are back as designed: Raiveli Isle has its mangrove ring round a pond again, Huvamaru its two lakes, and the bird islets are protected. Code: `generate()` in index.html.

### Smoother all the time: buildings drawn in batches (PR #5)
- **Why it lagged:** every part of every building (walls, roof, posts, fence, yard) was its own draw call, so a town of about 100 compounds asked the graphics card for 2,200 to 2,600 draws every frame, and again for the sun's shadow. The game logic itself only takes about 2 ms a frame.
- **Now:** when a building goes up, its fixed parts are baked into place and merged with the same material in 64-tile chunks. The same town draws in 370 to 480 calls, about six times fewer, so frames are steadier on every preset.
- **Nothing looks different:** same models, materials, shadows and water. Side-by-side screenshots of the old and new build on the same town match (only the waves and palm sway differ, because they move). Jetties (they turn toward their berth), swaying leaves and the warning pips stay separate as before.
- Placing or demolishing a building only rebuilds its own chunk. Code: `bakeBuilding`, `rebuildBatches` and `syncBuildings` in index.html.

## 2026-10-11

### Age 1: the Age of Cowries, a terraform panel and bigger compounds (PR #5)
- **Rum and sugar are gone.** The game now starts in Age 1, the Age of Cowries (c. 1200 to 1550), from the "Ages and Buildings" doc. Sugar fields became coconut groves and distilleries became toddy tappers; old saves load with that swap, and rum and sugar stock is dropped. Sugar Cay is now Kaashi Cay ("coconut cay").
- **New goods:** coconuts, sweet toddy, dried fish (hiki mas), coir rope, cowries and rice. The top bar only shows a good once the island has some or uses it.
- **New buildings:** coconut grove, toddy tapper (taps wild palms within 3 tiles; a grove counts as six), fish drying racks (fish plus timber for firewood into hiki mas), coir rope yard (coconuts into rope, within 2 tiles of water), cowrie beach (beach tiles, more shells beside reef shallows), island mosque (2×2) and the Sultan's palace (3×3, one per game, on the capital). A new Civic category (key 5) holds the mosque and palace; Harbour and Roads move to 6 and 7.
- **Renamed for the age:** harbor → jetty (the capital's is the trade post), fishery → fishing dhoni berth, lumber camp → timber cutter, quarry → coral stone cutter, which now cuts from the reef flat (shallow tiles within 2) as well as ridges.
- **Fishers' needs:** food (fish, or rice when fish runs out), coconuts, and a mosque within 10 tiles on the same island. A 3×2 compound holds 4 residents fed, 6 with coconuts and 8 with a mosque too; each need raises the tax by a quarter, and sweet toddy by a half. The compound panel shows each need with a tick or a cross.
- **Trading ships:** every two in-game days a two-masted odi sails in from the open ocean to the capital jetty, trades, and leaves (shown in ochre on the minimap). The capital jetty's panel has the trade post: toggle what it may sell (rope 8, dried fish 6, cowries 5, coconuts and fish 1 gold each) and whether to buy rice (3 gold, up to 40). It keeps 40 rope for the charter and 20 fish and coconuts for the families.
- **A charter for the age:** grow the capital, feed coconuts, build a mosque, sell to a ship, twist 40 rope, build the palace, house 60 content fishers, then end the age from the palace. The palace panel lists each condition (60 fishers with all needs met, a rope yard, a ship's visit, the palace, 1,500 gold, 40 rope and 30 stone at the capital) and an Advance button. Advancing is instant; Age 2 comes in a later update, so for now it marks Age 1 complete.
- **Bigger compounds:** the Homes cards have a plot size switch (3×2 or 4×3, key P). The 4×3 compound has a second house, a vegetable patch and a mat in the yard, costs 110 gold and 11 timber, and holds twice the residents.
- **Terraform panel, as approved in the doc:** Raise, Lower, Level and a new Smooth (Y). One round brush from 1 to 9 tiles across, with − and + buttons, a slider, and the [ and ] keys. Level goes to a picked height: Deep −3, Channel −2, Reef −1, Beach 0, Land 1 or Ridge 2. Holding the mouse still keeps raising, lowering or smoothing a step every 0.3 s. The cursor tag shows what one pass costs, tiles the brush would refuse turn red under the ring, and Ctrl+Z undoes the last strokes from the past 10 seconds with the gold and stone given back.
- Saves are now version 7 (age, trade settings). Version 6 saves still load.

### Faster without looking any different (PR #5)
- **No more minimap stutter:** the minimap's ground used to be repainted in full five times a second (about 25 ms each on the test machine). Now it is painted once and only repainted after the land, roads, trees or buildings change; the regular refresh takes about 0.05 ms. It isn't drawn at all while collapsed on phones.
- **Lighter water on High and Ultra:** the refraction pass under the water no longer draws plants, which all stand on dry land and never show through it. That saves about 210 draw calls a frame in a close view. A side-by-side check showed no visible change.
- **Faster start:** building the world skips atolls far from each tile; the map comes out byte-identical in 1.5 s instead of 2.2 s, and the page is ready in about 4.3 s instead of 5.4 s.
- The "Island Sim: Master Plan" doc collects every decision, what's built, the optimization log, the roadmap and the open questions.

### A round brush ring instead of the box cursor (PR #5)
- The square wireframe box that followed the mouse while terraforming, painting roads or demolishing is gone. In its place a soft round ring lies on the ground, following the terrain, with a gentle glow and a faint grid over the tiles the brush will change. It is gold for Raise, turquoise for Lower, red for Demolish and sand for the rest.
- The brush itself is now round to match: the large brush changes a round patch of 21 tiles instead of a 5×5 square, so coasts come out less boxy. Small is still one tile and medium is 3×3.
- The brush buttons read S, M and L (hover for the width in tiles), and the cursor tag says "large brush" instead of "5×5".

### Bigger islands, Huvamaru further out, no building in open ocean (PR #5)
- **Everything is 1.25× bigger:** the world is now 720×500 tiles. Every atoll, island, reef and lagoon keeps its shape but covers more ground, so Palm Isle has about 1,680 tiles instead of 1,080. The camera zooms out further to fit it.
- **Huvamaru moved** further from Thoravi and Maarandu, so there is open sea on every side of it.
- **Open ocean is off limits:** deep water outside an atoll's outer reef edge can't be dredged, raised or lowered ("Open ocean: too deep to dredge or build out into"). Reef flats and lagoons can still be reshaped.
- The starting fishery now has fewer reef tiles in reach, so the capital's fish run down if you build nothing.
- Saves from before this update can't be loaded (the world changed size).
- The "Island Sim: Terraform Menu and Brushes" doc proposes a simpler Cities: Skylines 2-style terraform panel and round brush, waiting on Ahmed's OK.

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
