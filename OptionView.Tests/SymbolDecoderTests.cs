using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    // Note: SymbolDecoder logs a warning if its two parsers disagree; these
    // cases use symbols in the formats both parsers are known to handle.
    [TestClass]
    public class SymbolDecoderTests
    {
        [TestMethod]
        public void EquityOption_DecodesExpirationTypeAndStrike()
        {
            SymbolDecoder d = new SymbolDecoder("ROKU  200417P00065000", "Equity Option");

            Assert.AreEqual("Put", d.Type);
            Assert.AreEqual(new DateTime(2020, 4, 17), d.Expiration);
            Assert.AreEqual(65m, d.Strike);
        }

        [TestMethod]
        public void EquityOption_DecodesFractionalStrike()
        {
            SymbolDecoder d = new SymbolDecoder("SPY   241220C00452500", "Equity Option");

            Assert.AreEqual("Call", d.Type);
            Assert.AreEqual(new DateTime(2024, 12, 20), d.Expiration);
            Assert.AreEqual(452.5m, d.Strike);
        }

        [TestMethod]
        public void FutureOption_DecodesIntegerStrike()
        {
            SymbolDecoder d = new SymbolDecoder("./CLM0 LOM0  200514P15", "Future Option");

            Assert.AreEqual("Put", d.Type);
            Assert.AreEqual(new DateTime(2020, 5, 14), d.Expiration);
            Assert.AreEqual(15m, d.Strike);
        }

        [TestMethod]
        public void FutureOption_DecodesDecimalStrike()
        {
            SymbolDecoder d = new SymbolDecoder("./NGN0 LNEN0 200625C1.6", "Future Option");

            Assert.AreEqual("Call", d.Type);
            Assert.AreEqual(new DateTime(2020, 6, 25), d.Expiration);
            Assert.AreEqual(1.6m, d.Strike);
        }

        [TestMethod]
        public void Equity_IsStock()
        {
            SymbolDecoder d = new SymbolDecoder("AAPL", "Equity");

            Assert.AreEqual("Stock", d.Type);
            Assert.AreEqual(DateTime.MinValue, d.Expiration);
            Assert.AreEqual(0m, d.Strike);
        }
    }
}
