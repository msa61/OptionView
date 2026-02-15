using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TtTokenService;

namespace OptionView
{

    public class TWMarketInfo
    {
        public string Symbol { get; set; }
        public double ImpliedVolatility { get; set; }
        public double ImpliedVolatilityRank { get; set; }
        public double DividendYield { get; set; }
        public double Beta { get; set; }
        public double CorrelationToSPY { get; set; }
        public DateTime EarningsDate { get; set; }
        public decimal MarketCap { get; set; }
        public string EarningsTimeOfDay { get; set; }
    }
    public class TWMarketInfos : Dictionary<string, TWMarketInfo>
    {
    }



    public class TWAccount
    {
        public string Name { get; set; }
        public string Number { get; set; }
    }
    public class TWAccounts : Dictionary<string, TWAccount>
    {
    }

    public class TWCapitalRequirements : Dictionary<string,decimal>
    {
    }

    public class TWBalance
    {
        public decimal NetLiq { get; set; }
        public decimal OptionBuyingPower { get; set; }
        public decimal EquityBuyingPower { get; set; }
        public decimal CommittedPercentage { get; set; }
    }

    public class TWPosition
    {
        public string Symbol { get; set; }
        public string StreamerSymbol { get; set; }
        public string OptionSymbol { get; set; }
        public string OptionStreamerSymbol { get; set; }
        public DateTime ExpDate { get; set; }
        public decimal Strike { get; set; }
        public string Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Multiplier { get; set; }
        public decimal PreviousClose { get; set; }
        public bool OrderActive { get; set; }

        public TWPosition()
        {
            Quantity = 0;
            OrderActive = false;
        }
    }
    public class TWPositions : Dictionary<string, TWPosition>
    {
    }

    public class TWTransaction
    {
        public DateTime Time { get; set; }
        public string TransactionCode { get; set; }
        public string TransactionSubcode { get; set; }
        public string Action { get; set; }
        public Int32 TransID { get; set; }
        public string Symbol { get; set; }
        public string BuySell { get; set; }
        public string OpenClose { get; set; }
        public decimal? Quantity { get; set; }
        public DateTime? ExpireDate { get; set; }
        public decimal? Strike { get; set; }
        public string InsType { get; set; }
        public decimal? Price { get; set; }
        public decimal? Fees { get; set; }
        public decimal? Amount { get; set; }
        public string Description { get; set; }
        public string AccountRef { get; set; }
        public string TwSymbol { get; set; }

        public override string ToString()
        {
            string ret = Time.ToString() + ",";
            ret += TransactionCode.ToString() + ",";
            ret += TransactionSubcode.ToString() + ",";
            ret += TransID.ToString() + ",";
            ret += (Symbol != null ? Symbol.ToString() : "") + ",";
            ret += (BuySell != null ? BuySell.ToString() : "") + ",";
            ret += (OpenClose != null ? OpenClose.ToString() : "") + ",";
            ret += Quantity.ToString() + ",";
            ret += ExpireDate.ToString() + ",";
            ret += Strike.ToString() + ",";
            ret += (InsType != null ? InsType.ToString() : "") + ",";
            ret += Price.ToString() + ",";
            ret += Fees.ToString() + ",";
            ret += Amount.ToString() + ",";
            ret += Description.ToString() + ",";
            ret += AccountRef.ToString();
            return ret;
        }
    }
    public class TWTransactions : List<TWTransaction>
    {
    }

    public class StreamingParams
    {
        public string Address { get; set; }
        public string Token { get; set; }
    }



    public class TastyWorks
    {
        static TokenService TokenService= null;
        //static private string Token = "";
        //static EncodedWebClient Web = null;
        static bool alreadyFailedOnce = false;

        public TastyWorks()
        {
        }

        public static void ResetToken()
        {
            TokenService.GetAccessToken();
            alreadyFailedOnce = false;
        }

        public static bool InitiateSession( string clientSecret, string grantToken )
        {
            try
            {
                App.UpdateStatusMessage("InitiateSession");

                if (alreadyFailedOnce) return false;

                TokenService = new TokenService("https://api.tastyworks.com/", clientSecret, grantToken);

                return (TokenService.GetAccessToken().Length > 0);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                alreadyFailedOnce = true;
                MessageBoxResult yesNo = MessageBox.Show(e.Message + "\n\nContinue?", "InitiateSession Error", MessageBoxButton.YesNo);
                if (yesNo == MessageBoxResult.No) System.Windows.Application.Current.Shutdown();
            }
            return false;
        }

        public static bool ActiveSession()
        {
            try
            {
                 return (TokenService.GetAccessToken().Length > 0);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
            return false;
        }

        public static TWMarketInfos MarketInfo(List<string> symbols)
        {
            try
            {
                App.UpdateStatusMessage("TW MarketInfo");

                string symbolString = "";
                foreach (string sym in symbols)
                {
                    symbolString += sym + ",";
                }
                symbolString = symbolString.TrimEnd(',');

                JObject package = ExecuteGet("https://api.tastyworks.com/market-metrics?symbols=" + symbolString);

                TWMarketInfos returnList = new TWMarketInfos();

                List<JToken> list = package["data"]["items"].Children().ToList();

                foreach (JToken item in list)
                {
                    TWMarketInfo info = new TWMarketInfo
                    {
                        Symbol = item["symbol"].ToString(),
                        ImpliedVolatility = Convert.ToDouble(item["implied-volatility-index"]),
                        ImpliedVolatilityRank = Convert.ToDouble(item["implied-volatility-index-rank"]),
                        DividendYield = Convert.ToDouble(item["dividend-yield"]),
                        Beta = Convert.ToDouble(item["beta"]),
                        CorrelationToSPY = Convert.ToDouble(item["corr-spy-3month"]),
                        MarketCap = Convert.ToDecimal(item["market-cap"]) / 1E9m
                    };

                    JToken earnings = item["earnings"];
                    if (earnings != null)
                    {
                        if (earnings["expected-report-date"] != null) info.EarningsDate = Convert.ToDateTime(earnings["expected-report-date"]).Trim(TimeSpan.TicksPerDay);
                        if (earnings["time-of-day"] != null) info.EarningsTimeOfDay = earnings["time-of-day"].ToString();
                    } 

                    returnList.Add(info.Symbol, info);
                }

                return (returnList.Count > 0) ? returnList : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW MarketInfo");
                throw new Exception("Error in Tastyworks.MarketInfo", ex);
            }
        }



        public static TWAccounts Accounts()
        {
            try 
            { 
                App.UpdateStatusMessage("TW Accounts");

                JObject package = ExecuteGet("https://api.tastyworks.com/customers/me/accounts");


                TWAccounts returnList = new TWAccounts();

                List<JToken> list = package["data"]["items"].Children().ToList();

                foreach (JToken item in list)
                {
                    TWAccount inst = new TWAccount();
                    inst.Number = item["account"]["account-number"].ToString();
                    inst.Name = item["account"]["nickname"].ToString();
                    returnList.Add(inst.Number, inst);
                }

                return (returnList.Count > 0) ? returnList : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW Accounts");
                throw new Exception("Error in Tastyworks.Accounts", ex);
            }
        }

        // used for determining capital requirement during initial load
        public static TWCapitalRequirements MarginData(string accountNumber)
        {
            try
            { 
                App.UpdateStatusMessage("TW MarginData : " + accountNumber);

                JObject package = ExecuteGet("https://api.tastyworks.com/margin/accounts/" + accountNumber + "/requirements");


                List<JToken> list = package["data"]["groups"].Children().ToList();

                TWCapitalRequirements retval = new TWCapitalRequirements();

                foreach (JToken item in list)
                {
                    string key = item.Value<string>("underlying-symbol");
                    if (key == null) key = item["description"].ToString();
                    
                    retval.Add(key, Convert.ToDecimal(item["maintenance-requirement"]));
                }

                return retval;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW MarginData");
                throw new Exception("Error in Tastyworks.MarginData", ex);
            }
        }


        public static TWBalance Balances(string accountNumber)
        {
            try
            { 
                App.UpdateStatusMessage("TW Balances : " + accountNumber);
                if (TokenService == null) return new TWBalance();

                JObject package = ExecuteGet("https://api.tastyworks.com/accounts/" + accountNumber + "/balances");


                TWBalance retval = new TWBalance()
                {
                    NetLiq = Convert.ToDecimal(package["data"]["net-liquidating-value"]),
                    EquityBuyingPower = Convert.ToDecimal(package["data"]["equity-buying-power"]),
                    OptionBuyingPower = Convert.ToDecimal(package["data"]["derivative-buying-power"])
                };

                if (retval.NetLiq == 0)
                {
                    retval.CommittedPercentage = 0;
                }
                else
                {
                    retval.CommittedPercentage = 1 - (retval.OptionBuyingPower / retval.NetLiq);
                }
                return retval;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW Balances");
                throw new Exception("Error in Tastyworks.Balances", ex);
            }
        }

        public static TWPositions Positions(string accountNumber)
        {
            try
            { 
                App.UpdateStatusMessage("TW Positions : " + accountNumber);

                // get active orders
                Dictionary<string, Int32> orderIds = ActiveOrders(accountNumber);

                // retrieve specific positions
                JObject package = ExecuteGet("https://api.tastyworks.com/accounts/" + accountNumber + "/positions");


                TWPositions returnList = new TWPositions();

                List<JToken> list = package["data"]["items"].Children().ToList();

                foreach (JToken item in list)
                {
                    TWPosition inst = new TWPosition();
                    inst.Symbol = item["underlying-symbol"].ToString();
                    inst.OptionSymbol = item["symbol"].ToString();
                    inst.Quantity = Convert.ToDecimal(item["quantity"]);
                    if (item["quantity-direction"].ToString() == "Short") inst.Quantity *= -1;
                    DateTime exp = Convert.ToDateTime(item["expires-at"]).Trim(TimeSpan.TicksPerDay);
                    inst.PreviousClose = Convert.ToDecimal(item["close-price"]);
                    if (inst.PreviousClose == 0)  inst.PreviousClose = Convert.ToDecimal(item["average-open-price"]);

                    inst.Multiplier = Convert.ToDecimal(item["multiplier"]); ;
                    inst.OrderActive = orderIds.ContainsKey(inst.Symbol);

                    SymbolDecoder symbol = new SymbolDecoder(inst.OptionSymbol, item["instrument-type"].ToString());
                    inst.Type = symbol.Type;
                    inst.ExpDate = symbol.Expiration;
                    inst.Strike = symbol.Strike;

                    switch (item["instrument-type"].ToString())
                    {
                        case "Equity":
                            inst.StreamerSymbol = inst.Symbol;
                            inst.OptionStreamerSymbol = inst.Symbol;
                            break;
                        case "Equity Option":
                            inst.StreamerSymbol = inst.Symbol;
                            inst.OptionStreamerSymbol = GetStreamingSymbol(inst.OptionSymbol); 
                            break;
                        //case "Future":
                        //    break;
                        case "Future Option":
                            inst.StreamerSymbol = GetStreamingSymbol(inst.Symbol);
                            inst.OptionStreamerSymbol = GetStreamingSymbol(inst.OptionSymbol);
                            break;
                        default:
                            MessageBox.Show("Unknown type: " + item["instrument-type"].ToString() + "TW Positions", "TW Positions");
                            break;
                    }

                    returnList.Add(inst.OptionSymbol.Length > 0 ? inst.OptionSymbol : inst.Symbol, inst);
                }

                return returnList;
                //return (returnList.Count > 0) ? returnList : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW Positions");
                throw new Exception("Error in Tastyworks.Positions", ex);
            }
        }

        public static string GetStreamingSymbol(string symbol)
        {
            string retval = "";
            try
            {
                // inderminate, so skip
                //App.UpdateStatusMessage("TW GetFutureOptionSymbol : " + symbol);

                string url = "https://api.tastyworks.com/instruments/";
                if (symbol.Substring(0,2).IndexOf("/") > -1)
                {
                    /// this is a future
                    if (symbol.Substring(0, 1) == ".")
                        url += "future-options/" + symbol.Replace("/", "%2F");
                    else
                        url += "futures" + symbol;
                }
                else
                {
                    // equity
                    if (symbol.Length > 4)
                        url += "equity-options/" + symbol;
                    else
                        url += "equities/" + symbol;
                }
                JObject package = ExecuteGet(url);


                retval = package["data"]["streamer-symbol"].ToString();

                return retval;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW GetStreamingSymbol");
                throw new Exception("Error in Tastyworks.GetStreamingSymbol", ex);
            }
        }





        public static Dictionary<string,Int32> ActiveOrders(string accountNumber)
        {
            try
            {
                App.UpdateStatusMessage("TW ActiveOrders : " + accountNumber);

                Dictionary<string, Int32> retlist = new Dictionary<string, Int32>();
                if (TokenService == null) return retlist;

                JObject package = ExecuteGet("https://api.tastyworks.com/accounts/" + accountNumber + "/orders/live");


                List<JToken> list = package["data"]["items"].Children().ToList();

                foreach (JToken item in list)
                {
                    string symbol = item["underlying-symbol"].ToString();
                    string status = item["status"].ToString();
                    Int32 id = Convert.ToInt32(item["id"].ToString());

                    if ((status != "Filled") && (status != "Cancelled") && (!retlist.ContainsKey(symbol))) retlist.Add(symbol, id);
                }

                return retlist;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW ActiveOrders");
                throw new Exception("Error in Tastyworks.ActiveOrders", ex);
            }
        }


        // https://api.tastyworks.com/accounts/5WT66789/transactions?start-date=2020-03-12T05:00:00.000Z&end-date=2020-03-19T05:00:00.000Z
        //  https://api.tastyworks.com/accounts/5WT66789/transactions?
        //  start-date=2020-03-19T05:00:00.000Z &
        //  end-date=2020-03-19T05:00:00.000Z &
        //  per-page=250 &
        //  page-offset=0
        public static TWTransactions Transactions(string accountNumber)
        {
            return Transactions(accountNumber, null, null);
        }

        public static TWTransactions Transactions(string accountNumber, DateTime? start, DateTime? end)
        {
            try
            { 
                App.UpdateStatusMessage("TW Transations");

                string url = "https://api.tastyworks.com/accounts/" + accountNumber + "/transactions?";
                if (start != null) url += "start-date=" + String.Format("{0:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}", start) + "&";
                if (end != null) url += "end-date=" + String.Format("{0:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}", end);

                JObject package = ExecuteGet(url);


                TWTransactions returnList = new TWTransactions();

                Int32 pages = Convert.ToInt32( package["pagination"]["total-pages"] );
                Int32 pageOffset = 0;
                List<JToken> list = package["data"]["items"].Children().ToList();

                do
                {
                    foreach (JToken item in list)
                    {
                        //Debug.WriteLine(item.ToString());

                        TWTransaction inst = new TWTransaction();
                        inst.TransID = Convert.ToInt32(item["id"]);
                        inst.Time = Convert.ToDateTime(item["executed-at"]).ToUniversalTime();

                        inst.TransactionCode = item["transaction-type"].ToString();
                        inst.TransactionSubcode = item["transaction-sub-type"].ToString();
                        if (item["action"] != null) inst.Action = item["action"].ToString();
                        inst.Description = item["description"].ToString();
                        inst.AccountRef = item["account-number"].ToString();

                        inst.Price = Convert.ToDecimal(item["price"]);
                        inst.Fees = Convert.ToDecimal(item["commission"]) + Convert.ToDecimal(item["clearing-fees"]) + Convert.ToDecimal(item["regulatory-fees"]);
                        inst.Amount = Convert.ToDecimal(item["value"]) * ((item["value-effect"].ToString() == "Debit") ? -1 : 1);

                        if ((inst.TransactionCode == "Trade") || (inst.TransactionCode == "Receive Deliver"))
                        {
                            inst.Symbol = item["underlying-symbol"].ToString();
                            inst.TwSymbol = item["symbol"].ToString();
                            inst.Quantity = Convert.ToDecimal(item["quantity"]);

                            SymbolDecoder symbol = new SymbolDecoder(item["symbol"].ToString(), item["instrument-type"].ToString());
                            inst.InsType = symbol.Type;
                            inst.ExpireDate = symbol.Expiration;
                            inst.Strike = symbol.Strike;
                        }
                        if ((inst.TransactionCode == "Money Movement") && (inst.TransactionSubcode == "Dividend"))
                        {
                            inst.TransactionCode = "Dividend";
                            inst.Symbol = item["underlying-symbol"].ToString();
                            inst.InsType = "Dividend";
                            inst.Quantity = 0;
                        }
                        CompleteInstance(inst);

                        returnList.Add(inst);
                    }

                    if (pages > 1)
                    {
                        package = ExecuteGet(url + "&page-offset=" + ++pageOffset);

                        list = package["data"]["items"].Children().ToList();
                    }

                    pages--;
                } while (pages > 0);

                return (returnList.Count > 0) ? returnList : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW Transactions");
                throw new Exception("Error in Tastyworks.Transactions", ex);
            }
        }

        private static void CompleteInstance (TWTransaction tr)
        {
            if ((tr.TransactionSubcode == "Sell to Open") || (tr.TransactionSubcode == "Sell to Close")) tr.Quantity *= -1;

            if (tr.TransactionCode == "Receive Deliver")
            {
                if (tr.TransactionSubcode == "Assignment")
                {
                    tr.OpenClose = "Close";
                }
                else if (tr.TransactionSubcode == "Exercise")
                {
                    tr.OpenClose = "Close";
                    tr.Quantity *= -1;
                }
                else if (tr.TransactionSubcode == "Expiration")
                {
                    tr.BuySell = "Expired";
                    tr.OpenClose = "Close";
                }
                else if (tr.TransactionSubcode == "Forward Split")
                {
                    if ((tr.Action == "Sell to Open") || (tr.Action == "Sell to Close")) tr.Quantity *= -1;
                    TransactionParse(tr.Action, tr);
                }
                else
                {
                    // all that's left is Sell to Open and Buy to Open
                    tr.InsType = "Stock";
                    string[] s = tr.TransactionSubcode.Split(' ');
                    if (s.Length == 3)
                    {
                        tr.BuySell = s[0];
                        tr.OpenClose = s[2];
                    }
                }
            }
            else if (tr.TransactionCode == "Trade")
            {
                TransactionParse(tr.TransactionSubcode, tr);
            }

        }

        private static void TransactionParse( string str, TWTransaction tr)
        {
            switch (str.ToLower())
            {
                case "buy to close":
                    tr.BuySell = "Buy";
                    tr.OpenClose = "Close";
                    break;
                case "buy to open":
                    tr.BuySell = "Buy";
                    tr.OpenClose = "Open";
                    break;
                case "sell to close":
                    tr.BuySell = "Sell";
                    tr.OpenClose = "Close";
                    break;
                case "sell to open":
                    tr.BuySell = "Sell";
                    tr.OpenClose = "Open";
                    break;
            }
        }


        public static StreamingParams StreamingInfo()
        {
            try
            { 
                App.UpdateStatusMessage("TW StreamingInfo");

                JObject package = ExecuteGet("https://api.tastyworks.com/api-quote-tokens");


                StreamingParams strmParams = new StreamingParams();

                JToken data = package["data"];
                strmParams.Address = data["dxlink-url"].ToString();
                strmParams.Token = data["token"].ToString();

                return strmParams;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW StreamingInfo");
                throw new Exception("Error in Tastyworks.StreamingInfo", ex);
            }
        }

        public static List<string> WatchListSymbols()
        {
            try
            { 
                App.UpdateStatusMessage("TW WatchListSymbols");
                if (TokenService == null) return null;

                List<string> retlist = new List<string>() { "BSY" };

                JObject package = ExecuteGet("https://api.tastyworks.com/public-watchlists");


                List<JToken> list = package["data"]["items"].Children().ToList();

                foreach (JToken item in list)
                {
                    // capture the value of all of the options plus the underlaying
                    //JToken prices = item["marks"];
                    //Debug.WriteLine("watchlist: " + item["name"]);
                    string name = item["name"].ToString();
                    if ((name == "High Options Volume") ||
                        (name == "Dividend Aristocrats") ||
                        (name == "Liquid ETFs") ||
                        (name == "tasty Hourly Top Equities") ||
                        (name == "S&P 500") ||
                        (name == "NASDAQ 100") 
                        )
                    {
                        JToken entries = item["watchlist-entries"];
                        if (entries != null)
                        {
                            foreach (JToken entry in entries)
                            {
                                string symbol = entry["symbol"].ToString();
                                if (!retlist.Contains(symbol)) retlist.Add(symbol);
                            }
                        }
                    }
                }

                return retlist;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "TW WatchListSymbols");
                throw new Exception("Error in Tastyworks.WatchListSymbols", ex);
            }
        }



        public static JObject ExecuteGet(string url)
        {
            return ExecuteGetAsync(url).GetAwaiter().GetResult();
        }

        private static async Task<JObject> ExecuteGetAsync(string url)
        {
            var token = TokenService.GetAccessToken();

            var options = new RestClientOptions()
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            var client = new RestClient(options);
            var request = new RestRequest(url, Method.Get);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = await client.ExecuteAsync(request);

            return (response.Content != null) ? JObject.Parse(response.Content) : null;
        }
    }
}
