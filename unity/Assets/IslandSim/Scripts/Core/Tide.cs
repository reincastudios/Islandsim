using System;

namespace IslandSim.Core
{
    /// <summary>
    /// The tide, from the environmental plan: two harmonic constituents, the moon's (12.42 h) and the sun's (12 h), whose
    /// beat makes spring and neap tides on its own. It is a pure function of the game clock (Sim.T), so it pauses with the
    /// game, keeps step at every speed and comes back exactly the same after loading a save: nothing extra is saved.
    /// </summary>
    public static class Tide
    {
        // One full tide (high to high) every HOURS_PER_SEC game-hours per sim second: 12.42 h in 120 s, four game days.
        public const double CYCLE_SECONDS = 120;
        const double HOURS_PER_SEC = 12.4206 / CYCLE_SECONDS;
        // In world units (1 tile = 1 m across; a beach stands 0.26 above the still sea, so a real metre-high tide would
        // drown every beach): spring range 0.32, neap range 0.16.
        public const double LUNAR_AMP = 0.12, SOLAR_AMP = 0.04;

        // Gameplay bands: above HIGH a laden dhoni clears the reef flats; below LOW the sandbanks (height -1) dry enough to
        // wade. Even a neap tide (range 0.16) passes both, so every tide gives a crossing window either way.
        public const double HIGH = 0.06, LOW = -0.06;
        public static bool IsHigh(double simSeconds) => Level(simSeconds) > HIGH;
        public static bool IsLow(double simSeconds) => Level(simSeconds) < LOW;

        public static double Hours(double simSeconds) => simSeconds * HOURS_PER_SEC;

        /// <summary>Sea level above the still sea (World.WATER_Y) at a moment of the game clock, in world units.</summary>
        public static double Level(double simSeconds, double phase = 0)
        {
            double h = Hours(simSeconds);
            return LUNAR_AMP * Math.Cos(2 * Math.PI * h / 12.4206 + phase) + SOLAR_AMP * Math.Cos(2 * Math.PI * h / 12.0 + phase);
        }

        /// <summary>Rising or falling right now, and the sim seconds until the next turn of the tide (high or low water).</summary>
        public static (bool rising, double secondsToTurn, double nextLevel) Next(double simSeconds, double phase = 0)
        {
            const double step = 0.5;
            double a = Level(simSeconds, phase), b = Level(simSeconds + step, phase); bool rising = b > a;
            for (double t = step; t < CYCLE_SECONDS; t += step)
            {
                double c = Level(simSeconds + t + step, phase);
                if ((c > b) != rising) return (rising, t, b);
                b = c;
            }
            return (rising, CYCLE_SECONDS / 2, b);
        }

        /// <summary>How much of the spring/neap cycle we're in: 1 at springs (biggest range), 0 at neaps.</summary>
        public static double Spring(double simSeconds)
        {
            double h = Hours(simSeconds), beat = 2 * Math.PI * h * (1 / 12.0 - 1 / 12.4206);
            return .5 + .5 * Math.Cos(beat);
        }
    }
}
