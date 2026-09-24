using Microsoft.VisualStudio.TestTools.UnitTesting;
using OptionView;
using static OptionView.BlackScholes;

namespace OptionView.Tests
{
    [TestClass]
    public class BlackScholesTests
    {
        // textbook case: S=100, K=100, r=5%, no dividend, IV=20%, one year to expiration
        private const double S = 100, K = 100, R = 0.05, Q = 0.0, Sigma = 0.20;
        private const int Days = 365;

        [TestMethod]
        public void Price_Call_MatchesReferenceValue()
        {
            decimal price = BlackScholes.Price(OptionType.Call, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(10.4506, (double)price, 0.0001);
        }

        [TestMethod]
        public void Price_Put_MatchesReferenceValue()
        {
            decimal price = BlackScholes.Price(OptionType.Put, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(5.5735, (double)price, 0.0001);
        }

        [TestMethod]
        public void Price_DecimalOverload_MatchesDoubleOverload()
        {
            decimal fromDecimal = BlackScholes.Price(OptionType.Put, 50m, 55m, 0.03, 0, 0.35, 30);
            decimal fromDouble = BlackScholes.Price(OptionType.Put, 50.0, 55.0, 0.03, 0, 0.35, 30);
            Assert.AreEqual(fromDouble, fromDecimal);
            Assert.AreEqual(5.3680, (double)fromDecimal, 0.0001);
        }

        [DataTestMethod]
        [DataRow(100.0, 100.0, 0.05, 0.20, 365)]
        [DataRow(50.0, 55.0, 0.03, 0.35, 30)]
        [DataRow(420.0, 400.0, 0.045, 0.15, 7)]
        public void Price_SatisfiesPutCallParity(double s, double x, double r, double sigma, int days)
        {
            double t = days / 365.0;
            double call = (double)BlackScholes.Price(OptionType.Call, s, x, r, 0, sigma, days);
            double put = (double)BlackScholes.Price(OptionType.Put, s, x, r, 0, sigma, days);

            // C - P = S - K*e^(-rT)
            Assert.AreEqual(s - x * System.Math.Exp(-r * t), call - put, 1e-9);
        }

        [TestMethod]
        public void Price_DeepInTheMoneyCall_ApproachesIntrinsicValue()
        {
            double price = (double)BlackScholes.Price(OptionType.Call, 200.0, 100.0, 0, 0, 0.2, 1);
            Assert.AreEqual(100.0, price, 0.01);
        }

        [TestMethod]
        public void Theta_Call_MatchesReferenceValuePerDay()
        {
            decimal theta = BlackScholes.Theta(OptionType.Call, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(-0.017573, (double)theta, 0.000001);
        }

        [TestMethod]
        public void Theta_Put_MatchesReferenceValuePerDay()
        {
            decimal theta = BlackScholes.Theta(OptionType.Put, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(-0.004542, (double)theta, 0.000001);
        }

        [TestMethod]
        public void Theta_AtExpiration_IsZero()
        {
            Assert.AreEqual(0m, BlackScholes.Theta(OptionType.Call, S, K, R, Q, Sigma, 0));
        }

        [TestMethod]
        public void Vega_MatchesReferenceValuePerVolPoint()
        {
            Assert.AreEqual(0.375240, BlackScholes.Vega(S, K, R, Q, Sigma, Days), 0.000001);
        }

        [TestMethod]
        public void Vega_AtExpiration_IsZero()
        {
            Assert.AreEqual(0.0, BlackScholes.Vega(S, K, R, Q, Sigma, 0));
        }

        [DataTestMethod]
        [DataRow(0.20)]
        [DataRow(0.45)]
        [DataRow(1.16)]
        public void IV_RecoversVolatilityFromPrice(double sigma)
        {
            decimal price = BlackScholes.Price(OptionType.Call, S, K, R, Q, sigma, 45);
            double iv = BlackScholes.IV(OptionType.Call, S, K, R, Q, price, 45);
            Assert.AreEqual(sigma, iv, 0.001);
        }

        [TestMethod]
        public void Delta_Call_MatchesReferenceValue()
        {
            decimal delta = BlackScholes.Delta(OptionType.Call, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(0.6368, (double)delta, 0.0001);
        }

        [TestMethod]
        public void Delta_Put_IsNegativeAndMatchesReferenceValue()
        {
            decimal delta = BlackScholes.Delta(OptionType.Put, S, K, R, Q, Sigma, Days);
            Assert.AreEqual(-0.3632, (double)delta, 0.0001);
        }

        [TestMethod]
        public void Delta_WithDividendYield_CallMinusPutEqualsDividendDiscount()
        {
            const double q = 0.02;
            double call = (double)BlackScholes.Delta(OptionType.Call, S, K, R, q, Sigma, Days);
            double put = (double)BlackScholes.Delta(OptionType.Put, S, K, R, q, Sigma, Days);

            Assert.AreEqual(System.Math.Exp(-q), call - put, 1e-9);
        }

        [DataTestMethod]
        [DataRow(true, 110.0, 10.0, 1.0)]
        [DataRow(true, 90.0, 0.0, 0.0)]
        [DataRow(false, 90.0, 10.0, -1.0)]
        [DataRow(false, 110.0, 0.0, 0.0)]
        public void AtExpiration_PriceIsIntrinsicAndDeltaIsBinary(bool isCall, double s, double price, double delta)
        {
            OptionType type = isCall ? OptionType.Call : OptionType.Put;
            Assert.AreEqual((decimal)price, BlackScholes.Price(type, s, K, R, Q, Sigma, 0));
            Assert.AreEqual((decimal)delta, BlackScholes.Delta(type, s, K, R, Q, Sigma, 0));
        }

        [TestMethod]
        public void DeltaFromPrice_AgreesWithDeltaAtImpliedVolatility()
        {
            decimal price = BlackScholes.Price(OptionType.Call, S, K, R, Q, Sigma, 60);
            decimal expected = BlackScholes.Delta(OptionType.Call, S, K, R, Q, Sigma, 60);
            decimal actual = BlackScholes.DeltaFromPrice(OptionType.Call, S, K, R, Q, price, 60);
            Assert.AreEqual((double)expected, (double)actual, 0.001);
        }
    }
}
