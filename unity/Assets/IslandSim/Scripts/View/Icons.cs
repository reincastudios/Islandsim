using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IslandSim.View
{
    /// <summary>
    /// The web game's in-house icon set (filled silhouettes on a 24-unit grid, even-odd fill), drawn from the same SVG path
    /// data into small textures once, so the HUD uses exactly the same shapes. Icons are white and tinted by the stylesheet.
    /// </summary>
    public static class Icons
    {
        public static readonly Dictionary<string, string> Path = new Dictionary<string, string>
        {
            ["gold"] = "M12 2.5a9.5 9.5 0 1 0 0 19 9.5 9.5 0 0 0 0-19zM9.6 9.6h4.8v4.8H9.6z",
            ["wood"] = "M3 6.5h13.5a2.75 2.75 0 0 1 0 5.5H3zM17 7.9a1.35 1.35 0 1 0 0 2.7 1.35 1.35 0 0 0 0-2.7zM5 13h13.5a2.75 2.75 0 0 1 0 5.5H5zM19 14.4a1.35 1.35 0 1 0 0 2.7 1.35 1.35 0 0 0 0-2.7z",
            ["stone"] = "M4 8.5L8.5 4h7L20 8.5v7L15.5 20h-7L4 15.5zM8.5 8.5h2.2v2.2H8.5zM13 11.5h2.4v2.4H13zM9.5 14h1.8v1.8H9.5z",
            ["fish"] = "M1.5 12C4.5 7 10.5 6 15 9.2L21 6v12l-6-3.2C10.5 18 4.5 17 1.5 12zM7 10.8a1.2 1.2 0 1 0 0 2.4 1.2 1.2 0 0 0 0-2.4z",
            ["coconut"] = "M12 3a8.5 9 0 1 0 0 18 8.5 9 0 0 0 0-18zM9.2 8a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8zM14.8 8a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8zM12 11.6a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z",
            ["toddy"] = "M9 2.5h6v2.5c3.5 1.5 5 4.5 5 8 0 5-3.6 8.5-8 8.5S4 18 4 13c0-3.5 1.5-6.5 5-8zM7 11h10v1.6H7z",
            ["hikimas"] = "M1.5 12C4.5 7 10.5 6 15 9.2L21 6v12l-6-3.2C10.5 18 4.5 17 1.5 12zM8 7.8h1.6v8.4H8zM11.5 7.6h1.6v8.8h-1.6z",
            ["rope"] = "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 5.5a6.5 6.5 0 1 1 0 13 6.5 6.5 0 0 1 0-13zM12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM12 10.3a1.7 1.7 0 1 1 0 3.4 1.7 1.7 0 0 1 0-3.4z",
            ["cowrie"] = "M12 2.5c4.4 0 7 4.3 7 9.5s-2.6 9.5-7 9.5-7-4.3-7-9.5 2.6-9.5 7-9.5zM11.2 5h1.6v14h-1.6zM8.5 8h2.2v1.2H8.5zM13.3 8h2.2v1.2h-2.2zM8.2 11.4h2.5v1.2H8.2zM13.3 11.4h2.5v1.2h-2.5zM8.5 14.8h2.2v1.2H8.5zM13.3 14.8h2.2v1.2h-2.2z",
            ["rice"] = "M8 2.5h8l-1.5 3c4 1.5 6 5 6 9.5 0 4-2 6.5-8.5 6.5S3.5 19 3.5 15c0-4.5 2-8 6-9.5zM7.5 9.5h9V11h-9z",
            ["palm"] = "M11 9h2l1 12.5h-4zM11.6 8.6C9 5.4 5.4 5 2 7c3.2-.4 6.2.2 9 2.4zM12.4 8.6c2.6-3.2 6.2-3.6 9.6-1.6-3.2-.4-6.2.2-9 2.4zM11.2 8C10.4 4.6 8 3 5 2.5c2.8 1.6 4.6 3.6 5.6 6zM12.8 8c.8-3.4 3.2-5 6.2-5.5-2.8 1.6-4.6 3.6-5.6 6z",
            ["racks"] = "M3 4h2v17H3zM19 4h2v17h-2zM5 5h14v1.6H5zM5 12h14v1.6H5zM7.5 6.6H9v4H7.5zM11.25 6.6h1.5v4h-1.5zM15 6.6h1.5v4H15zM7.5 13.6H9v4H7.5zM11.25 13.6h1.5v4h-1.5zM15 13.6h1.5v4H15z",
            ["mosque"] = "M11.2 1.5h1.6v2.6h-1.6zM12 4.2l9 6H3zM4.5 11.2h15v10.3h-15zM10 14.5h4v7h-4z",
            ["palace"] = "M2 21.5V9l3 2V6l3 2V4l4 3 4-3v4l3-2v5l3-2v12.5zM10 15h4v6.5h-4z",
            ["trade"] = "M2 15h20l-3 5.5H5zM6 3.5h1.6v11H6zM13 3.5h1.6v11H13zM7.6 4.5l4.4 8H7.6zM14.6 4.5l5.4 8h-5.4z",
            ["people"] = "M9 3.5a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7zM2 20.5c0-4 3.1-7.5 7-7.5s7 3.5 7 7.5zM17 5a3 3 0 1 1 0 6 3 3 0 0 1 0-6zM17.5 12.5c2.6.3 4.5 3.4 4.5 7h-4.5c0-2.6-.8-5-2.2-6.6.7-.3 1.4-.4 2.2-.4z",
            ["sun"] = "M12 7a5 5 0 1 1 0 10 5 5 0 0 1 0-10zM11 .5h2v4h-2zM11 19.5h2v4h-2zM.5 11h4v2h-4zM19.5 11h4v2h-4zM3.5 5L5 3.5l2.8 2.8-1.5 1.5zM16.2 17.7l1.5-1.5 2.8 2.8-1.5 1.5zM3.5 19l2.8-2.8 1.5 1.5L5 20.5zM16.2 6.3L19 3.5l1.5 1.5-2.8 2.8z",
            ["menu"] = "M3 5h18v2.4H3zM3 10.8h18v2.4H3zM3 16.6h18V19H3z",
            ["bell"] = "M12 2a6 6 0 0 1 6 6v5l2 3v1.5H4V16l2-3V8a6 6 0 0 1 6-6zM9.5 19h5a2.5 2.5 0 0 1-5 0z",
            ["terra"] = "M11 2h2v8h-2zM7.5 9.5h9v4.5a4.5 4.5 0 0 1-9 0zM1 20.5c2-1.8 4-1.8 5.5 0s3.5 1.8 5.5 0 3.5-1.8 5.5 0 3.5 1.8 5.5 0V23c-2 1.3-3.8 1-5.5-.5-1.7 1.5-3.8 1.5-5.5 0-1.7 1.5-3.8 1.5-5.5 0C4.8 24 3 24.3 1 23z",
            ["homes"] = "M1.5 21.5v-10l3-2v12zM22.5 21.5v-10l-3-2v12zM6 21.5V12l6-5.5 6 5.5v9.5h-4v-5h-4v5z",
            ["axe"] = "M13.5 2.5c3.5.5 6.5 3 7.5 6.5l-4.5 1.5-1.2-1.2L5.5 21 3 18.5 14.8 8.7l-1.3-1.3z",
            ["harbour"] = "M1.5 14.5h21l-3.2 5.5H5zM11 2.5v11H4.5zM13 4.5l7 9h-7z",
            ["road"] = "M9 2h2.2L9 22H4.5zM12.8 2H15l4.5 20H15zM11.3 5.5h1.4v3.5h-1.4zM11.2 12h1.6v4.5h-1.6z",
            ["raise"] = "M12 2.5l6.5 7.5H14v6h-4v-6H5.5zM2 19.5h20V22H2z",
            ["lower"] = "M12 17L5.5 9.5H10v-6h4v6h4.5zM2 19.5h20V22H2z",
            ["level"] = "M2 16h20v6H2zM2 10.5h20v2.5H2zM6 5.5h12V8H6z",
            ["smooth"] = "M1.5 17c3-6 6-9 10.5-9s7.5 3 10.5 9v5H1.5zM6.5 3.5h11V6h-11z",
            ["demolish"] = "M4.5 2.5L21.5 19.5 19.5 21.5 2.5 4.5zM19.5 2.5l2 2-17 17-2-2z",
            ["survey"] = "M10 2.5a7.5 7.5 0 1 1 0 15 7.5 7.5 0 0 1 0-15zM10 5a5 5 0 1 0 0 10 5 5 0 0 0 0-10zM15.2 17l1.8-1.8 5 5-1.8 1.8z",
            ["pause"] = "M6 4h4v16H6zM14 4h4v16h-4z",
            ["play"] = "M7 4l13 8-13 8z",
            ["warn"] = "M12 2L23 21.5H1zM11 9h2v6.5h-2zM11 17h2v2h-2z",
            ["edit"] = "M15.5 3l5.5 5.5L9 20.5H3.5V15zM13.8 6.8l3.4 3.4",
            ["pin"] = "M12 2a7 7 0 0 1 7 7c0 5-7 13-7 13S5 14 5 9a7 7 0 0 1 7-7zM12 6.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z",
            ["map"] = "M2 5.5L8 3l8 3 6-2.5v15L16 21l-8-3-6 2.5zM9 5.6v10.8l6 2.2V7.8z",
            ["gear"] = Gear(),
            // glyphs the HUD fonts lack, drawn as icons instead
            ["star"] = Star(),
            ["cross"] = "M5.5 4L12 10.5 18.5 4 20 5.5 13.5 12 20 18.5 18.5 20 12 13.5 5.5 20 4 18.5 10.5 12 4 5.5z",
            ["chevd"] = "M5 8.5h14L12 16z",
            ["chevu"] = "M5 15.5h14L12 8z",
            ["rotl"] = "M12 5A8 8 0 1 1 5.07 9L7.15 10.2A5.6 5.6 0 1 0 12 7.4zM12 2.2L12 10.2 7 6.2z",
            ["rotr"] = "M12 5A8 8 0 1 0 18.93 9L16.85 10.2A5.6 5.6 0 1 1 12 7.4zM12 2.2L12 10.2 17 6.2z",
            ["home"] = "M12 3l9 8v10H3V11zM12 5.7L5 12v7h14v-7z",
        };

        static string Gear()
        {
            var sb = new System.Text.StringBuilder(); const int n = 8; double st = Math.PI * 2 / n;
            for (int k = 0; k < n; k++)
            {
                double c = k * st;
                foreach (var (r, a) in new[] { (8.4, c - .24), (11.0, c - .15), (11.0, c + .15), (8.4, c + .24) })
                    sb.Append(sb.Length == 0 ? "M" : "L").Append((12 + r * Math.Sin(a)).ToString("0.00", CultureInfo.InvariantCulture)).Append(' ').Append((12 - r * Math.Cos(a)).ToString("0.00", CultureInfo.InvariantCulture));
            }
            return sb + "zM15.5 12a3.5 3.5 0 1 0-7 0 3.5 3.5 0 0 0 7 0z";
        }

        static string Star()
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI / 5, r = k % 2 == 0 ? 10 : 4.2;
                sb.Append(k == 0 ? "M" : "L").Append((12 + r * Math.Sin(a)).ToString("0.00", CultureInfo.InvariantCulture)).Append(' ').Append((12.6 - r * Math.Cos(a)).ToString("0.00", CultureInfo.InvariantCulture));
            }
            return sb + "z";
        }

        const int PX = 96;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>A white icon (tinted in the stylesheet).</summary>
        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var t)) return t;
            if (name == "needle") t = Draw(new[] { ("M12 1.5l4 10.5H8z", Hex(0xe0533f)), ("M12 22.5L8 12h8z", Hex(0xf2ece0)) }, 24, 24);
            else if (name == "portrait") t = Portrait();
            else t = Draw(new[] { (Path.TryGetValue(name, out var d) ? d : "", Color.white) }, 24, 24);
            t.name = "icon " + name;
            return cache[name] = t;
        }
        static Color Hex(uint h) => World.Srgb(h);

        // the katheeb, the island chief who reports in the advisor feed
        static Texture2D Portrait()
        {
            string Circle(float cx, float cy, float r) => FormattableString.Invariant($"M{cx - r} {cy}a{r} {r} 0 1 0 {2 * r} 0a{r} {r} 0 1 0 {-2 * r} 0z");
            var t = Draw(new[]
            {
                ("M0 0h40v40H0z", Hex(0xd9a441)), ("M5 40c1-9 7-13 15-13s14 4 15 13z", Hex(0xf2ece0)), ("M17 27h6l-3 5z", Hex(0xa3362a)),
                (Circle(20, 18, 8), Hex(0x8a5a3a)), ("M11.5 14.5c0-7 17-7 17 0z", Hex(0xf2ece0)), ("M11.5 14h17v1.8h-17z", Hex(0xa3362a)),
                (Circle(17, 18.5f, 1), Hex(0x2a1a10)), (Circle(23, 18.5f, 1), Hex(0x2a1a10)),
            }, 40, 40, 80);
            Stroke(t, "M16.5 22.5c2 1.6 5 1.6 7 0", 40, 1.2f, Hex(0x3a2416));
            t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------ path parsing
        static List<List<Vector2>> Parse(string d)
        {
            var subs = new List<List<Vector2>>(); List<Vector2> cur = null;
            int i = 0; char cmd = 'M'; Vector2 p = Vector2.zero, start = Vector2.zero, lastC = Vector2.zero; char prev = ' ';
            float Num()
            {
                while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++;
                int s = i; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                bool dot = false;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot))) { if (d[i] == '.') dot = true; i++; }
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) { i++; if (d[i] == '-' || d[i] == '+') i++; while (i < d.Length && char.IsDigit(d[i])) i++; }
                return float.Parse(d.Substring(s, i - s), CultureInfo.InvariantCulture);
            }
            bool Flag() { while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++; return d[i++] == '1'; }
            bool MoreNums() { while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++; return i < d.Length && (char.IsDigit(d[i]) || d[i] == '-' || d[i] == '.' || d[i] == '+'); }
            void To(Vector2 q) { cur.Add(q); p = q; }
            void Cubic(Vector2 c1, Vector2 c2, Vector2 e) { var a = p; for (int k = 1; k <= 12; k++) { float t = k / 12f, u = 1 - t; To(u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * e); } lastC = c2; }
            while (i < d.Length)
            {
                if (char.IsLetter(d[i]) && d[i] != 'e' && d[i] != 'E') cmd = d[i++];
                else if (!MoreNums()) { i++; continue; }
                bool rel = char.IsLower(cmd); var o = rel ? p : Vector2.zero; char C = char.ToUpperInvariant(cmd);
                switch (C)
                {
                    case 'M': p = o + new Vector2(Num(), Num()); start = p; cur = new List<Vector2> { p }; subs.Add(cur); cmd = rel ? 'l' : 'L'; break;
                    case 'L': To(o + new Vector2(Num(), Num())); break;
                    case 'H': To(new Vector2((rel ? p.x : 0) + Num(), p.y)); break;
                    case 'V': To(new Vector2(p.x, (rel ? p.y : 0) + Num())); break;
                    case 'C': { var c1 = o + new Vector2(Num(), Num()); var c2 = o + new Vector2(Num(), Num()); var e = o + new Vector2(Num(), Num()); Cubic(c1, c2, e); break; }
                    case 'S':
                        {
                            var c1 = (prev == 'C' || prev == 'S') ? 2 * p - lastC : p;
                            var c2 = o + new Vector2(Num(), Num()); var e = o + new Vector2(Num(), Num()); Cubic(c1, c2, e); break;
                        }
                    case 'A': { float rx = Num(), ry = Num(), rot = Num(); bool la = Flag(), sw = Flag(); var e = o + new Vector2(Num(), Num()); Arc(p, e, rx, ry, rot, la, sw, To); break; }
                    case 'Z': p = start; if (cur != null) cur = new List<Vector2> { p }; subs.Add(cur); break;
                }
                prev = C;
            }
            return subs;
        }

        // SVG endpoint arc to points (the SVG spec's centre parameterisation)
        static void Arc(Vector2 a, Vector2 b, float rx, float ry, float rotDeg, bool large, bool sweep, Action<Vector2> to)
        {
            if (rx == 0 || ry == 0) { to(b); return; }
            float phi = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            float dx = (a.x - b.x) / 2, dy = (a.y - b.y) / 2, x1 = cs * dx + sn * dy, y1 = -sn * dx + cs * dy;
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            float lam = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry); if (lam > 1) { float s = Mathf.Sqrt(lam); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1, den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float co = Mathf.Sqrt(Mathf.Max(0, num / den)) * (large == sweep ? -1 : 1);
            float cxp = co * rx * y1 / ry, cyp = -co * ry * x1 / rx;
            float cx = cs * cxp - sn * cyp + (a.x + b.x) / 2, cy = sn * cxp + cs * cyp + (a.y + b.y) / 2;
            float Ang(float ux, float uy, float vx, float vy) { float g = Mathf.Atan2(ux * vy - uy * vx, ux * vx + uy * vy); return g; }
            float t1 = Ang(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry), dt = Ang((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!sweep && dt > 0) dt -= 2 * Mathf.PI; else if (sweep && dt < 0) dt += 2 * Mathf.PI;
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(dt) / (Mathf.PI / 16)));
            for (int k = 1; k <= n; k++)
            {
                float t = t1 + dt * k / n, ex = rx * Mathf.Cos(t), ey = ry * Mathf.Sin(t);
                to(k == n ? b : new Vector2(cs * ex - sn * ey + cx, sn * ex + cs * ey + cy));
            }
        }

        // ------------------------------------------------------------ filling (even-odd, 4×4 samples per pixel)
        static Texture2D Draw((string d, Color c)[] layers, float vw, float vh, int px = PX)
        {
            int w = px, h = Mathf.RoundToInt(px * vh / vw);
            var buf = new Color[w * h];
            const int SS = 4; var xs = new List<float>();
            foreach (var (d, col) in layers)
            {
                var subs = Parse(d);
                var cov = new float[w * h];
                for (int y = 0; y < h; y++)
                    for (int sy = 0; sy < SS; sy++)
                    {
                        float vy = (y + (sy + .5f) / SS) * vh / h; xs.Clear();
                        foreach (var s in subs)
                            for (int k = 0; k + 1 < s.Count + 1 && s.Count > 1; k++)
                            {
                                var p0 = s[k]; var p1 = s[(k + 1) % s.Count];
                                if ((p0.y <= vy) != (p1.y <= vy)) xs.Add(p0.x + (vy - p0.y) / (p1.y - p0.y) * (p1.x - p0.x));
                            }
                        xs.Sort();
                        for (int k = 0; k + 1 < xs.Count; k += 2)
                        {
                            float a = xs[k] * w / vw, b = xs[k + 1] * w / vw;
                            for (int x = Mathf.Max(0, Mathf.FloorToInt(a)); x < Mathf.Min(w, Mathf.CeilToInt(b)); x++)
                                for (int sx = 0; sx < SS; sx++) { float fx = x + (sx + .5f) / SS; if (fx >= a && fx < b) cov[(h - 1 - y) * w + x] += 1f / (SS * SS); }
                        }
                    }
                for (int k = 0; k < buf.Length; k++) { float a = Mathf.Clamp01(cov[k]); if (a > 0) buf[k] = Over(buf[k], col, a); }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 1 };
            t.SetPixels(buf); t.Apply(true);
            return t;
        }

        static Color Over(Color dst, Color src, float a)
        {
            float oa = a + dst.a * (1 - a); if (oa <= 0) return Color.clear;
            var c = (src * a + dst * dst.a * (1 - a)) / oa; c.a = oa; return c;
        }

        // a round-capped line along a path (the portrait's smile)
        static void Stroke(Texture2D t, string d, float vw, float width, Color col)
        {
            var pts = Parse(d)[0]; int w = t.width, h = t.height; float s = w / vw, r = width * s / 2;
            var px = t.GetPixels(0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var q = new Vector2(x + .5f, h - 1 - y + .5f) / s; float best = 1e9f;
                    for (int k = 0; k + 1 < pts.Count; k++) best = Mathf.Min(best, SegDist(q, pts[k], pts[k + 1]));
                    float a = Mathf.Clamp01(r - best * s + .5f); if (a > 0) px[y * w + x] = Over(px[y * w + x], col, a);
                }
            t.SetPixels(px, 0);
        }
        static float SegDist(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return Vector2.Distance(p, a + ab * t); }
    }
}
