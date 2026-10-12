# Mechanics

Numbers live in `unity/Assets/IslandSim/Scripts/Core/Defs.cs` and `Sim.cs`. This doc says what they mean and why. Change both together.

## Layer order

Build and polish one layer at a time. A layer is done when it plays in the greybox, has EditMode tests, and the reviewer approved it. The original 8-step workflow listed walking, casting, bait, catching, and day and night; those belonged to a fishing game, so the layers below are the city-builder equivalent.

| # | Layer | State |
|---|---|---|
| 1 | Camera and terrain reading (pan, rotate, zoom, survey heights) | Ported, needs a play test in Unity |
| 2 | Terraforming (raise, lower, level, brush sizes, costs) | Ported, needs a play test |
| 3 | Building placement (height rules, coast rules, demolish) | Ported, needs a play test |
| 4 | Economy (houses, fish, wood, stone, sugar, rum, gold, workers) | Ported, needs a play test |
| 5 | Islands, warehouses and boats (land bridges, canals, shuttles) | Ported, needs a play test |
| 6 | Day and night cycle | Not started, see below |
| 7 | Charter goals and progression | Ported, needs a play test |
| 8 | Save and load | Ported (F5 / F9) |

## 1. Camera and reading the land

- RTS camera: pan, rotate, tilt, zoom. See [UI.md](UI.md) for bindings.
- The Survey tool shows the height and terrain name of the tile under the cursor.

## 2. Terraforming

- **Raise** a tile by one: costs gold plus stone from the nearest harbor's warehouse.
- **Lower** a tile by one: refunds half a stone.
- **Level** a brush to a target height.
- Brushes are 1×1, 3×3 and 5×5.
- Reshaping a tile clears its trees. A tile with a building must be demolished first.
- Heights are clamped to −3..7.

## 3. Buildings

| Building | Cost | Upkeep/s | Height | Jobs | Notes |
|---|---|---|---|---|---|
| Harbor | 300g, 10 wood | 0.4 | 0–1 | 0 | Island warehouse and boat berth. Coastal tile, deep water beside it for boats |
| House | 20g, 3 wood | 0 | 0–2 | 0 | 4 residents on fish, 8 with rum. Pays tax when fed |
| Fishery | 60g, 5 wood | 0.15 | 0–1 | 2 | Coast only. Catch grows with reef tiles within 2 |
| Lumber camp | 50g | 0.1 | 1–4 | 2 | Wood from trees within 2 |
| Quarry | 80g, 5 wood | 0.25 | 3–6 | 2 | Higher ground gives more stone |
| Sugar field | 60g, 4 wood | 0.15 | 1–2 | 2 | +50% within 2 tiles of water |
| Distillery | 150g, 8 wood, 6 stone | 0.4 | 0–3 | 2 | Sugar into rum, 2:1 |

## 4. Economy

- Houses eat fish (0.012 per resident per second); fed houses grow toward their cap and pay 0.1 gold per resident per second. Rum doubles tax and the cap.
- Production buildings run at the island's staffing ratio: residents ÷ jobs, capped at 100%.
- Trees spread slowly onto empty lowland and hills next to other trees.

## 5. Islands and boats

- Each island stores goods at its harbor. Boats shuttle goods from each harbor to the capital along deep water.
- A boat with no deep-water route is "Blocked" and says why.

## 6. Day and night (not started)

Proposal, to be confirmed by Ahmed:
- One day lasts 8 real minutes at normal speed: dawn 1, day 4, dusk 1, night 2.
- Light and colour follow [MOOD.md](MOOD.md). Dusk must stay readable (see FEEDBACK).
- Gameplay first pass: none. Night is visual only (lit windows, harbor lamps, torches). Later options: fisheries rest at night, rum demand rises at night.
- The sun direction in `Atmosphere.cs` becomes time-driven; the sim exposes `T` so the view derives time of day from it.

## 7. Charter goals

1. Grow Palm Isle to 30 residents.
2. Quarry stone on Basalt Isle.
3. Link Sugar Cay to the capital.
4. Distil 25 barrels of rum.
5. Reach 200 residents across the atoll.

## Open questions

- Does night change any rule in the first release, or is it visual only? (Default: visual only.)
- Roads and building footprints larger than one tile: when?
- Trade ships from outside the atoll?
