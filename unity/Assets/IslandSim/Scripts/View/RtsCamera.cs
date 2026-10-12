using IslandSim.Core;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>Anno-style orbit camera: a target point on the water plane, yaw, pitch and distance.</summary>
    public sealed class RtsCamera
    {
        public readonly Camera Cam;
        public float Tx, Tz, Dist = 120, Yaw, Pitch;
        const float MinPitch = 22, MaxPitch = 84, MinDist = 10, MaxDist = 320;

        public RtsCamera(Camera cam) { Cam = cam; Home(); }

        public void Home()
        {
            var p = World.Pos((float)Defs.AcX - 2, (float)Defs.AcY - 6, 0);
            Tx = p.x; Tz = p.z;
            Dist = Mathf.Min(MaxDist, 132 * Mathf.Max(1, 1.25f / Mathf.Max(.3f, Cam.aspect)));
            Yaw = 208; Pitch = 54;
        }

        Vector3 Offset => new Vector3(Mathf.Sin(Yaw * Mathf.Deg2Rad), 0, Mathf.Cos(Yaw * Mathf.Deg2Rad));
        public Vector3 Forward => -Offset;                                   // from camera toward the target, on the ground
        public Vector3 Right { get { var f = Forward; return new Vector3(f.z, 0, -f.x); } }

        public void Pan(Vector3 worldDelta) { Tx += worldDelta.x; Tz += worldDelta.z; }

        public void Apply()
        {
            Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch); Dist = Mathf.Clamp(Dist, MinDist, MaxDist);
            Tx = Mathf.Clamp(Tx, -10, Defs.W + 10); Tz = Mathf.Clamp(Tz, -10, Defs.H + 10);
            float cp = Mathf.Cos(Pitch * Mathf.Deg2Rad);
            var target = new Vector3(Tx, 0, Tz);
            Cam.transform.position = target + Offset * cp * Dist + Vector3.up * Mathf.Sin(Pitch * Mathf.Deg2Rad) * Dist;
            Cam.transform.LookAt(target);
        }

        /// <summary>Point on the plane y = h under a screen position.</summary>
        public bool GroundPoint(Vector2 screen, float h, out Vector3 p)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, h, 0));
            if (plane.Raycast(ray, out var d)) { p = ray.GetPoint(d); return true; }
            p = default; return false;
        }
    }
}
