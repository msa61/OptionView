using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace OptionView
{
    public class EquityProfile
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public double ImpliedVolatility { get; set; }
        public double ImpliedVolatilityRank { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal UnderlyingPriceChange { get; set; }
        public decimal UnderlyingPriceChangePercent { get; set; }
        public double OptionVolume { get; set; }
        public DateTime EarningsDate { get; set; }
        public double DaysUntilEarnings { get; set; } = -1;
        public Visibility EarningsInNextSession { get; set; } = Visibility.Collapsed;
        public string EarningsTimeOfDay { get; set; }
        public decimal MarketCap { get; set; }
        public Visibility CurrentlyHeld { get; set; } = Visibility.Hidden;
    }



    public class EquityProfiles : List<EquityProfile>
    {
        public EquityProfiles(Portfolio portfolio)
        {
            try
            {
                List<string> symbols = TastyWorks.WatchListSymbols();
                if ((symbols != null) && (symbols.Count > 0))
                {
                    TWMarketInfos symbolData = TastyWorks.MarketInfo(symbols);
                    Dictionary<string, DxLink.Quote> quotes = App.DxHandler.GetQuotes(symbols).Result;

                    App.UpdateStatusMessage("Reconciling data");
                    foreach (KeyValuePair<string, TWMarketInfo> item in symbolData)
                    {
                        TWMarketInfo sym = item.Value;

                        EquityProfile equity = new EquityProfile()
                        {
                            Symbol = sym.Symbol,
                            ImpliedVolatility = sym.ImpliedVolatility,
                            ImpliedVolatilityRank = sym.ImpliedVolatilityRank,
                            EarningsDate = sym.EarningsDate,
                            EarningsTimeOfDay = (sym.EarningsTimeOfDay != null) ? sym.EarningsTimeOfDay.Substring(0,1).ToLower() : "",
                            MarketCap = sym.MarketCap
                        };

                        if (quotes.ContainsKey(sym.Symbol))
                        {
                            DxLink.Quote qt = quotes[sym.Symbol];
                            equity.UnderlyingPrice = qt.LastPrice;
                            equity.UnderlyingPriceChange = qt.Change;
                            if (qt.LastPrice > 0) equity.UnderlyingPriceChangePercent = qt.Change / qt.LastPrice;

                            equity.Name = qt.Description;
                            equity.OptionVolume = qt.Volume / 1000; //this is underlying volume, would prefer option volume
                        }
                        if (equity.EarningsDate < DateTime.Today)
                        {
                            // clear useless data
                            equity.EarningsDate = DateTime.MinValue;
                            equity.EarningsTimeOfDay = "";
                        }
                        if (equity.EarningsDate >= DateTime.Today) equity.DaysUntilEarnings = Math.Truncate((equity.EarningsDate - DateTime.Today).TotalDays);
                        // set visibility to indicate earnings trade in next session
                        if (((equity.EarningsDate == DateTime.Today) && (equity.EarningsTimeOfDay == "a")) ||
                            ((equity.EarningsDate == DateTime.Today.AddDays(1)) && (equity.EarningsTimeOfDay == "b")))
                        {
                            equity.EarningsInNextSession = Visibility.Visible;
                        }

                        foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
                        {
                            TransactionGroup grp = entry.Value;
                            if (grp.Symbol == equity.Symbol)
                                equity.CurrentlyHeld = Visibility.Visible;
                        }

                        this.Add(equity);
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "EquityProfiles");
            }
        }
    }
}
