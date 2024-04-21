using System;
using System.Diagnostics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RootFinding;

namespace OptionView
{
    class BlackScholes
    {
        // s - underlying price
        // x - strike
        // r - interest (0.005 = 1/2%)
        // q - dividend rate
        // sigma - IV (116% would be passed as 1.16)
        // t - number of days until expiration
        // 


        public enum OptionType
        {
            Call,
            Put
        }

        private static double D1(double s, double x, double r, double q, double sigma, double t)
        {
            return (Math.Log(s / x) + (r - q + (sigma * sigma) / 2) * t) / (sigma * Math.Sqrt(t));
        }

        private static double D2(double d1, double sigma, double t)
        {
            return d1 - (sigma * Math.Sqrt(t));
        }

        static public decimal Price(OptionType type, decimal s, decimal x, double r, double q, double sigma, int days)
        {
            return Price(type, Convert.ToDouble(s), Convert.ToDouble(x), r, q, sigma, days);
        }

        static public decimal Price(OptionType type, double s, double x, double r, double q, double sigma, int days)
        {
            double t = Convert.ToDouble(days) / 365;
            double d1 = D1(s, x, r, q, sigma, t);
            double d2 = D2(d1, sigma, t);

            switch (type)
            {
                case OptionType.Call:
                    return Convert.ToDecimal(s * Math.Exp(-q * t) * Normal.CDF(0, 1, d1) - x * Math.Exp(-r * t) * Normal.CDF(0, 1, d2));
                case OptionType.Put:
                    return Convert.ToDecimal(x * Math.Exp(-r * t) * Normal.CDF(0, 1, -d2) - s * Math.Exp(-q * t) * Normal.CDF(0, 1, -d1));
            }
            return 0;
        }

        static public decimal Delta(OptionType type, decimal s, decimal x, double r, double q, double sigma, int days)
        {
            return Delta(type, Convert.ToDouble(s), Convert.ToDouble(x), r, q, sigma, days);
        }

        static public decimal Delta(OptionType type, double s, double x, double r, double q, double sigma, int days)
        {
            double t = Convert.ToDouble(days) / 365;
            double d1 = D1(s, x, r, q, sigma, t);

            switch (type)
            {
                case OptionType.Call:
                    return Convert.ToDecimal(Math.Exp(-r * t) * Normal.CDF(0, 1, d1));
                case OptionType.Put:
                    return Convert.ToDecimal(-Math.Exp(-r * t) * (Normal.CDF(0, 1, d1) - 1));
            }
            return 0;
        }


        static public decimal Theta(OptionType type, decimal s, decimal x, double r, double q, double sigma, int days)
        {
            return Theta(type, Convert.ToDouble(s), Convert.ToDouble(x), r, q, sigma, days);
        }


        static public decimal Theta(OptionType type, double s, double x, double r, double q, double sigma, int days)
        {
            double t = Convert.ToDouble(days) / 365;
            double d1 = D1(s, x, r, q, sigma, t);
            double d2 = D2(d1, sigma, t);

            if (t > 0)
            {
                switch (type)
                {
                    case OptionType.Call:
                        {
                            double theta = -s * sigma * Math.Exp(-q * t) * Normal.PDF(0, 1, d1) / 2 / Math.Sqrt(t);
                            theta -= (r * x * Math.Exp(-r * t) * Normal.CDF(0, 1, d2));
                            theta += (q * s * Math.Exp(-q * t) * Normal.CDF(0, 1, d1));
                            return Convert.ToDecimal(theta / 365);
                        }
                    case OptionType.Put:
                        {
                            double theta = -s * sigma * Math.Exp(-q * t) * Normal.PDF(0, 1, d1) / 2 / Math.Sqrt(t);
                            theta += (r * x * Math.Exp(-r * t) * Normal.CDF(0, 1, -d2));
                            theta -= (q * s * Math.Exp(-q * t) * Normal.CDF(0, 1, -d1));
                            return Convert.ToDecimal(theta / 365);
                        }
                }
            }
            return 0;
        }

        static public double Vega(double s, double x, double r, double q, double sigma, int days)
        {
            double t = Convert.ToDouble(days) / 365;
            double d1 = D1(s, x, r, q, sigma, t);
            double d2 = D2(d1, sigma, t);

            if (t > 0)
            {
                return s * Math.Exp(-q * t) * Normal.PDF(0, 1, d1) * Math.Sqrt(t) / 100;
            }
            return 0;
        }

        static public double IV(OptionType type, double s, double x, double r, double q, decimal optionPrice, int days)
        {
            double t = Convert.ToDouble(days) / 365;

            Func<double, double> f = sigma => Convert.ToDouble(Price(type, s, x, r, q, sigma, days) - optionPrice);
            Func<double, double> df = sigma => Vega(s, x, r, q, sigma, days);

            return RobustNewtonRaphson.FindRoot(f, df, lowerBound: 0, upperBound: 100, accuracy: 0.001);
        }



        static public decimal DeltaFromPrice(OptionType type, decimal s, decimal x, double r, double q, decimal optionPrice, int days)
        {
            return DeltaFromPrice(type, Convert.ToDouble(s), Convert.ToDouble(x), r, q, optionPrice, days);
        }

        static public decimal DeltaFromPrice(OptionType type, double s, double x, double r, double q, decimal optionPrice, int days)
        {
            try
            {
                double sigma = IV(type, s, x, r, q, optionPrice, days);

                return Delta(type, s, x, r, q, sigma, days);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return 0;
        }




    }
}
