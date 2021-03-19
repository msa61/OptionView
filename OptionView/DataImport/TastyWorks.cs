using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OptionView
{
    // override required to decrypt replies
    class EncodedWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            return request;
        }
    }

    class TWMarketInfo
    {
        public string Symbol { get; set; }
        public double ImpliedVolatility { get; set; }
        public double ImpliedVolatilityRank { get; set; }
    }
    class TWMarketInfos : Dictionary<string, TWMarketInfo>
    {
    }



    class TWAccount
    {
        public string Name { get; set; }
        public string Number { get; set; }
    }
    class TWAccounts : Dictionary<string, TWAccount>
    {
    }

    class TWMargin
    {
        public string Symbol { get; set; }
        public decimal CapitalRequirement { get; set; }
    }
    class TWMargins : Dictionary<string,TWMargin>
    {
    }

    class TWBalance
    {
        public decimal NetLiq { get; set; }
        public decimal OptionBuyingPower { get; set; }
        public decimal EquityBuyingPower { get; set; }
    }

    class TWPosition
    {
        public string Symbol { get; set; }
        public string OptionSymbol { get; set; }
        public DateTime ExpDate { get; set; }
        public decimal Strike { get; set; }
        public string Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Market { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal Multiplier { get; set; }

        public TWPosition()
        {
            Quantity = 0;
        }
    }
    class TWPositions : Dictionary<string, TWPosition>
    {
    }

    class TWTransaction
    {
        public DateTime Time { get; set; }
        public string TransactionCode { get; set; }
        public string TransactionSubcode { get; set; }
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
    class TWTransactions : List<TWTransaction>
    {
    }



    class TastyWorks
    {
        static private string Token = "";
        static EncodedWebClient Web = null;
        static bool alreadyFailedOnce = false;

        public TastyWorks()
        {
        }

        public static void ResetToken()
        {
            Token = "";
            alreadyFailedOnce = false;
        }

        public static bool InitiateSession( string user, string password )
        {
            try
            {
                if (alreadyFailedOnce) return false;
                if (Token.Length > 0) return true;  // no need to login again

                Web = new EncodedWebClient();
                SetHeaders();

                string reply = Web.UploadString("https://api.tastyworks.com/sessions", "{ \"login\": \"" + user + "\", \"password\": \"" + password + "\" }");
                JObject package = JObject.Parse(reply);

                Token = package["data"]["session-token"].ToString();

                return (Token.Length > 0);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                alreadyFailedOnce = true;
            }
            return false;
        }


        public static TWMarketInfos MarketInfo(List<string> symbols)
        {
            string symbolString = "";
            foreach (string sym in symbols)
            {
                symbolString += sym + ",";
            }
            symbolString = symbolString.TrimEnd(',');

            Web.Headers[HttpRequestHeader.Authorization] = Token;
            string reply = Web.DownloadString("https://api.tastyworks.com/market-metrics?symbols=" + symbolString);

            JObject package = JObject.Parse(reply);

            TWMarketInfos returnList = new TWMarketInfos();

            List<JToken> list = package["data"]["items"].Children().ToList();

            foreach (JToken item in list)
            {
                TWMarketInfo info = new TWMarketInfo();
                info.Symbol = item["symbol"].ToString();
                info.ImpliedVolatility = Convert.ToDouble(item["implied-volatility-index"]);
                info.ImpliedVolatilityRank = Convert.ToDouble(item["implied-volatility-index-rank"]);
                returnList.Add(info.Symbol, info);
            }

            return (returnList.Count > 0) ? returnList : null;
        }



        public static TWAccounts Accounts()
        {
            Web.Headers[HttpRequestHeader.Authorization] = Token;
            string reply = Web.DownloadString("https://api.tastyworks.com/customers/me/accounts");

            JObject package = JObject.Parse(reply);

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


        // used for determining capital requirement during initial load
        public static TWMargins MarginData(string accountNumber)
        {
            SetHeaders();
            Web.Headers[HttpRequestHeader.Authorization] = Token;
            string reply = Web.DownloadString("https://api.tastyworks.com/margin/accounts/" + accountNumber);

            JObject package = JObject.Parse(reply);

            List<JToken> list = package["data"]["underlyings"].Children().ToList();

            TWMargins retval = new TWMargins();

            foreach (JToken item in list)
            {
                TWMargin mar = new TWMargin();
                mar.Symbol = item["underlying-symbol"].ToString();
                mar.CapitalRequirement = Convert.ToDecimal(item["margin-requirement"]);
                retval.Add(mar.Symbol, mar);
            }

            return retval;
        }


        public static TWBalance Balances(string accountNumber)
        {
            SetHeaders();
            Web.Headers[HttpRequestHeader.Authorization] = Token;
            string reply = Web.DownloadString("https://api.tastyworks.com/accounts/" + accountNumber + "/balances");

            JObject package = JObject.Parse(reply);

            TWBalance retval = new TWBalance();

            retval.NetLiq = Convert.ToDecimal( package["data"]["net-liquidating-value"] );
            retval.EquityBuyingPower = Convert.ToDecimal(package["data"]["equity-buying-power"]);
            retval.OptionBuyingPower = Convert.ToDecimal(package["data"]["derivative-buying-power"]);

            return retval;
        }

        public static TWPositions Positions(string accountNumber)
        {
            Dictionary<string, decimal> marketValues = new Dictionary<string, decimal>();

            SetHeaders();
            Web.Headers[HttpRequestHeader.Authorization] = Token;

            // retrieve current values
            string reply = Web.DownloadString("https://api.tastyworks.com/margin/accounts/" + accountNumber);
            JObject package = JObject.Parse(reply);

            List<JToken> list = package["data"]["underlyings"].Children().ToList();

            foreach (JToken item in list)
            {
                JToken underlaying = item["marks"];

                foreach (JProperty symbol in underlaying)
                {
                    if (!marketValues.ContainsKey(symbol.Name)) marketValues.Add(symbol.Name, Convert.ToDecimal(symbol.Value));
                }
            }


            // retrieve specific positions
            reply = Web.DownloadString("https://api.tastyworks.com/accounts/" + accountNumber + "/positions");
            package = JObject.Parse(reply);

            TWPositions returnList = new TWPositions();

            list = package["data"]["items"].Children().ToList();

            foreach (JToken item in list)
            {
                TWPosition inst = new TWPosition();
                inst.Symbol = item["underlying-symbol"].ToString();
                inst.OptionSymbol = item["symbol"].ToString();
                inst.Quantity = Convert.ToDecimal(item["quantity"]);
                if (item["quantity-direction"].ToString() == "Short") inst.Quantity *= -1;
                DateTime exp = Convert.ToDateTime(item["expires-at"]).Trim(TimeSpan.TicksPerDay);

                inst.Multiplier = Convert.ToDecimal(item["multiplier"]); ;
                inst.Market = marketValues[inst.OptionSymbol] * inst.Multiplier;
                inst.UnderlyingPrice = marketValues[inst.Symbol];

                SymbolDecoder symbol = new SymbolDecoder(inst.OptionSymbol, item["instrument-type"].ToString());
                inst.Type = symbol.Type;
                inst.ExpDate = symbol.Expiration;
                inst.Strike = symbol.Strike;

                returnList.Add(inst.OptionSymbol.Length > 0 ? inst.OptionSymbol : inst.Symbol, inst);
            }


            return (returnList.Count > 0) ? returnList : null;
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
            SetHeaders();
            Web.Headers[HttpRequestHeader.Authorization] = Token;

            string url = "https://api.tastyworks.com/accounts/" + accountNumber + "/transactions?";
            if (start != null) url += "start-date=" + String.Format("{0:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}", start) + "&";
            if (end != null) url += "end-date=" + String.Format("{0:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}", end);

            string reply = Web.DownloadString(url);
            //Debug.WriteLine(reply);
            JObject package = JObject.Parse(reply);

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
                    inst.Time = Convert.ToDateTime(item["executed-at"]);
                    inst.TransactionCode = item["transaction-type"].ToString();
                    inst.TransactionSubcode = item["transaction-sub-type"].ToString();
                    inst.Description = item["description"].ToString();
                    inst.AccountRef = item["account-number"].ToString();

                    inst.Price = Convert.ToDecimal(item["price"]);
                    inst.Fees = Convert.ToDecimal(item["commission"]) + Convert.ToDecimal(item["clearing-fees"]) + Convert.ToDecimal(item["regulatory-fees"]);
                    inst.Amount = Convert.ToDecimal(item["value"]) * ((item["value-effect"].ToString() == "Debit") ? -1 : 1);

                    if ((inst.TransactionCode == "Trade") || (inst.TransactionCode == "Receive Deliver"))
                    {
                        inst.Symbol = item["underlying-symbol"].ToString();
                        inst.Quantity = Convert.ToDecimal(item["quantity"]);

                        SymbolDecoder symbol = new SymbolDecoder(item["symbol"].ToString(), item["instrument-type"].ToString());
                        inst.InsType = symbol.Type;
                        inst.ExpireDate = symbol.Expiration;
                        inst.Strike = symbol.Strike;
                    }
                    CompleteInstance(inst);

                    returnList.Add(inst);
                }

                if (pages > 1)
                {
                    reply = Web.DownloadString(url + "&page-offset=" + ++pageOffset);
                    package = JObject.Parse(reply);
                    list = package["data"]["items"].Children().ToList();
                }

                pages--;
            } while (pages > 0);

            return (returnList.Count > 0) ? returnList : null;
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
                if (tr.TransactionSubcode.IndexOf("Buy") >= 0) tr.BuySell = "Buy";
                if (tr.TransactionSubcode.IndexOf("Sell") >= 0) tr.BuySell = "Sell";
                if (tr.TransactionSubcode.IndexOf("Close") >= 0) tr.OpenClose = "Close";
                if (tr.TransactionSubcode.IndexOf("Open") >= 0) tr.OpenClose = "Open";
            }

        }


        private static void SetHeaders ()
        {
            Web.Headers[HttpRequestHeader.ContentType] = "application/json";
            Web.Headers[HttpRequestHeader.AcceptEncoding] = "gzip, deflate, br";
        }

    }
}
