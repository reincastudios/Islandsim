namespace IslandSim.Core
{
    public enum Good { Wood, Stone, Fish, Sugar, Rum }

    public enum BType { Harbor, House, Fishery, Lumber, Quarry, Plantation, Distillery }

    public enum TerraMode { Raise, Lower, Level }

    public sealed class BuildingDef
    {
        public string Name, Key, Info;
        public int Gold, Wood, Stone;
        public float Upkeep;
        public int MinH, MaxH, Jobs;

        public int Cost(Good g) => g == Good.Wood ? Wood : g == Good.Stone ? Stone : 0;

        public string CostText()
        {
            var s = Gold + "g";
            if (Wood > 0) s += " · " + Wood + " wood";
            if (Stone > 0) s += " · " + Stone + " stone";
            return s;
        }
    }

    public static class Defs
    {
        public const int W = 144, H = 144, MinH = -3, MaxH = 7;
        public const int GoodCount = 5;
        public static readonly string[] GoodNames = { "Wood", "Stone", "Fish", "Sugar", "Rum" };

        // Atoll centre (tile coords) and mean ring radius.
        public const double AcX = 73, AcY = 83, AcR = 44;
        public const double PassAng = 150;

        public sealed class IslandSpec
        {
            public string Name; public double Ang, Ext, Rw, Off, Peak;
            public IslandSpec(string n, double ang, double ext, double rw, double off, double peak) { Name = n; Ang = ang; Ext = ext; Rw = rw; Off = off; Peak = peak; }
        }

        // Islands on the atoll rim: bearing (deg), angular half-extent, radial half-width, inward offset, peak height.
        public static readonly IslandSpec[] Islands =
        {
            new IslandSpec("Palm Isle",   214, 44, 10,  2.5, 4.5),
            new IslandSpec("Basalt Isle", 313, 23, 8.5, 1.5, 7),
            new IslandSpec("Sugar Cay",    92, 30, 6.5, 0,   2.3),
        };

        public static readonly BuildingDef[] Buildings =
        {
            new BuildingDef { Name = "Harbor",      Key = "H", Gold = 300, Wood = 10,            Upkeep = .4f,  MinH = 0, MaxH = 1, Jobs = 0, Info = "Island warehouse and boat berth. Needs a coastal tile; boats need deep water beside it." },
            new BuildingDef { Name = "House",       Key = "1", Gold = 20,  Wood = 3,             Upkeep = 0,    MinH = 0, MaxH = 2, Jobs = 0, Info = "Up to 4 residents on fish, 8 with rum. Pays tax when fed." },
            new BuildingDef { Name = "Fishery",     Key = "2", Gold = 60,  Wood = 5,             Upkeep = .15f, MinH = 0, MaxH = 1, Jobs = 2, Info = "Coast only. Catch grows with shallow reef tiles within 2." },
            new BuildingDef { Name = "Lumber camp", Key = "3", Gold = 50,                        Upkeep = .1f,  MinH = 1, MaxH = 4, Jobs = 2, Info = "Cuts wood from trees within 2 tiles. Terraforming clears trees." },
            new BuildingDef { Name = "Quarry",      Key = "4", Gold = 80,  Wood = 5,             Upkeep = .25f, MinH = 3, MaxH = 6, Jobs = 2, Info = "Hills only (height 3+). Higher ground yields more stone." },
            new BuildingDef { Name = "Sugar field", Key = "5", Gold = 60,  Wood = 4,             Upkeep = .15f, MinH = 1, MaxH = 2, Jobs = 2, Info = "Lowland (height 1-2). +50% within 2 tiles of water." },
            new BuildingDef { Name = "Distillery",  Key = "6", Gold = 150, Wood = 8, Stone = 6,  Upkeep = .4f,  MinH = 0, MaxH = 3, Jobs = 2, Info = "Turns sugar into rum." },
        };

        public static BuildingDef Def(BType t) => Buildings[(int)t];

        public static string TerrainName(int h) =>
            h <= -3 ? "Open ocean" : h == -2 ? "Deep water (boats sail)" : h == -1 ? "Shallow reef (blocks boats, feeds fish)" :
            h == 0 ? "Beach" : h <= 2 ? "Lowland" : h <= 4 ? "Hills" : "Mountain";
    }
}
