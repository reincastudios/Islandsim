using IslandSim.Core;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>
    /// Shared view constants and the one coordinate rule. The web game works in "tile space": x = tile column, y = up,
    /// z = tile row, a right-handed frame. Unity is left-handed, so every point goes across as (x, y, -z), triangles flip
    /// their winding, and a transform M becomes F·M·F with F = diag(1, 1, -1). Models are built in tile space (Geo) and
    /// converted once, so the numbers in Models.cs read exactly like the web game's.
    /// </summary>
    public static class World
    {
        public const float LVL = 0.4f, DEPTH = 0.62f;   // world units per height level on land / under water
        public const float WATER_Y = 0.02f;
        /// <summary>The sea surface right now: WATER_Y plus the tide (set by WaterView.SetTide). Boats ride on it.</summary>
        public static float SeaY = WATER_Y;
        /// <summary>Height of the drawn ground at a point in tile space (TerrainView.SurfaceY), once the terrain is built.</summary>
        public static System.Func<float, float, float> Surface;
        /// <summary>Where an object standing at this spot sits: never above the drawn ground, so nothing floats over the
        /// gently sloping beach.</summary>
        public static float StandY(float x, float z, float y) => Surface != null ? Mathf.Min(y, Surface(x, z)) : y;
        public const int CH = 64;                        // terrain, plant and building chunks are 64×64 tiles
        public static readonly int CXN = (Defs.W + CH - 1) / CH, CYN = (Defs.H + CH - 1) / CH;

        /// <summary>A tile's surface height.</summary>
        // the lagoon floor (height -2 and below) sits a little deeper than the steps alone, for a richer turquoise (look only)
        public static float TopY(int h) => h >= 0 ? 0.26f + h * LVL : h * DEPTH - 0.12f - (h <= -2 ? LAGOON_EXTRA : 0);
        public const float LAGOON_EXTRA = 0.5f;

        /// <summary>A tile-space point in Unity space.</summary>
        public static Vector3 U(float x, float y, float z) => new Vector3(x, y, -z);
        public static Vector3 U(Vector3 p) => new Vector3(p.x, p.y, -p.z);
        /// <summary>A Unity point back in tile space (the same flip).</summary>
        public static Vector3 T(Vector3 p) => new Vector3(p.x, p.y, -p.z);

        static readonly Matrix4x4 F = Matrix4x4.Scale(new Vector3(1, 1, -1));
        /// <summary>A tile-space transform as a Unity transform.</summary>
        public static Matrix4x4 U(Matrix4x4 m) => F * m * F;

        // tile-space transforms, matching three.js (rotation matrices are the same numbers in either handedness)
        public static Matrix4x4 RotX(float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); var m = Matrix4x4.identity; m.m11 = c; m.m12 = -s; m.m21 = s; m.m22 = c; return m; }
        public static Matrix4x4 RotY(float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); var m = Matrix4x4.identity; m.m00 = c; m.m02 = s; m.m20 = -s; m.m22 = c; return m; }
        public static Matrix4x4 RotZ(float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); var m = Matrix4x4.identity; m.m00 = c; m.m01 = -s; m.m10 = s; m.m11 = c; return m; }
        public static Matrix4x4 Tr(float x, float y, float z) => Matrix4x4.Translate(new Vector3(x, y, z));
        public static Matrix4x4 Sc(float s) => Matrix4x4.Scale(new Vector3(s, s, s));
        public static Matrix4x4 Sc(float x, float y, float z) => Matrix4x4.Scale(new Vector3(x, y, z));
        /// <summary>three.js Object3D matrix with Euler order XYZ: T · Rx · Ry · Rz · S.</summary>
        public static Matrix4x4 TRS(Vector3 p, float rx, float ry, float rz, Vector3 s) => Matrix4x4.Translate(p) * RotX(rx) * RotY(ry) * RotZ(rz) * Matrix4x4.Scale(s);

        public static float Hash(int x, int y, int s) => (float)Noise.Hash2(x, y, s);

        /// <summary>An sRGB hex colour as a linear colour (three.js converts hex colours the same way).</summary>
        public static Color Lin(uint hex) => Srgb(hex).linear;
        public static Color Srgb(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1);
        public static Color Srgb(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        public static Color Lin(string hex) => Srgb(hex).linear;

        public static readonly Vector3 SunDir3 = new Vector3(-0.55f, 0.58f, -0.42f).normalized;   // tile space, toward the sun
        public static Vector3 SunDir => U(SunDir3);

        public static readonly int IdTime = Shader.PropertyToID("_IsTime");
    }
}
