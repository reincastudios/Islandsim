using System;
using System.Linq;
using IslandSim.Core;
using NUnit.Framework;

namespace IslandSim.Tests
{
    /// <summary>Rules-level checks that run without a scene (Window > General > Test Runner > EditMode).</summary>
    public class SimTests
    {
        static Sim NewSim() { var s = new Sim(); s.NewGame(); return s; }

        static (int x, int y) FindHarborSite(Sim s, int origin)
        {
            int bx = -1, by = -1; double best = 1e9;
            for (int y = 0; y < Defs.H; y++)
                for (int x = 0; x < Defs.W; x++)
                {
                    if (s.Origin[Sim.Idx(x, y)] != origin || !s.CanPlace(BType.Harbor, x, y).Ok) continue;
                    double d = Math.Abs(x - Defs.AcX) + Math.Abs(y - Defs.AcY);
                    if (d < best) { best = d; bx = x; by = y; }
                }
            return (bx, by);
        }

        [Test]
        public void NewGameFoundsCapitalWithBerthAndVillage()
        {
            var s = NewSim();
            var cap = s.CapitalHarbor;
            Assert.NotNull(cap);
            Assert.IsTrue(s.BerthOf(cap.X, cap.Y, out _, out _), "capital harbor needs deep water beside it");
            Assert.AreEqual(7, s.Buildings.Count, "harbor + 4 houses + fishery + lumber camp");
            Assert.AreEqual("Palm Isle", s.CapitalIsland.Name);
        }

        [Test]
        public void ThreeMainIslandsExist()
        {
            var s = NewSim();
            var names = s.Islands.Where(i => i.N >= 60).Select(i => i.Name).ToArray();
            CollectionAssert.IsSupersetOf(names, new[] { "Palm Isle", "Basalt Isle", "Sugar Cay" });
            Assert.Greater(s.Islands.First(i => i.Name == "Palm Isle").N, s.Islands.First(i => i.Name == "Sugar Cay").N);
        }

        [Test]
        public void StarterVillageGrows()
        {
            var s = NewSim();
            float before = s.TotalPop;
            for (int k = 0; k < 600; k++) s.Step(0.1f);
            Assert.Greater(s.TotalPop, before);
        }

        [Test]
        public void SugarCayNeedsDredgingBeforeBoatsSail()
        {
            var s = NewSim();
            s.Gold = 99999; s.CapitalHarbor.Stock[(int)Good.Wood] = 500;
            var (sx, sy) = FindHarborSite(s, 2);
            Assert.GreaterOrEqual(sx, 0);
            Assert.IsTrue(s.Place(BType.Harbor, sx, sy));
            s.Step(0.1f);
            var boat = s.Boats.Single(b => s.ById(b.Home).X == sx && s.ById(b.Home).Y == sy);
            Assert.AreEqual(BoatState.Blocked, boat.State);

            // dredge a straight channel toward the lagoon centre until a route opens
            double dx = Defs.AcX - sx, dy = Defs.AcY - sy, len = Math.Sqrt(dx * dx + dy * dy); dx /= len; dy /= len;
            for (int st = 1; st < 60 && boat.State != BoatState.Sail; st++)
            {
                int x = (int)Math.Round(sx + dx * st), y = (int)Math.Round(sy + dy * st);
                while (s.Hgt[Sim.Idx(x, y)] > -2) s.TerraformBrush(x, y, 0, TerraMode.Lower, 0, null);
                s.Step(0.05f);
            }
            Assert.AreEqual(BoatState.Sail, boat.State);
        }

        [Test]
        public void RaisingLandCostsStoneAndClearsTrees()
        {
            var s = NewSim();
            var cap = s.CapitalHarbor; float stone = cap.Stock[(int)Good.Stone];
            int tx = -1, ty = -1;
            for (int y = 0; y < Defs.H && tx < 0; y++)
                for (int x = 0; x < Defs.W; x++)
                    if (s.Tree[Sim.Idx(x, y)] != 0 && s.Bld[Sim.Idx(x, y)] < 0 && s.Hgt[Sim.Idx(x, y)] < Defs.MaxH && s.IslandAt(x, y) == s.CapitalIsland) { tx = x; ty = y; break; }
            Assert.GreaterOrEqual(tx, 0);
            var r = s.Terraform(tx, ty, TerraMode.Raise);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, s.Tree[Sim.Idx(tx, ty)]);
            Assert.AreEqual(stone - 1, cap.Stock[(int)Good.Stone], 1e-4);
        }

        [Test]
        public void SaveRoundTrip()
        {
            var s = NewSim();
            for (int k = 0; k < 50; k++) s.Step(0.1f);
            var s2 = new Sim();
            Assert.IsTrue(s2.FromSave(s.ToSave()));
            Assert.AreEqual(s.Buildings.Count, s2.Buildings.Count);
            Assert.AreEqual(s.Gold, s2.Gold, 1e-3);
            CollectionAssert.AreEqual(s.Hgt, s2.Hgt);
        }
    }
}
