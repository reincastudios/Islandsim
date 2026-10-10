# Connecting Claude Code to Unity and Blender (MCP)

Both run on your own computer, next to the Unity Editor and Blender. Do this once.

## 0. Prerequisites

- **Claude Code** installed (`claude --version` works in a terminal).
- **uv** (runs the MCP servers). Use the official installer, not `pip install uv`:
  - macOS: `brew install uv`
  - Windows (PowerShell): `powershell -c "irm https://astral.sh/uv/install.ps1 | iex"`
  - Linux: `curl -LsSf https://astral.sh/uv/install.sh | sh`
- **git** (Unity uses it to fetch the MCP package).
- **Unity 6** (6000.0 LTS or newer) through Unity Hub, and **Blender** 3.0 or newer (4.x recommended).
- This repo cloned, with the `unity/` folder added in Unity Hub (see `unity/README.md`).

## 1. Unity: MCP for Unity (CoplayDev, free, MIT)

1. Open the `unity/` project in Unity 6.
2. **Window > Package Manager > + > Add package from git URL**, paste:
   `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0`
3. **Window > MCP for Unity > Configure All Detected Clients**. This adds a `unity` entry to Claude Code.
4. Keep the Unity Editor open while you work; the bridge runs inside it.

## 2. Blender: MCP for Blender (ahujasid, free, MIT)

1. Close Blender, then in a terminal run:
   `uvx mcp-for-blender setup`
   It finds Claude Code, adds a `blender` entry, and installs and enables the Blender add-on.
   If it doesn't work, do it by hand:
   - `claude mcp add blender uvx mcp-for-blender`
   - `uvx mcp-for-blender install-addon`, then in Blender **Edit > Preferences > Add-ons**, enable **Interface: MCP for Blender**.
2. Open Blender. In the 3D viewport press **N**, open the **MCP for Blender** tab, and click **Start MCP Server** if it isn't running.

## 3. Check it

From the repo root:

```
claude mcp list
```

You should see `unity` and `blender` as connected. Then start `claude` in the repo and ask: "Use Unity to read the console, and use Blender to add a cube and render a preview."

## Notes

- Run only one client against the Blender server at a time.
- Both bridges can run arbitrary editor code. Only use them on projects you trust, and commit before big automated changes.
