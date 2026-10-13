using System.Linq;
using IslandSim.Core;
using NUnit.Framework;

namespace IslandSim.Tests
{
    /// <summary>Rules-level checks that run without a scene (Window > General > Test Runner > EditMode).</summary>
    public class SimTests
    {
        static Sim NewSim(int seed = 1)
        {
            var s = new Sim(); var rng = new Mulberry32((uint)seed); s.Random = rng.Next; double now = 0; s.Now = () => now += .001;
            s.NewGame(); return s;
        }

        [Test]
        public void NewGameFoundsTheCapitalOnPalmIsle()
        {
            var s = NewSim();
            var cap = s.CapitalHarbor;
            Assert.NotNull(cap);
            Assert.IsTrue(s.BerthOf(cap.X, cap.Y).HasValue, "the capital jetty needs a berth beside it");
            Assert.AreEqual("Palm Isle", s.CapitalIsland.Name);
            Assert.AreEqual(3, s.Buildings.Count(b => b.Type == BType.House), "three starter compounds");
            Assert.IsTrue(s.Buildings.Any(b => b.Type == BType.Fishery) && s.Buildings.Any(b => b.Type == BType.Lumber));
            Assert.AreEqual(800, s.Gold);
        }

        [Test]
        public void MapIsTheWebGamesSize() { var s = NewSim(); Assert.AreEqual(720 * 500, s.Hgt.Length); Assert.AreEqual(Defs.W * Defs.H, s.Gt.Length); }

        [Test]
        public void StarterVillageGrows()
        {
            var s = NewSim();
            double before = s.TotalPop;
            for (int k = 0; k < 600; k++) s.Step(.1);
            Assert.Greater(s.TotalPop, before);
        }

        [Test]
        public void RaisingLandCostsGoldAndStone()
        {
            var s = NewSim(); var st = s.CapitalHarbor.Stock; double stone = st[(int)Good.Stone], gold = s.Gold;
            int tx = -1, ty = -1;
            for (int y = 0; y < Defs.H && tx < 0; y++)
                for (int x = 0; x < Defs.W; x++)
                {
                    int i = Sim.Idx(x, y);
                    if (s.Hgt[i] == 1 && s.Bld[i] < 0 && s.Gt[i] == GT.None && s.IslandOf(x, y) == s.CapitalIsland) { tx = x; ty = y; break; }
                }
            Assert.GreaterOrEqual(tx, 0);
            s.Tool = Tool.Raise; s.BrushSize = 1;
            s.BeginStroke(tx, ty); s.EndStroke();
            Assert.AreEqual(2, s.Hgt[Sim.Idx(tx, ty)]);
            Assert.AreEqual(0, s.Tree[Sim.Idx(tx, ty)]);
            Assert.AreEqual(gold - 4, s.Gold, 1e-9);
            Assert.AreEqual(stone - 1, st[(int)Good.Stone], 1e-9);
            s.UndoTerraform();
            Assert.AreEqual(1, s.Hgt[Sim.Idx(tx, ty)]);
            Assert.AreEqual(gold, s.Gold, 1e-9);
        }

        [Test]
        public void TheOpenOceanCannotBeBuiltOut()
        {
            var s = NewSim();
            var r = s.Terraform(0, 0, Tool.Raise, 0);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Open ocean", r.Why);
        }

        [Test]
        public void SaveRoundTrip()
        {
            var s = NewSim();
            for (int k = 0; k < 50; k++) s.Step(.1);
            var s2 = new Sim(); s2.LoadJson(s.SaveJson());
            Assert.AreEqual(s.Buildings.Count, s2.Buildings.Count);
            Assert.AreEqual(s.Gold, s2.Gold, 1e-6);
            CollectionAssert.AreEqual(s.Hgt, s2.Hgt);
            CollectionAssert.AreEqual(s.Tree, s2.Tree);
            Assert.AreEqual(s.CapitalIsland.Name, s2.CapitalIsland.Name);
        }

        [Test]
        public void CharterStartsWithTheFirstGoalOpen()
        {
            var s = NewSim();
            Assert.AreEqual(8, Sim.GoalList.Length);
            Assert.IsFalse(s.Goals.Contains("advance"));
            s.AgeReady(out bool ok); Assert.IsFalse(ok);
        }
    }
}
