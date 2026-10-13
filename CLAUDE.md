# Island Sim: rules for Claude

Island Sim is an Anno/Tropico-style city-builder on one tropical atoll. Terraforming is the signature mechanic. The game is being moved from the browser prototype (`index.html`) to Unity 6 URP (`unity/`).

## Source of truth

- **Design:** `docs/design/` is the single source of truth for what the game is. Read `docs/design/README.md` first, then the doc for the area you touch. If a doc and the code disagree, the doc wins unless the code is newer and the doc says "TBD"; then update the doc in the same change.
- **Never guess.** If the design docs don't answer a question (a colour, a number, a rule), do not invent it silently. Add it to "Open questions" at the bottom of the relevant doc, pick the most conservative option, and say so in the PR or reply.
- **Rules:** `unity/Assets/IslandSim/Scripts/Core/` (pure C#, no `UnityEngine`). Every gameplay number lives in `Defs.cs` or `Sim.cs`, never in a View script.
- **Look:** `unity/Assets/IslandSim/Scripts/View/` reads `Sim` and its dirty flags. Views never change game state.

## How we work (the loop)

1. **Brief.** Changes start from the design docs. A new feature gets its section in `docs/design/MECHANICS.md` (or the right doc) before code.
2. **Greybox first.** Build it with primitives or the procedural models in `Models.cs`. Play it, fix it, repeat. Art comes after the greybox plays well.
3. **One mechanic layer at a time.** Follow the layer order in `docs/design/MECHANICS.md`. Don't start a layer until the one before it is playable and tested.
4. **Review.** Every change is checked by the `reviewer` agent (`.claude/agents/reviewer.md`) before it is called done. The reviewer only looks and approves or rejects; it never edits.
5. **Feedback with a reason.** Ahmed's feedback is logged in `docs/design/FEEDBACK.md` with its reason ("too dark at dusk", "cut the water stripes"). The reason becomes a rule in the matching design doc so it isn't undone later.

## Agents

Defined in `.claude/agents/`:

| Agent | Owns | Writes |
|---|---|---|
| `designer` | `docs/design/` | Design docs only |
| `modeler` | Blender assets, `unity/Assets/IslandSim/Art/Models/` | `.blend`, `.fbx`, prefabs |
| `texturer` | Materials, textures, shaders, `unity/Assets/IslandSim/Art/Materials/` | Materials, textures, shaders |
| `reviewer` | Nothing | Nothing. Reads, plays, screenshots, approves or rejects |

Gameplay code (`Scripts/Core`, `Scripts/View`) is written by the main session, not a subagent, so one place holds the whole picture.

## Unity and Blender (MCP)

Setup is in `docs/MCP_SETUP.md`. When the Unity MCP is connected:
- After script changes, wait for the compile to finish and read the console. Zero errors and zero new warnings before moving on.
- Run EditMode tests (`Tests/Editor`) after any change to `Scripts/Core`.
- Enter Play mode in `Assets/IslandSim/Scenes/Atoll.unity`, take a screenshot from the default camera and one at dusk, and compare against `docs/design/MOOD.md`.

When the Blender MCP is connected:
- Follow `docs/design/ASSETS.md` for scale, triangle budgets, pivots and naming.
- Render a three-quarter preview of each asset next to the scale figure before exporting.
- Export FBX to `unity/Assets/IslandSim/Art/Models/` with the settings in `ASSETS.md`.

## Code rules

- C# only in `unity/`. Keep `Scripts/Core` free of `UnityEngine` so it stays testable.
- Add or update an EditMode test for every rule change.
- Keep the web prototype (`index.html`) as the reference for map generation; don't break tile-for-tile parity in `Sim.Generate` without a design-doc change.
- Small PRs. One mechanic layer or one asset set per PR.
