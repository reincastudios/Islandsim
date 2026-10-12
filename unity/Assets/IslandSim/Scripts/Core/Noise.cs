using System;

namespace IslandSim.Core
{
    /// <summary>Deterministic hash, value noise and fbm. Bit-for-bit ports of the web prototype so the atoll matches.</summary>
    public static class Noise
    {
        public static double Hash2(long x, long y, long s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393L + y * 668265263L + s * 982451653L);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296.0;
            }
        }

        public static double VNoise(double x, double y, long s)
        {
            double xi = Math.Floor(x), yi = Math.Floor(y), xf = x - xi, yf = y - yi;
            double u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            long ix = (long)xi, iy = (long)yi;
            double a = Hash2(ix, iy, s), b = Hash2(ix + 1, iy, s), c = Hash2(ix, iy + 1, s), d = Hash2(ix + 1, iy + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static double Fbm(double x, double y, long s)
        {
            return VNoise(x, y, s) * .55 + VNoise(x * 2.1, y * 2.1, s + 7) * .3 + VNoise(x * 4.3, y * 4.3, s + 13) * .15;
        }

        /// <summary>Signed difference between two bearings in degrees, in [-180, 180).</summary>
        public static double AngDiff(double a, double b)
        {
            double m = (a - b) % 360.0;
            m = (m + 540.0) % 360.0;
            if (m < 0) m += 360.0;
            return m - 180.0;
        }

        public static double Rad(double d) => d * Math.PI / 180.0;
    }

    /// <summary>mulberry32, as in the web prototype.</summary>
    public sealed class Mulberry32
    {
        uint s;
        public Mulberry32(uint seed) { s = seed; }
        public double Next()
        {
            unchecked
            {
                s += 0x6D2B79F5u;
                uint t = s;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
