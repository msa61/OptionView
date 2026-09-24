using System.Collections.Generic;
using DxLink;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class SubscriptionsTests
    {
        [DataTestMethod]
        [DataRow("AAPL", SubscriptionType.AllEquity)]
        [DataRow("/ESZ24:XCME", SubscriptionType.AllEquity)]
        [DataRow(".SPY240119P450", SubscriptionType.AllOption)]
        [DataRow("./ESZ24C4500:XCME", SubscriptionType.AllOption)]
        public void Add_InfersEquityOrOptionSubscription(string symbol, SubscriptionType expected)
        {
            Subscriptions subs = new Subscriptions();
            Subscription s = subs.Add(symbol);

            Assert.AreEqual(expected, s.Type);
            Assert.AreEqual(expected, s.Status, "status starts with everything outstanding");
            Assert.AreEqual(symbol, s.Quote.Symbol);
        }

        [TestMethod]
        public void Add_ExplicitType_IsKept()
        {
            Subscription s = new Subscriptions().Add("AAPL", SubscriptionType.TimeSeries);
            Assert.AreEqual(SubscriptionType.TimeSeries, s.Type);
        }

        [TestMethod]
        public void Add_ExistingSymbol_ReturnsSameSubscriptionWithFreshCandles()
        {
            Subscriptions subs = new Subscriptions();
            Subscription first = subs.Add("AAPL");
            Candles originalCandles = first.Candles;

            Subscription second = subs.Add("AAPL");

            Assert.AreSame(first, second);
            Assert.AreNotSame(originalCandles, second.Candles);
            Assert.AreEqual(1, subs.Count);
        }

        [TestMethod]
        public void AddList_ReturnsOnlyRequestedSymbols()
        {
            Subscriptions subs = new Subscriptions();
            subs.Add("MSFT");

            Subscriptions added = subs.Add(new List<string> { "AAPL", "SPY" });

            Assert.AreEqual(2, added.Count);
            Assert.AreEqual(3, subs.Count);
        }

        [TestMethod]
        public void Remove_MissingSymbol_DoesNotThrow()
        {
            Subscriptions subs = new Subscriptions();
            subs.Add("AAPL");

            subs.Remove("NOPE");
            subs.Remove(new List<string> { "AAPL" });

            Assert.IsFalse(subs.Subscribed("AAPL"));
        }

        [TestMethod]
        public void IsComplete_WhenAllStatusesCleared()
        {
            Subscriptions subs = new Subscriptions();
            Subscription a = subs.Add("AAPL");
            Subscription b = subs.Add(".SPY240119P450");
            Assert.IsFalse(subs.IsComplete());

            a.Status = SubscriptionType.None;
            Assert.IsFalse(subs.IsComplete());

            b.Status = SubscriptionType.None;
            Assert.IsTrue(subs.IsComplete());
        }

        [TestMethod]
        public void GetStatus_CountsOutstandingEventTypes()
        {
            Subscriptions subs = new Subscriptions();
            Subscription equity = subs.Add("AAPL");            // Trade | Quote | Profile | Summary
            subs.Add(".SPY240119P450");                        // Trade | Quote | Greek
            equity.Status &= ~SubscriptionType.Trade;

            DxStatusParams status = subs.GetStatus();

            Assert.AreEqual(2, status.Count);
            Assert.AreEqual(1, status.RemainingTrade);
            Assert.AreEqual(2, status.RemainingQuote);
            Assert.AreEqual(1, status.RemainingProfile);
            Assert.AreEqual(1, status.RemainingSummary);
            Assert.AreEqual(1, status.RemainingGreek);
            Assert.AreEqual(0, status.RemainingTimeSeries);
            Assert.AreEqual(2, status.RemainingOverall);
        }
    }
}
