using MathNet.Numerics;
using OptionView.DataImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public double DaysUntilEarnings { get; set; }
    }



    public class EquityProfiles : List<EquityProfile>
    {
        public EquityProfiles()
        {
            App.InitializeStatusWindow(7);
            List<string> symbols = TastyWorks.WatchListSymbols();
            if (symbols.Count > 0)
            {
                TWMarketInfos symbolData = TastyWorks.MarketInfo(symbols);
                App.UpdateStatusMessage("Retrieving Dx profiles");
                Dictionary<string, string> descriptions = DataFeed.GetProfiles(symbols);
                App.UpdateStatusMessage("Retrieving Dx volumes");
                Dictionary<string, double> volumes = DataFeed.GetVolumes(symbols);
                App.UpdateStatusMessage("Retrieving Dx prices");
                Dictionary<string, Quote> prices = DataFeed.GetPrices(symbols);

                App.UpdateStatusMessage("Reconciling data");
                foreach (KeyValuePair<string, TWMarketInfo> item in symbolData)
                {
                    TWMarketInfo sym = item.Value;

                    EquityProfile equity = new EquityProfile()
                    {
                        Symbol = sym.Symbol,
                        ImpliedVolatility = sym.ImpliedVolatility,
                        ImpliedVolatilityRank = sym.ImpliedVolatilityRank,
                        EarningsDate = sym.Earnings
                    };
                    if (descriptions.ContainsKey(sym.Symbol)) equity.Name = descriptions[sym.Symbol];
                    if (volumes.ContainsKey(sym.Symbol)) equity.OptionVolume = volumes[sym.Symbol] / 1000;
                    if (prices.ContainsKey(sym.Symbol))
                    {
                        Quote qt = prices[sym.Symbol];
                        equity.UnderlyingPrice = qt.Price;
                        equity.UnderlyingPriceChange = qt.Change;
                        equity.UnderlyingPriceChangePercent = qt.Change / qt.Price;
                    }
                    if (equity.EarningsDate < DateTime.Now) equity.EarningsDate = DateTime.MinValue;  // clear useless data
                    if (equity.EarningsDate > DateTime.Now) equity.DaysUntilEarnings = Math.Truncate((equity.EarningsDate - DateTime.Now).TotalDays);
                    this.Add(equity);
                }
            }
            App.CloseStatusWindow();
        }
    }
}
