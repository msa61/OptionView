using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class TransactionGroupTests
    {
        private static readonly DateTime Exp = new DateTime(2024, 1, 19);
        private CultureInfo savedCulture;

        [TestInitialize]
        public void SetCulture()
        {
            savedCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        }

        [TestCleanup]
        public void RestoreCulture()
        {
            Thread.CurrentThread.CurrentCulture = savedCulture;
        }

        [TestMethod]
        public void Net_IsCostLessFees()
        {
            TransactionGroup g = new TransactionGroup("SPY") { Cost = 250m, Fees = 4.5m };
            Assert.AreEqual(245.5m, g.Net);
        }

        [TestMethod]
        public void GetPerLotCost_EvenLots_ReturnsCostPerLot()
        {
            TransactionGroup g = new TransactionGroup("SPY") { Cost = 300m };
            g.Holdings.Add("SPY", "Put", Exp, 450m, -2, 1, "", 0);
            g.Holdings.Add("SPY", "Call", Exp, 480m, -2, 2, "", 0);

            Assert.AreEqual(" - $150/lot", g.GetPerLotCost());
        }

        [TestMethod]
        public void GetPerLotCost_UnevenLots_ReturnsAsterisk()
        {
            TransactionGroup g = new TransactionGroup("SPY") { Cost = 300m };
            g.Holdings.Add("SPY", "Put", Exp, 450m, -2, 1, "", 0);
            g.Holdings.Add("SPY", "Call", Exp, 480m, -1, 2, "", 0);

            Assert.AreEqual(" *", g.GetPerLotCost());
        }

        [TestMethod]
        public void GetPerLotCost_SingleLotOrStockOnly_ReturnsEmpty()
        {
            TransactionGroup single = new TransactionGroup("SPY") { Cost = 300m };
            single.Holdings.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);
            Assert.AreEqual("", single.GetPerLotCost());

            TransactionGroup stock = new TransactionGroup("AAPL") { Cost = -18000m };
            stock.Holdings.Add("AAPL", "Stock", DateTime.MinValue, 0m, 100, 1, "", 0);
            Assert.AreEqual("", stock.GetPerLotCost());
        }

        [TestMethod]
        public void GetPerLotCost_NoHoldings_ReturnsOopsie()
        {
            Assert.AreEqual(" oopsie", new TransactionGroup("SPY").GetPerLotCost());
        }

        [DataTestMethod]
        [DataRow("Put", 450.0, 440.0, true)]
        [DataRow("Put", 450.0, 460.0, false)]
        [DataRow("Call", 480.0, 490.0, true)]
        [DataRow("Call", 480.0, 470.0, false)]
        public void HasInTheMoneyPositions_ComparesStrikeToUnderlying(string type, double strike, double underlying, bool expected)
        {
            TransactionGroup g = new TransactionGroup("SPY") { UnderlyingPrice = (decimal)underlying };
            g.Holdings.Add("SPY", type, Exp, (decimal)strike, -1, 1, "", 0);

            Assert.AreEqual(expected, g.HasInTheMoneyPositions());
        }

        [TestMethod]
        public void HasInTheMoneyPositions_UnknownUnderlying_IsFalse()
        {
            TransactionGroup g = new TransactionGroup("SPY");
            g.Holdings.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);

            Assert.IsFalse(g.HasInTheMoneyPositions());
        }

        [TestMethod]
        public void GetStrikes_FiltersByTypeAndReturnsNullWhenNone()
        {
            TransactionGroup g = new TransactionGroup("SPY");
            g.Holdings.Add("SPY", "Put", Exp, 440m, 1, 1, "", 0);
            g.Holdings.Add("SPY", "Put", Exp, 450m, -1, 2, "", 0);

            CollectionAssert.AreEquivalent(new[] { 440m, 450m }, g.GetStrikes("Put"));
            Assert.IsNull(g.GetStrikes("Call"));
        }

        [TestMethod]
        public void GetDescription_AllOpens_IsOpened()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 1, "Sell to Open", 0);
            p.Add("SPY", "Call", Exp, 480m, -1, 2, "Sell to Open", 0);

            Assert.AreEqual("Opened", new TransactionGroup().GetDescription(p));
        }

        [TestMethod]
        public void GetDescription_AllCloses_IsClosed()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, 1, 1, "Buy to Close", 0);

            Assert.AreEqual("Closed", new TransactionGroup().GetDescription(p));
        }

        [TestMethod]
        public void GetDescription_CloseAndOpenSameExpiration_IsRolled()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, 1, 1, "Buy to Close", 0);
            p.Add("SPY", "Put", Exp, 440m, -1, 2, "Sell to Open", 0);

            Assert.AreEqual("Rolled", new TransactionGroup().GetDescription(p));
        }

        [TestMethod]
        public void GetDescription_CloseAndOpenDifferentExpiration_IsRolledOut()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, 1, 1, "Buy to Close", 0);
            p.Add("SPY", "Put", Exp.AddDays(28), 450m, -1, 2, "Sell to Open", 0);

            Assert.AreEqual("Rolled Out", new TransactionGroup().GetDescription(p));
        }

        [DataTestMethod]
        [DataRow("Expiration", "Expired")]
        [DataRow("Assignment", "Assigned")]
        [DataRow("Dividend", "Dividend")]
        public void GetDescription_SpecialEvents(string transType, string expected)
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, 1, 1, transType, 0);

            Assert.AreEqual(expected, new TransactionGroup().GetDescription(p));
        }
    }
}
