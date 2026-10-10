# World

## The atoll

- One ring atoll on a **144 × 144** tile map. Centre at tile (73, 83), mean rim radius 44 tiles.
- A deep lagoon inside the ring, open ocean outside, a natural pass through the reef at a bearing of 150°.
- Map generation is deterministic and matches the web prototype tile for tile (`Sim.Generate`, `Defs.Islands`).

## Islands

| Island | Bearing | Peak | Role |
|---|---|---|---|
| Palm Isle | 214° | 4.5 | Start island and capital. Beaches, lowland, palms, some hills |
| Basalt Isle | 313° | 7 | Mountain and stone. Joined to Palm Isle by reef flat, so a land bridge is possible |
| Sugar Cay | 92° | 2.3 | Flat farmland behind wide lagoon flats that must be dredged for boats |

## Heights

Heights are integers from −3 to 7. Every rule keys off these bands.

| Height | Terrain | Meaning |
|---|---|---|
| −3 | Open ocean | Edge of the world |
| −2 | Deep water | Boats sail |
| −1 | Shallow reef | Blocks boats, feeds fisheries |
| 0 | Beach | Harbors, fisheries, houses |
| 1–2 | Lowland | Houses, sugar fields, lumber |
| 3–4 | Hills | Quarries, lumber |
| 5–7 | Mountain | Quarries, best stone |

## Islands as connected land

An island is any connected group of land tiles (height ≥ 0). Each island has its own warehouse at its harbor. Raising a land bridge merges two islands and their warehouses; cutting a canal splits them.

## Open questions

- Do we want more than three islands later, or a second atoll?
- Sea level and storms: in scope for the first release or later?
