# Island Sim design brief

This folder is the single source of truth for what Island Sim is. Code and art follow it. When something here changes, the code follows in the same or next PR.

**One-line pitch:** build a thriving colony on a tropical atoll by reshaping the land itself: raise bridges across the reef, dredge channels for boats, flatten hills for farms.

| Doc | Covers |
|---|---|
| [WORLD.md](WORLD.md) | The atoll, its islands, terrain heights, water |
| [MECHANICS.md](MECHANICS.md) | Rules, numbers and the layer-by-layer build order |
| [UI.md](UI.md) | Camera, controls, HUD, panels, feedback to the player |
| [MOOD.md](MOOD.md) | Look, light, palette, day and night, sound |
| [ASSETS.md](ASSETS.md) | Asset list and Blender-to-Unity rules |
| [references/](references/) | Reference images, one slot per topic |
| [FEEDBACK.md](FEEDBACK.md) | Ahmed's feedback with reasons, and the rule each became |

## Pillars

1. **The land is the toolbox.** Every big decision is a terraforming decision. A puzzle you can solve with a shovel should be solvable with a shovel.
2. **Water shapes everything.** Deep water carries boats, reef feeds fish and blocks boats, beaches and lowlands farm, hills give stone.
3. **Readable at a glance.** From the default camera the player can tell height, water depth and what each building is doing.
4. **Warm and calm.** An afternoon in the tropics, not a war game. No fail state beyond going broke.

## Status

- Web prototype: `index.html` (playable, procedural art).
- Unity 6 URP port: `unity/` (rules ported, procedural greybox art, not yet opened in a real editor).
