using System;
using System.Collections.Generic;
using IslandSim.Core;
using UnityEngine;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// The web game's camera rig, in tile space: a point on the map the camera looks at, a distance, a compass yaw and a
    /// tilt. Panning, zoom limits, the 45° turns and picking a tile under the pointer all work as in the browser.
    /// </summary>
    public sealed class RtsCamera
    {
        public readonly Camera Cam;
        public float Tx = W / 2f, Tz = H / 2f, Dist = 120, Yaw = -28 * Mathf.Deg2Rad, Pitch = 54 * Mathf.Deg2Rad, Spin;
        public float? Goal;

        public RtsCamera(Camera cam)
        {
            Cam = cam; cam.fieldOfView = 38; cam.nearClipPlane = .4f; cam.farClipPlane = 2000;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        public void Home(Sim s)
        {
            var A = Atolls[0]; var c = s?.CapitalHarbor; Goal = null;
            Tx = c != null ? (float)(c.X * .6 + A.X * .4) : (float)A.X; Tz = c != null ? (float)(c.Y * .6 + A.Y * .4) : (float)A.Y;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            Dist = Mathf.Min(680, 150 * Mathf.Max(1, 1.25f / aspect)); Yaw = -28 * Mathf.Deg2Rad; Pitch = (Screen.width < 760 ? 62 : 50) * Mathf.Deg2Rad;
        }

        public void Apply()
        {
            Pitch = Mathf.Clamp(Pitch, 18 * Mathf.Deg2Rad, 84 * Mathf.Deg2Rad); Dist = Mathf.Clamp(Dist, 6, 680);
            Tx = Mathf.Clamp(Tx, -10, W + 10); Tz = Mathf.Clamp(Tz, -10, H + 10);
            float cp = Mathf.Cos(Pitch);
            var p = U(Tx + Mathf.Sin(Yaw) * cp * Dist, Mathf.Sin(Pitch) * Dist, Tz + Mathf.Cos(Yaw) * cp * Dist);
            Cam.transform.position = p; Cam.transform.LookAt(U(Tx, 0, Tz), Vector3.up);
        }

        /// <summary>Drags the map by a screen-pixel offset, as the browser's left-drag does.</summary>
        public void PanBy(float dx, float dy)
        {
            float s = Dist * .0016f, c = Mathf.Cos(Yaw), sn = Mathf.Sin(Yaw);
            Tx -= (dx * c + dy * sn) * s; Tz -= (-dx * sn + dy * c) * s;
        }

        /// <summary>Turns toward the goal yaw (set by the north needle and the rotate buttons), or spins while a key is held.</summary>
        public void Turn(float dt, float keys)
        {
            if (keys != 0 || Spin != 0) { Goal = null; Yaw += 1.6f * dt * (keys + Spin); }
            if (Goal.HasValue) { float d = Goal.Value - Yaw; Yaw += d * Mathf.Min(1, dt * 9); if (Mathf.Abs(d) < .002f) { Yaw = Goal.Value; Goal = null; } }
        }

        public void JumpTo(int x, int y) { Tx = x + .5f; Tz = y + .5f; Dist = Mathf.Min(Dist, 80); }

        Ray RayAt(Vector2 screenTopLeft) => Cam.ScreenPointToRay(new Vector3(screenTopLeft.x, Screen.height - screenTopLeft.y, 0));

        /// <summary>The tile under a screen point (pixels from the top left): marches the view ray down to the ground as it is
        /// drawn (ground(x, z) in tile space, smooth coast and shelf included), then narrows the hit so the cursor lands on
        /// the spot under the mouse rather than on a tile's raised edge.</summary>
        public Vector2Int? Pick(Sim s, Vector2 screen, Func<float, float, float> ground)
        {
            var r = RayAt(screen); var o = T(r.origin); var d = T(r.direction); float prev = 1;
            for (float t = 1; t < 900; t += .25f)
            {
                float x = o.x + d.x * t, y = o.y + d.y * t, z = o.z + d.z * t;
                if (y <= ground(x, z))
                {
                    float lo = prev, hi = t;
                    for (int i = 0; i < 8; i++) { float m = (lo + hi) * .5f; if (o.y + d.y * m <= ground(o.x + d.x * m, o.z + d.z * m)) hi = m; else lo = m; }
                    int tx = Mathf.FloorToInt(o.x + d.x * hi), tz = Mathf.FloorToInt(o.z + d.z * hi);
                    return Sim.Inb(tx, tz) ? new Vector2Int(tx, tz) : (Vector2Int?)null;
                }
                if (y < -3) break;
                prev = t;
            }
            return null;
        }

        /// <summary>Where a viewport corner (-1..1) meets sea level, in tile space, for the minimap's view outline.</summary>
        public Vector2 GroundAt(float nx, float ny)
        {
            var r = Cam.ViewportPointToRay(new Vector3((nx + 1) / 2, (ny + 1) / 2, 0)); var o = T(r.origin); var d = T(r.direction);
            float t = d.y < -1e-3f ? -o.y / d.y : 400; t = Mathf.Min(t, 400);
            return new Vector2(o.x + d.x * t, o.z + d.z * t);
        }

        // the camera rides along in saves, as in the web game
        public void Store(Sim s)
        {
            s.Cam = new Dictionary<string, object> { ["tx"] = (double)Tx, ["tz"] = (double)Tz, ["dist"] = (double)Dist, ["yaw"] = (double)Yaw, ["pitch"] = (double)Pitch, ["goal"] = null, ["spin"] = 0.0 };
        }
        public void Load(Sim s)
        {
            if (s.Cam == null) return;
            float F(string k, float d) => s.Cam.TryGetValue(k, out var v) && v != null ? Convert.ToSingle(v) : d;
            Tx = F("tx", Tx); Tz = F("tz", Tz); Dist = F("dist", Dist); Yaw = F("yaw", Yaw); Pitch = F("pitch", Pitch); Goal = null; Spin = 0;
        }
    }
}
