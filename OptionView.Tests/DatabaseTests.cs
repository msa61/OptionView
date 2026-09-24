using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    // App.ConnStr is a process-wide static, so these must not run in parallel with each other.
    [TestClass]
    [DoNotParallelize]
    public class DatabaseTests
    {
        private TestDatabase db;
        private CultureInfo savedCulture;

        [TestInitialize]
        public void Setup()
        {
            savedCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            db = new TestDatabase();
        }

        [TestCleanup]
        public void Cleanup()
        {
            db.Dispose();
            Thread.CurrentThread.CurrentCulture = savedCulture;
        }

        [TestMethod]
        public void Config_GetProp_MissingPropIsEmpty()
        {
            Assert.AreEqual("", Config.GetProp("nothing-here"));
        }

        [TestMethod]
        public void Config_SetProp_InsertsThenUpdates()
        {
            Assert.IsTrue(Config.SetProp("theme", "dark"));
            Assert.AreEqual("dark", Config.GetProp("theme"));

            Assert.IsTrue(Config.SetProp("theme", "light"));
            Assert.AreEqual("light", Config.GetProp("theme"));
        }

        [TestMethod]
        public void Config_GetDateProp_ParsesStoredDate()
        {
            DateTime local = new DateTime(2024, 6, 3, 14, 30, 0, DateTimeKind.Local);
            Config.SetProp("LastUpdate", local.ToString("o"));

            Assert.AreEqual(local.ToUniversalTime(), Config.GetDateProp("LastUpdate"));
        }

        [TestMethod]
        public void Config_GetDateProp_MissingOrInvalidIsMinValue()
        {
            Config.SetProp("bad", "not a date");

            Assert.AreEqual(DateTime.MinValue, Config.GetDateProp("missing"));
            Assert.AreEqual(DateTime.MinValue, Config.GetDateProp("bad"));
        }

        [TestMethod]
        public void Config_EncryptedProp_RoundTripsAndIsNotStoredInPlainText()
        {
            const string secret = "refresh-token-abc123!";

            Assert.IsTrue(Config.SetEncryptedProp("token", secret));

            Assert.IsFalse(Config.GetProp("token").Contains(secret));
            Assert.AreEqual(secret, Config.GetEncryptedProp("token"));
        }

        [TestMethod]
        public void Config_EncryptedProp_MissingIsEmpty()
        {
            Assert.AreEqual("", Config.GetEncryptedProp("missing"));
        }

        [TestMethod]
        public void TransactionGroup_GetHistoryText_DescribesEachTradeEvent()
        {
            db.AddTransaction(5, "2024-01-05 15:30:00", "Sell to Open", "SPY", "Put", "2024-01-19", 450, -1, 250, 465);
            db.AddTransaction(5, "2024-01-05 15:30:00", "Sell to Open", "SPY", "Call", "2024-01-19", 480, -1, 200, 465);
            db.AddTransaction(5, "2024-01-12 16:00:00", "Buy to Close", "SPY", "Put", "2024-01-19", 450, 1, -100, 470);
            db.AddTransaction(5, "2024-01-12 16:00:00", "Buy to Close", "SPY", "Call", "2024-01-19", 480, 1, -50, 470);
            db.AddTransaction(6, "2024-01-05 15:30:00", "Sell to Open", "QQQ", "Put", "2024-01-19", 400, -1, 99);

            string text = new TransactionGroup("SPY") { GroupID = 5 }.GetHistoryText();

            string expected =
                "5-Jan-24 15:30:00   Opened for $450  @ $465.00\n" +
                "   -1 Put 450 Jan19 STO\n" +
                "   -1 Call 480 Jan19 STO\n" +
                "12-Jan-24 16:00:00   Closed for ($150)  @ $470.00\n" +
                "    1 Put 450 Jan19 BTC\n" +
                "    1 Call 480 Jan19 BTC\n";
            Assert.AreEqual(expected, text);
        }

        [TestMethod]
        public void TransactionGroup_GetOptionStrikeHistory_CarriesOpenStrikesForward()
        {
            db.AddTransaction(5, "2024-01-05 15:30:00", "Sell to Open", "SPY", "Put", "2024-01-19", 450, -1, 250);
            db.AddTransaction(5, "2024-01-05 15:30:00", "Sell to Open", "SPY", "Call", "2024-01-19", 480, -1, 200);
            db.AddTransaction(5, "2024-01-10 15:30:00", "Buy to Close", "SPY", "Put", "2024-01-19", 450, 1, -80);
            db.AddTransaction(5, "2024-01-10 15:30:00", "Sell to Open", "SPY", "Put", "2024-01-19", 460, -1, 180);

            SortedList<DateTime, List<decimal>> history = new TransactionGroup("SPY") { GroupID = 5 }.GetOptionStrikeHistory("Put");

            Assert.AreEqual(2, history.Count);
            CollectionAssert.AreEqual(new[] { 450m }, history.Values[0]);
            CollectionAssert.AreEqual(new[] { 460m }, history.Values[1]);
        }
    }
}
