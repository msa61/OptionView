using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class PositionsTests
    {
        private static readonly DateTime Exp = new DateTime(2024, 1, 19);

        [TestMethod]
        public void Add_OptionKeyIncludesExpirationStrikeAndType()
        {
            Positions p = new Positions();
            string key = p.Add("SPY", "Put", Exp, 450m, -1, 1, "Sell to Open", 0);
            Assert.AreEqual("SPY2401190450.0Put", key);
        }

        [TestMethod]
        public void Add_StockKeyIsSymbol()
        {
            Positions p = new Positions();
            string key = p.Add("AAPL", "Stock", DateTime.MinValue, 0m, 100, 1, "Buy to Open", 0);
            Assert.AreEqual("AAPL", key);
        }

        [TestMethod]
        public void Add_SameContract_AggregatesQuantityAmountAndRows()
        {
            Positions p = new Positions();
            DateTime early = new DateTime(2024, 1, 2, 10, 0, 0);
            DateTime late = early.AddDays(3);

            p.Add("SPY", "Put", Exp, 450m, -2, 120m, late, 7, "Sell to Open", 0, 470m);
            string key = p.Add("SPY", "Put", Exp, 450m, 1, -40m, early, 9, "Buy to Close", 0, 460m);

            Assert.AreEqual(1, p.Count);
            Position pos = p[key];
            Assert.AreEqual(-1m, pos.Quantity);
            Assert.AreEqual(80m, pos.Amount);
            Assert.AreEqual(early, pos.TransTime, "keeps the earliest transaction time");
            Assert.AreEqual(-2m, pos.InitialQuantity, "initial quantity is from the first add");
            Assert.AreEqual("Buy to Close", pos.TransType, "open/close reflects the latest add");
            CollectionAssert.AreEqual(new[] { 7, 9 }, pos.Rows);
        }

        [TestMethod]
        public void GroupID_TracksLastNonZeroGroup()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 1, "", 12);
            p.Add("SPY", "Call", Exp, 480m, -1, 2, "", 0);
            Assert.AreEqual(12, p.GroupID());
        }

        [TestMethod]
        public void PurgeEmptyPositions_RemovesOnlyFlatPositions()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);
            p.Add("SPY", "Put", Exp, 450m, 1, 2, "", 0);
            p.Add("SPY", "Call", Exp, 480m, -1, 3, "", 0);
            p.Add("SPY", "Call", Exp, 490m, 0, 4, "", 0);

            p.PurgeEmptyPositions();

            Assert.AreEqual(1, p.Count);
            Assert.AreEqual(480m, p.Values.Single().Strike);
        }

        [TestMethod]
        public void IsAllClosed_TrueOnlyWhenEveryQuantityIsZero()
        {
            Positions p = new Positions();
            Assert.IsTrue(p.IsAllClosed(), "empty set is closed");

            p.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);
            Assert.IsFalse(p.IsAllClosed());

            p.Add("SPY", "Put", Exp, 450m, 1, 2, "", 0);
            Assert.IsTrue(p.IsAllClosed());
        }

        [TestMethod]
        public void Includes_MatchesOnStrikeOnly()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);

            Assert.IsTrue(p.Includes("Call", Exp.AddDays(7), 450m));
            Assert.IsFalse(p.Includes("Put", Exp, 455m));
        }

        [TestMethod]
        public void Concat_MergesQuantitiesIntoExistingPositions()
        {
            Positions a = new Positions();
            a.Add("SPY", "Put", Exp, 450m, -1, 1, "", 0);
            Positions b = new Positions();
            b.Add("SPY", "Put", Exp, 450m, -2, 2, "", 0);
            b.Add("SPY", "Call", Exp, 480m, -1, 3, "", 0);

            Positions result = a.Concat(b);

            Assert.AreSame(a, result);
            Assert.AreEqual(2, a.Count);
            Assert.AreEqual(-3m, a["SPY2401190450.0Put"].Quantity);
            Assert.AreEqual(-1m, a["SPY2401190480.0Call"].Quantity);
        }

        [TestMethod]
        public void GetRowNumbers_ReturnsRowsFromAllPositions()
        {
            Positions p = new Positions();
            p.Add("SPY", "Put", Exp, 450m, -1, 4, "", 0);
            p.Add("SPY", "Put", Exp, 450m, 1, 8, "", 0);
            p.Add("SPY", "Call", Exp, 480m, -1, 5, "", 0);

            CollectionAssert.AreEquivalent(new[] { 4, 8, 5 }, p.GetRowNumbers());
        }

        [TestMethod]
        public void AddPosition_UsesSameKeyScheme()
        {
            Positions p = new Positions();
            string key = p.Add(new Position { Symbol = "QQQ", Type = "Call", ExpDate = Exp, Strike = 400m, Quantity = 1 });
            Assert.AreEqual("QQQ2401190400.0Call", key);
            Assert.AreEqual(1m, p[key].Quantity);
        }
    }
}
