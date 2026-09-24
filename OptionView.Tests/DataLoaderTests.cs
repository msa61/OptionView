using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class DataLoaderTests
    {
        private static readonly DateTime Exp = new DateTime(2024, 1, 19);

        private static Positions Legs(params (string type, DateTime exp, decimal strike, decimal qty)[] legs)
        {
            Positions p = new Positions();
            int row = 0;
            foreach (var leg in legs)
                p.Add("SPY", leg.type, leg.exp, leg.strike, leg.qty, row++, "", 0);
            return p;
        }

        [TestMethod]
        public void GuessStrategy_SingleShortOption()
        {
            Assert.AreEqual("Short Put", DataLoader.GuessStrategy(Legs(("Put", Exp, 450m, -1))));
        }

        [TestMethod]
        public void GuessStrategy_SingleLongStock()
        {
            Positions p = new Positions();
            p.Add("AAPL", "Stock", DateTime.MinValue, 0m, 100, 0, "", 0);
            Assert.AreEqual("Long Stock", DataLoader.GuessStrategy(p));
        }

        [TestMethod]
        public void GuessStrategy_SingleClosedPosition_UsesInitialQuantity()
        {
            Positions p = new Positions();
            p.Add("SPY", "Call", Exp, 480m, -1, 0, "", 0);
            p.Add("SPY", "Call", Exp, 480m, 1, 1, "", 0);

            Assert.AreEqual("Short Call", DataLoader.GuessStrategy(p));
        }

        [TestMethod]
        public void GuessStrategy_Vertical()
        {
            Assert.AreEqual("Vertical Put Spread", DataLoader.GuessStrategy(Legs(("Put", Exp, 450m, -1), ("Put", Exp, 440m, 1))));
        }

        [TestMethod]
        public void GuessStrategy_Ratio()
        {
            Assert.AreEqual("Ratio Call Spread", DataLoader.GuessStrategy(Legs(("Call", Exp, 480m, -2), ("Call", Exp, 470m, 1))));
        }

        [TestMethod]
        public void GuessStrategy_TwoExpirations_IsCalendar()
        {
            Assert.AreEqual("Calendar Spread", DataLoader.GuessStrategy(Legs(("Call", Exp, 480m, -1), ("Call", Exp.AddDays(28), 480m, 1))));
        }

        [TestMethod]
        public void GuessStrategy_Straddle()
        {
            Assert.AreEqual("Straddle", DataLoader.GuessStrategy(Legs(("Put", Exp, 460m, -1), ("Call", Exp, 460m, -1))));
        }

        [TestMethod]
        public void GuessStrategy_Strangle()
        {
            Assert.AreEqual("Strangle", DataLoader.GuessStrategy(Legs(("Put", Exp, 450m, -1), ("Call", Exp, 480m, -1))));
        }

        [TestMethod]
        public void GuessStrategy_IronCondor()
        {
            Positions p = Legs(("Put", Exp, 440m, 1), ("Put", Exp, 450m, -1), ("Call", Exp, 480m, -1), ("Call", Exp, 490m, 1));
            Assert.AreEqual("Iron Condor", DataLoader.GuessStrategy(p));
        }

        [TestMethod]
        public void GuessStrategy_FourLegsAcrossExpirations_IsCalendar()
        {
            Positions p = Legs(("Put", Exp, 440m, 1), ("Put", Exp.AddDays(7), 450m, -1), ("Call", Exp, 480m, -1), ("Call", Exp.AddDays(7), 490m, 1));
            Assert.AreEqual("Calendar Spread", DataLoader.GuessStrategy(p));
        }

        [TestMethod]
        public void GuessStrategy_ThreeLegs_IsUnknown()
        {
            Positions p = Legs(("Put", Exp, 440m, 1), ("Put", Exp, 450m, -2), ("Put", Exp, 460m, 1));
            Assert.AreEqual("", DataLoader.GuessStrategy(p));
        }

        [DataTestMethod]
        [DataRow("Iron Condor", 1)]
        [DataRow("Vertical Put Spread", 1)]
        [DataRow("Strangle", 0)]
        [DataRow("Short Put", 0)]
        [DataRow("", 0)]
        public void DefaultDefinedRisk(string strategy, int expected)
        {
            Assert.AreEqual(expected, DataLoader.DefaultDefinedRisk(strategy));
        }

        [DataTestMethod]
        [DataRow("Iron Condor", 1)]
        [DataRow("Straddle", 1)]
        [DataRow("Strangle", 1)]
        [DataRow("Vertical Call Spread", 0)]
        [DataRow("Short", 0)]
        public void DefaultNeutralStrategy(string strategy, int expected)
        {
            Assert.AreEqual(expected, DataLoader.DefaultNeutralStrategy(strategy));
        }

        [TestMethod]
        public void DefaultRisk_DefinedRisk_IsCapitalLessCredit()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 150m, null, 0, "", 0, 0);
            p.Add("SPY", "Put", Exp, 440m, 1, -50m, null, 1, "", 0, 0);

            Assert.AreEqual(900m, DataLoader.DefaultRisk("Vertical Put Spread", 1000m, p));
        }

        [TestMethod]
        public void DefaultRisk_UndefinedRisk_IsZero()
        {
            Positions p = Legs(("Put", Exp, 450m, -1), ("Call", Exp, 480m, -1));
            Assert.AreEqual(0m, DataLoader.DefaultRisk("Strangle", 1000m, p));
        }
    }
}
