using System.Collections.Generic;

namespace IslandSim.Core
{
    // Age 1, the Age of Cowries (c. 1200–1550): fish and coconuts feed the islands; rope, dried fish and cowries go to passing traders
    public enum Good { Wood, Stone, Fish, Coconut, Toddy, Hikimas, Rope, Cowrie, Rice }

    public enum BType { Harbor, House, Fishery, Drying, Grove, Toddy, Lumber, Quarry, Coir, Cowrie, Mosque, Palace }

    /// <summary>Ground types laid over the height map.</summary>
    public static class GT
    {
        public const byte None = 0, Kulhi = 1, Pond = 2, Taro = 3, Seagrass = 4, Finolhu = 5, Bird = 6;
        public static readonly string[] Name = { "", "Kulhi: mangrove wetland", "Brackish pond", "Taro field", "Seagrass meadow", "Finolhu: shifting sandbank", "Bird island (protected)" };
    }

    /// <summary>Tools the player can hold: Survey, the terraform tools, roads, Demolish, or a building (Build + a BType).</summary>
    public enum Tool { Inspect, Raise, Lower, Flatten, Smooth, Road, Demolish, Build, Plant }

    public sealed class Cost
    {
        public int Gold, Wood, Stone;
        public Cost(int gold, int wood = 0, int stone = 0) { Gold = gold; Wood = wood; Stone = stone; }
        public int Of(Good g) => g == Good.Wood ? Wood : g == Good.Stone ? Stone : 0;
    }

    public sealed class BuildingDef
    {
        public string Id, Name, Key = "", Info;
        public Cost Cost;
        public double Upkeep;
        public int MinH, MaxH, Jobs, Range;
        public int[] Size;          // multi-tile plots (w, d); null = one tile
        public int[][] Sizes;       // compounds: the plot sizes picked in the Homes cards
        public bool Unit;           // drawn as one model across its plot
    }

    public sealed class AtollSpec
    {
        public string Name; public double X, Y, R, Rot, Sx = 1, Sy = 1, Bend; public bool Lone; public int Faros, Seed;
        public double[][] Shape = new double[0][], Dents = new double[0][], Passes = new double[0][];
    }

    public sealed class IslandSpec
    {
        public string Name, Kind; public int At; public double Ang, Ext, Rw, Off, Hills; public bool Flats;
        public IslandSpec(string name, int at, string kind, double ang, double ext, double rw, double off, double hills, bool flats = false)
        { Name = name; At = at; Kind = kind; Ang = ang; Ext = ext; Rw = rw; Off = off; Hills = hills; Flats = flats; }
    }

    public static class Defs
    {
        public const int W = 720, H = 500, MinH = -3, MaxH = 3; // Maldives: islands sit about a metre above the sea
        public const int GoodCount = 9;

        // Four atolls and a lone island, each with its own outline (names are invented, Maldivian in flavour).
        // X,Y centre; R mean rim radius; Rot (deg) and Sx,Sy stretch the ring; Shape = [harmonic, amplitude, phase] wobbles;
        // Bend curves a long atoll; Dents pull the rim in (or push it out, if negative) around a bearing: [bearing, amount, width deg];
        // Passes = channels (bearing, half-width deg); Faros = ring reefs scattered in the lagoon.
        public static readonly AtollSpec[] Atolls =
        {
            new AtollSpec { Name = "Maarandu Atoll", X = 126, Y = 222, R = 70, Rot = -18, Shape = new[] { new[] { 2, .06, .7 }, new[] { 3, .12, 2.1 }, new[] { 5, .025, 4 } }, Passes = new[] { new[] { 150, 2.2 }, new[] { 28, 2.0 }, new[] { 275, 1.8 } }, Faros = 5, Seed = 1 },
            new AtollSpec { Name = "Thoravi Atoll", X = 292, Y = 82, R = 46, Rot = -10, Sx = 1.95, Shape = new[] { new[] { 3, .05, 1.2 }, new[] { 4, .03, 2.6 } }, Bend = .32, Dents = new[] { new[] { 90, .3, 16 }, new[] { 270, .3, 16 } }, Passes = new[] { new[] { 90, 5.0 }, new[] { 270, 5.0 }, new[] { 20, 1.6 }, new[] { 200, 1.6 }, new[] { 330, 1.4 } }, Faros = 16, Seed = 2 },
            new AtollSpec { Name = "Fenumaa Atoll", X = 366, Y = 300, R = 46, Rot = -12, Sx = 2.15, Shape = new[] { new[] { 3, .04, 1 }, new[] { 5, .02, 2 } }, Dents = new[] { new[] { 270, .3, 24 }, new[] { 90, -.14, 30 } }, Passes = new[] { new[] { 300, 2.0 }, new[] { 345, 1.6 }, new[] { 182, 2.0 }, new[] { 77, 1.6 } }, Faros = 5, Seed = 3 },
            new AtollSpec { Name = "Huvamaru", X = 264, Y = 204, R = 12, Rot = -62, Sx = 2.3, Lone = true, Seed = 4 },
            new AtollSpec { Name = "Oivaru Atoll", X = 486, Y = 118, R = 46, Rot = 34, Sx = 1.25, Shape = new[] { new[] { 2, .05, 1.9 }, new[] { 3, .08, .4 } }, Dents = new[] { new[] { 200, .36, 22 }, new[] { 20, -.12, 40 } }, Passes = new[] { new[] { 110, 2.0 }, new[] { 250, 1.8 }, new[] { 330, 1.6 } }, Faros = 8, Seed = 5 },
        };

        // Islands: atoll index, kind, bearing on the rim (deg), angular half-extent, radial half-width, inward offset from the rim
        // (large offsets put an islet in the lagoon), hills = threshold for low coral ridges (0 = none).
        // Kinds: rim, long, wooded (big, dense forest), kulhi (mangrove wetland and pond), vilu (ring round a pool),
        // finolhu (bare sandbank), bird (protected), lone (the single-island atoll).
        public static readonly IslandSpec[] Islands =
        {
            // home atoll: the first three keep their roles (capital, coral stone, coconuts behind lagoon flats)
            new IslandSpec("Palm Isle",       0, "rim",     214, 26, 10,  2.5, .72),
            new IslandSpec("Coral Isle",      0, "rim",     313, 15, 8.5, 1.5, .6),
            new IslandSpec("Kaashi Cay",      0, "rim",      92, 18, 6.5, 0,   0, flats: true),
            new IslandSpec("Dhigu Isle",      0, "long",     50, 17, 3.6, 0,   0),
            new IslandSpec("Raiveli Isle",    0, "kulhi",   264, 12, 9,   3,   0),
            new IslandSpec("Bodu Vilu",       0, "vilu",    350, 8,  7,   1,   0),
            new IslandSpec("Bambu Isle",      0, "wooded",  130, 13, 10,  3,   .7),
            new IslandSpec("Kandu Isle",      0, "rim",      16, 7,  5,   0,   0),
            new IslandSpec("Faru Islet",      0, "rim",     190, 5,  3.5, 34,  0),
            new IslandSpec("Gull Finolhu",    0, "finolhu", 300, 4,  3,   26,  0),
            new IslandSpec("Turtle Finolhu",  0, "finolhu", 110, 4,  2.6, 30,  0),
            new IslandSpec("Dhooni Islet",    0, "bird",    163, 4,  3.5, 0,   0),
            // north atoll: many small islands, two vilu, sandbanks, one kulhi; the story's hero comes from Thakuru Isle
            new IslandSpec("Thakuru Isle",    1, "rim",     205, 9,  6,   1,   0),
            new IslandSpec("Cowrie Isle",     1, "rim",     160, 8,  5,   0,   0),
            new IslandSpec("Coir Isle",       1, "rim",      22, 9,  5.5, 1,   0),
            new IslandSpec("Reed Isle",       1, "kulhi",   342, 10, 7,   2,   0),
            new IslandSpec("Twin Vilu",       1, "vilu",    235, 7,  6,   1,   0),
            new IslandSpec("Lonu Vilu",       1, "vilu",     58, 6,  5.5, 1,   0),
            new IslandSpec("Old Fort Isle",   1, "rim",     290, 8,  5.5, 1,   .62),
            new IslandSpec("Mas Isle",        1, "rim",     125, 7,  5,   0,   0),
            new IslandSpec("Sandpiper Cay",   1, "finolhu", 110, 3,  2.6, 0,   0),
            new IslandSpec("Heron Finolhu",   1, "finolhu", 250, 3,  2.4, 0,   0),
            new IslandSpec("Ufaa Finolhu",    1, "finolhu",   2, 3,  2.6, 20,  0),
            new IslandSpec("Kiru Isle",       1, "rim",     180, 4,  3.5, 24,  0),
            new IslandSpec("Saafu Isle",      1, "rim",     350, 4,  3.5, 26,  0),
            new IslandSpec("Noddy Islet",     1, "bird",     80, 4,  3,   0,   0),
            // south atoll: fewer, bigger islands close together on the rim; the long island takes an airfield one day
            new IslandSpec("Great Wood Isle", 2, "wooded",  145, 14, 10,  2.5, .68),
            new IslandSpec("Ironwood Isle",   2, "wooded",  238, 11, 8,   2,   0),
            new IslandSpec("Raskulhi Isle",   2, "kulhi",   194, 9,  8,   2,   0),
            new IslandSpec("Fenkulhi Isle",   2, "kulhi",    92, 11, 7,   1.5, 0),
            new IslandSpec("Dhiguvaa Isle",   2, "long",     38, 22, 3.4, 0,   0),
            new IslandSpec("Tern Islet",      2, "bird",     70, 4,  3,   0,   0),
            new IslandSpec("Swell Islet",     2, "rim",     320, 6,  3.5, 0,   .6),
            new IslandSpec("Gate Islet",      2, "rim",     292, 4,  3,   0,   0),
            new IslandSpec("Breadfruit Isle", 2, "rim",       0, 7,  6,   1,   0),
            new IslandSpec("Kalhuvaa Isle",   2, "long",    266, 10, 3.2, 0,   0),
            new IslandSpec("Ambu Vilu",       2, "vilu",    172, 6,  6,   1,   0),
            new IslandSpec("Saruvaa Isle",    2, "rim",     118, 7,  5,   1,   0),
            new IslandSpec("Keyo Isle",       2, "wooded",  214, 5,  7,   2,   0),
            new IslandSpec("Hiri Finolhu",    2, "finolhu",  60, 4,  2.8, 24,  0),
            new IslandSpec("Midhili Islet",   2, "rim",     200, 5,  4,   30,  0),
            // the lone island: no lagoon, two freshwater lakes
            new IslandSpec("Huvamaru",        3, "lone",      0, 0,  0,   0,   0),
            // east atoll: a lopsided kidney of mid-sized islands, a kulhi and a bird islet
            new IslandSpec("Faalu Isle",      4, "rim",     150, 12, 7,   1.5, 0),
            new IslandSpec("Rasgiri Isle",    4, "rim",     300, 10, 6.5, 1,   .62),
            new IslandSpec("Weaver Isle",     4, "wooded",   60, 12, 8,   2,   0),
            new IslandSpec("Ruh Kulhi Isle",  4, "kulhi",   235, 10, 7,   2,   0),
            new IslandSpec("Dhoshi Vilu",     4, "vilu",     15, 7,  6,   1,   0),
            new IslandSpec("Kanbili Isle",    4, "long",     95, 9,  3.2, 0,   0),
            new IslandSpec("Moonrise Isle",   4, "rim",     340, 6,  4.5, 0,   0),
            new IslandSpec("Fehi Finolhu",    4, "finolhu", 270, 3,  2.6, 0,   0),
            new IslandSpec("Booby Islet",     4, "bird",    185, 4,  3,   0,   0),
            new IslandSpec("Neru Islet",      4, "rim",     120, 4,  3.5, 26,  0),
        };

        // The tables above are in design units; the map is drawn SC tiles per unit, so islands come out bigger.
        public const double SC = 1.25;
        static Defs()
        {
            foreach (var A in Atolls) { A.X *= SC; A.Y *= SC; A.R *= SC; }
            foreach (var I in Islands) { I.Rw *= SC; I.Off *= SC; }
        }

        public static readonly Dictionary<string, double> KindDens = new Dictionary<string, double>
        { { "rim", .5 }, { "long", .55 }, { "wooded", .3 }, { "kulhi", .5 }, { "vilu", .5 }, { "finolhu", 2 }, { "bird", .28 }, { "lone", .42 } };

        public static readonly string[] GoodIds = { "wood", "stone", "fish", "coconut", "toddy", "hikimas", "rope", "cowrie", "rice" };
        public static readonly string[] GoodLabel = { "Timber", "Coral stone", "Fish", "Coconuts", "Sweet toddy", "Dried fish (hiki mas)", "Coir rope", "Cowries", "Rice" };

        public static readonly BuildingDef[] Buildings =
        {
            new BuildingDef { Id = "harbor",  Name = "Jetty", Key = "H", Cost = new Cost(300, 10), Upkeep = 0.5, MinH = 0, MaxH = 1, Info = "Island warehouse and dhoni berth. Needs a coastal tile; boats need deep water beside it. The capital's jetty is the trade post, where passing ships buy exports and sell rice." },
            new BuildingDef { Id = "house",   Name = "Compound", Key = "C", Cost = new Cost(60, 6), MinH = 0, MaxH = 2, Size = new[] { 3, 2 }, Sizes = new[] { new[] { 3, 2 }, new[] { 4, 3 } }, Info = "A goathi: a fishing family's walled plot with a thatched house. Families need fish or rice, then coconuts and a mosque within walking distance; sweet toddy raises their tax." },
            new BuildingDef { Id = "fishery", Name = "Fishing dhoni berth", Cost = new Cost(60, 5), Upkeep = 0.2, MinH = 0, MaxH = 1, Jobs = 2, Range = 2, Info = "Coast only. The catch grows with shallow reef tiles within 2, and with kulhi or seagrass nurseries within 3." },
            new BuildingDef { Id = "drying",  Name = "Fish drying racks", Cost = new Cost(80, 8), Upkeep = 0.2, MinH = 0, MaxH = 2, Jobs = 2, Info = "Boils, smokes and sun-dries fish into hiki mas, which keeps and sells well to traders. Burns timber as firewood." },
            new BuildingDef { Id = "grove",   Name = "Coconut grove", Cost = new Cost(50, 3), Upkeep = 0.15, MinH = 1, MaxH = 2, Jobs = 2, Range = 2, Info = "Lowland (height 1–2). +50% within 2 tiles of water. Families need coconuts, and the rope yard turns them into coir." },
            new BuildingDef { Id = "toddy",   Name = "Toddy tapper", Cost = new Cost(60, 4), Upkeep = 0.15, MinH = 0, MaxH = 3, Jobs = 1, Range = 3, Info = "Climbs palms for sweet toddy (raa) and palm sugar. Needs coconut palms within 3 tiles: wild palms count, and each coconut grove counts as six." },
            new BuildingDef { Id = "lumber",  Name = "Timber cutter", Cost = new Cost(50), Upkeep = 0.15, MinH = 1, MaxH = 3, Jobs = 2, Range = 2, Info = "Fells trees within 2 tiles for timber. Palms regrow slowly. Terraforming clears trees." },
            new BuildingDef { Id = "quarry",  Name = "Coral stone cutter", Cost = new Cost(80, 5), Upkeep = 0.3, MinH = 0, MaxH = 3, Jobs = 2, Range = 2, Info = "Cuts coral stone from the reef flat: more shallow reef tiles within 2 cut more, and height 2+ ridges add more." },
            new BuildingDef { Id = "coir",    Name = "Coir rope yard", Cost = new Cost(120, 10), Upkeep = 0.3, MinH = 0, MaxH = 1, Jobs = 3, Info = "Soaks coconut husks in the lagoon, then beats and twists the fibre into rope, the main export of the age. Needs water within 2 tiles." },
            new BuildingDef { Id = "cowrie",  Name = "Cowrie beach", Cost = new Cost(40, 2), Upkeep = 0.1, MinH = 0, MaxH = 0, Jobs = 2, Range = 2, Info = "Women gather cowrie shells in the shallows to sell to traders. Beach only; more shallow reef tiles within 2 gather more." },
            new BuildingDef { Id = "mosque",  Name = "Island mosque", Cost = new Cost(200, 10, 20), Upkeep = 0.3, MinH = 0, MaxH = 2, Size = new[] { 2, 2 }, Unit = true, Info = "A small coral-stone mosque. Compounds on the same island within 10 tiles have their faith need met." },
            new BuildingDef { Id = "palace",  Name = "Sultan's palace", Cost = new Cost(600, 30, 40), Upkeep = 1, MinH = 1, MaxH = 2, Size = new[] { 3, 3 }, Unit = true, Info = "The capital's main building, one per game, on the capital island. Shows what the age still needs and starts the advance." },
        };
        public static BuildingDef Def(BType t) => Buildings[(int)t];
        public static bool TryType(string id, out BType t)
        {
            for (int k = 0; k < Buildings.Length; k++) if (Buildings[k].Id == id) { t = (BType)k; return true; }
            t = BType.Harbor; return false;
        }

        // What passing traders pay for exports and charge for rice, in gold per unit.
        public static readonly (Good g, int price)[] Price = { (Good.Rope, 8), (Good.Hikimas, 6), (Good.Cowrie, 5), (Good.Coconut, 1), (Good.Fish, 1) };
        public const int RicePrice = 3, RiceKeep = 40, TradeEvery = 60;
        // Age 1 is complete when the capital meets this charter (Ages and Buildings doc).
        public const int Age1Fishers = 60, Age1Gold = 1500, Age1Rope = 40, Age1Stone = 30;
        public const double MosqueWalk = 10;
        // Economy: tax per fed resident per second, the road bonus, how far residents walk to work, and the debt that loses the game.
        public const double Tax = 0.08, RoadBonus = 1.25, Walk = 10, DebtLimit = -300;
        public static readonly Cost BigHouse = new Cost(110, 11);

        public static readonly (int h, string name)[] Levels = { (-3, "Deep"), (-2, "Channel"), (-1, "Reef"), (0, "Beach"), (1, "Land"), (2, "Ridge") };

        public static string TerrainName(int h) =>
            h <= -3 ? "Open ocean" : h == -2 ? "Deep water (boats sail)" : h == -1 ? "Shallow reef (blocks boats, feeds fish)" :
            h == 0 ? "Beach" : h == 1 ? "Lowland" : "Coral ridge";
    }
}
