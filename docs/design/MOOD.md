# Mood and look

**Feeling:** a warm, calm afternoon on a remote atoll. Anno 1800's New World and Tropico 6 are the touchstones: saturated but not neon, soft shadows, lots of small life.

## Palette

| Role | Colour | Notes |
|---|---|---|
| Deep lagoon | #0E5C7A | Navy-teal, not black |
| Shallow reef | #2FC4C0 | Bright turquoise over sand |
| Beach | #EAD9A8 | Warm pale sand |
| Lowland grass | #7BB04A | |
| Jungle / palms | #3E7A35 | |
| Rock / cliff | #8A8379 | Basalt Isle goes darker, #5B5752 |
| Roofs | #B5532E, #D9A441 | Terracotta and thatch |
| Sky horizon | #CFE6ED | Matches `Atmosphere.Horizon` |

Placeholder values; replace once reference images are in `references/`.

## Light

- One strong warm sun, soft shadows, sky-coloured ambient light.
- Post: ACES tone mapping, gentle bloom, light vignette, slight tilt-shift blur at the far edges when zoomed in.

## Day and night

| Phase | Sun | Feel | Rule |
|---|---|---|---|
| Dawn | Low, pink-gold | Mist on the lagoon | |
| Day | High, warm white | The default look | |
| Dusk | Low, orange | Long shadows, lamps come on | Terrain heights and building silhouettes must stay readable |
| Night | Moon, cool blue | Lit windows, harbor lamps, torches | Never darker than the player can read the coastline |

## Water

- Colour from depth: turquoise over sand to navy over deep water.
- Shoreline foam that follows the coast when it changes.
- No visible tiling stripes or banding.

## Sound (later)

Surf, gulls, wind in palms, distant hammering near busy buildings.

## Open questions

- Stylised (Tropico) or closer to realistic (Anno 1800)? Default: between the two, leaning Anno.
