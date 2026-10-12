# UI

## Camera and controls

| Input | Action |
|---|---|
| Left-drag (Survey tool) or middle-drag | Pan |
| Right-drag | Rotate and tilt |
| Mouse wheel | Zoom |
| WASD / arrows, Shift for faster | Pan |
| Q / E | Turn |
| V R F T X | Survey, Raise, Lower, Level, Demolish |
| H, 1–6 | Harbor, House, Fishery, Lumber camp, Quarry, Sugar field, Distillery |
| B | Brush size 1×1, 3×3, 5×5 |
| Space | Pause |
| F5 / F9 | Save / load |

## HUD layout

- **Top bar:** gold and gold per second, population, and the stock of the island under the camera (wood, stone, fish, sugar, rum).
- **Left:** tool palette (terraform tools, then buildings), with cost and the height rule on hover.
- **Bottom right:** selected building panel: name, status line, rate, staffing.
- **Top right:** charter goals with a tick when met.
- **Toasts:** short messages bottom-centre, green for good, red for problems, fade after 4 s.

## Feedback to the player

- Placement ghost is green when valid and red when not, with the reason in one line ("Needs height 3+").
- Terraform brush shows the tiles it will change and the stone it will cost before you click.
- Blocked boats show a small icon over the boat and the reason in its tooltip.

## Tech

The HUD is IMGUI today for zero setup. Move to UI Toolkit once the layout above is confirmed.

## Open questions

- Minimap: yes or no?
- Font and icon style (see MOOD.md).
