using System.Collections.Generic;
using System.IO;
using IslandSim.Core;
using UnityEngine;
using static IslandSim.Core.Defs;
using static IslandSim.View.World;

namespace IslandSim.View
{
    /// <summary>
    /// A drone fly-through of one island for a trailer: high establishing orbit, a low run over the lagoon to the beach,
    /// a slow pass along the palms, then a climb that reveals the atoll. The game keeps running underneath (boats,
    /// waves, wind) on a fixed clock, so every frame is a frame of the video however long it takes to render. With
    /// record on, each frame is saved as a JPEG under Recordings/&lt;name&gt;/ next to the Assets folder.
    /// </summary>
    public sealed class Cinematic
    {
        public const int Fps = 30;
        public bool Active => cam != null;
        public string Folder { get; private set; }
        public int Frame { get; private set; }
        public float Duration => keys[keys.Count - 1].t;

        struct Key { public float t; public Vector3 pos, look; }
        readonly List<Key> keys = new List<Key>();
        Camera cam; float fov0, clock; bool record; Texture2D grab;

        /// <summary>Plans the flight: close shots round a focus spot (fx, fy: the island's jetty or its heart), the opening
        /// and the final climb round the whole island (centre cx, cy and reach r, all in tiles).</summary>
        public void Start(Camera c, Sim s, float fx, float fy, float cx, float cy, float r, string name, bool rec)
        {
            cam = c; fov0 = c.fieldOfView; c.fieldOfView = 42; clock = 0; Frame = 0; record = rec; keys.Clear();
            // come in from the side of the focus with the most water close by, so the low shots look across the lagoon
            // at the beach rather than into the trees
            float a0 = 0, best = -1;
            for (int k = 0; k < 24; k++)
            {
                float a = k * Mathf.PI / 12, wet = 0;
                for (float d = 5; d < 26; d += 1) { int x = (int)(fx + Mathf.Cos(a) * d), y = (int)(fy + Mathf.Sin(a) * d); if (Sim.Inb(x, y) && s.Hgt[Sim.Idx(x, y)] < 0) wet += d < 14 ? 2 : 1; }
                if (wet > best) { best = wet; a0 = a; }
            }
            var F = new Vector3(fx, 0, fy); var C = new Vector3(cx, 0, cy);
            Vector3 P(Vector3 o, float a, float d, float h) => o + new Vector3(Mathf.Cos(a0 + a) * d, h, Mathf.Sin(a0 + a) * d);
            void K(float t, Vector3 p, Vector3 look) => keys.Add(new Key { t = t, pos = p, look = look });
            K(0, P(F, -.4f, 70, 75), F);                                  // high and looking down on the jetty and the reef
            K(6, P(F, -.15f, 36, 24), F);                                 // gliding down towards it
            K(11, P(F, .1f, 17, 3.2f), F + Vector3.up * 2.0f);             // skimming the lagoon towards the beach
            K(16, P(F, .65f, 12, 2.6f), F + Vector3.up * 1.6f);            // sweeping along the shore, palms going by
            K(21, P(F, 1.2f, 14, 4.5f), F + Vector3.up * 1.2f);
            K(28, P(C, 1.5f, r * 2.6f + 30, r * 2.2f + 40), C);           // climbing away: the whole island in its atoll
            Folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Recordings", name);
            if (record) { if (Directory.Exists(Folder)) foreach (var f in Directory.GetFiles(Folder, "frame_*.jpg")) File.Delete(f); Directory.CreateDirectory(Folder); }
            Time.captureFramerate = record ? Fps : 0;
        }

        public void Stop() { if (cam != null) cam.fieldOfView = fov0; cam = null; Time.captureFramerate = 0; }

        /// <summary>Moves the camera to this frame's spot; false once the flight is over. ground gives the terrain height.</summary>
        public bool Step(System.Func<float, float, float> ground, out float time, out Vector3 look)
        {
            time = clock; look = Vector3.zero;
            if (cam == null) return false;
            if (clock > Duration) { Stop(); return false; }
            int i = 0; while (i < keys.Count - 2 && clock > keys[i + 1].t) i++;
            float u = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i].t, keys[i + 1].t, clock));
            // Catmull-Rom through the keys, eased within each leg so the drone glides rather than lurches
            Key k0 = keys[Mathf.Max(0, i - 1)], k1 = keys[i], k2 = keys[i + 1], k3 = keys[Mathf.Min(keys.Count - 1, i + 2)];
            var p = CR(k0.pos, k1.pos, k2.pos, k3.pos, u);
            look = CR(k0.look, k1.look, k2.look, k3.look, u);
            float g = ground(p.x, p.z); p.y = Mathf.Max(p.y, g + (g > WATER_Y + .05f ? 5f : 1.4f));   // never under the sea, and over land above the palms
            cam.transform.position = U(p.x, p.y, p.z); cam.transform.LookAt(U(look.x, look.y, look.z), Vector3.up);
            clock += 1f / Fps;
            return true;
        }

        static Vector3 CR(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        }

        /// <summary>Saves the frame just rendered (call after the frame has finished rendering).</summary>
        public void Capture()
        {
            if (!record || Folder == null) return;
            // read the finished frame straight off the back buffer (the Screen Capture module isn't in this project)
            int w = Screen.width, h = Screen.height;
            if (grab == null || grab.width != w || grab.height != h) { if (grab != null) Object.Destroy(grab); grab = new Texture2D(w, h, TextureFormat.RGB24, false); }
            grab.ReadPixels(new Rect(0, 0, w, h), 0, 0, false); grab.Apply(false);
            File.WriteAllBytes(Path.Combine(Folder, $"frame_{Frame:D4}.jpg"), grab.EncodeToJPG(92));
            Frame++;
        }
    }
}
