using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class TastyWorksTests
    {
        private static TWTransaction Complete(string code, string subcode, decimal qty, string action = null)
        {
            TWTransaction tr = new TWTransaction { TransactionCode = code, TransactionSubcode = subcode, Action = action, Quantity = qty };
            TastyWorks.CompleteInstance(tr);
            return tr;
        }

        [DataTestMethod]
        [DataRow("Buy to Open", "Buy", "Open", 2)]
        [DataRow("Buy to Close", "Buy", "Close", 2)]
        [DataRow("Sell to Open", "Sell", "Open", -2)]
        [DataRow("Sell to Close", "Sell", "Close", -2)]
        public void Trade_SetsDirectionAndSignsSells(string subcode, string buySell, string openClose, int expectedQty)
        {
            TWTransaction tr = Complete("Trade", subcode, 2);

            Assert.AreEqual(buySell, tr.BuySell);
            Assert.AreEqual(openClose, tr.OpenClose);
            Assert.AreEqual((decimal)expectedQty, tr.Quantity);
        }

        [TestMethod]
        public void Assignment_ClosesWithoutChangingQuantity()
        {
            TWTransaction tr = Complete("Receive Deliver", "Assignment", 1);

            Assert.AreEqual("Close", tr.OpenClose);
            Assert.AreEqual(1m, tr.Quantity);
        }

        [TestMethod]
        public void Exercise_ClosesAndNegatesQuantity()
        {
            TWTransaction tr = Complete("Receive Deliver", "Exercise", 1);

            Assert.AreEqual("Close", tr.OpenClose);
            Assert.AreEqual(-1m, tr.Quantity);
        }

        [TestMethod]
        public void Expiration_MarksExpiredAndClosed()
        {
            TWTransaction tr = Complete("Receive Deliver", "Expiration", 1);

            Assert.AreEqual("Expired", tr.BuySell);
            Assert.AreEqual("Close", tr.OpenClose);
        }

        [TestMethod]
        public void ReceiveDeliverStock_ParsedFromSubcode()
        {
            TWTransaction tr = Complete("Receive Deliver", "Buy to Open", 100);

            Assert.AreEqual("Stock", tr.InsType);
            Assert.AreEqual("Buy", tr.BuySell);
            Assert.AreEqual("Open", tr.OpenClose);
            Assert.AreEqual(100m, tr.Quantity);
        }

        [TestMethod]
        public void ReceiveDeliverStockSale_NegatesQuantity()
        {
            TWTransaction tr = Complete("Receive Deliver", "Sell to Close", 100);

            Assert.AreEqual("Sell", tr.BuySell);
            Assert.AreEqual("Close", tr.OpenClose);
            Assert.AreEqual(-100m, tr.Quantity);
        }

        [TestMethod]
        public void ForwardSplit_UsesActionForDirection()
        {
            TWTransaction tr = Complete("Receive Deliver", "Forward Split", 50, action: "Sell to Close");

            Assert.AreEqual("Sell", tr.BuySell);
            Assert.AreEqual("Close", tr.OpenClose);
            Assert.AreEqual(-50m, tr.Quantity);
        }

        [TestMethod]
        public void TransactionParse_IsCaseInsensitiveAndIgnoresUnknown()
        {
            TWTransaction tr = new TWTransaction();
            TastyWorks.TransactionParse("SELL TO OPEN", tr);
            Assert.AreEqual("Sell", tr.BuySell);
            Assert.AreEqual("Open", tr.OpenClose);

            TWTransaction other = new TWTransaction();
            TastyWorks.TransactionParse("Dividend", other);
            Assert.IsNull(other.BuySell);
            Assert.IsNull(other.OpenClose);
        }
    }
}
