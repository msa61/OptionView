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

    class TWAccount
    {
        public string Name { get; set; }
        public string Number { get; set; }
    }
    class TWAccounts : Dictionary<string, TWAccount>
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
        public DateTime ExpDate { get; set; }
        public decimal Strike { get; set; }
        public string Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Market { get; set; }
        public decimal OpenAmount { get; set; }

        public TWPosition()
        {
            Quantity = 0;
        }
    }
    class TWPositions : List<TWPosition>
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
            SetHeaders();
            Web.Headers[HttpRequestHeader.Authorization] = Token;
            string reply = Web.DownloadString("https://api.tastyworks.com/accounts/" + accountNumber + "/positions");

            JObject package = JObject.Parse(reply);


            TWPositions returnList = new TWPositions();

            List<JToken> list = package["data"]["items"].Children().ToList();

            foreach (JToken item in list)
            {
                TWPosition inst = new TWPosition();
                inst.Symbol = item["underlying-symbol"].ToString();
                inst.Quantity = Convert.ToDecimal(item["quantity"]);
                if (item["quantity-direction"].ToString() == "Short") inst.Quantity *= -1;
                DateTime exp = Convert.ToDateTime(item["expires-at"]).Trim(TimeSpan.TicksPerDay);
                inst.Market = Convert.ToDecimal(item["mark"]);
                if (item["cost-effect"].ToString() == "Debit") inst.Market *= -1;
                inst.OpenAmount = Convert.ToDecimal(item["average-open-price"]);

                if (item["instrument-type"].ToString() == "Equity Option")
                {
                    string symbol = item["symbol"].ToString();
                    //  ROKU  200417P00065000
                    string pattern = @"(.+)\s+(\d{2})(\d{2})(\d{2})(.)(\d{5})(\d+)";
                    string[] subs = Regex.Split(symbol, pattern);
                    if (subs.Count() == 9)
                    {
                        inst.ExpDate = exp;
                        if (subs[5] == "C")
                        {
                            inst.Type = "Call";
                        }
                        else if (subs[5] == "P")
                        {
                            inst.Type = "Put";
                        }
                        inst.Strike = Convert.ToDecimal(subs[6]) + Convert.ToDecimal(subs[7]) / 1000;
                    }
                }
                else if (item["instrument-type"].ToString() == "Future Option")
                {
                    string symbol = item["symbol"].ToString();
                    //  ./CLM0 LOM0  200514P15
                    string pattern = @"(.+)\s+(.+)\s+(\d{2})(\d{2})(\d{2})(.)(\d{1,5})";
                    string[] subs = Regex.Split(symbol, pattern);
                    if (subs.Count() == 9)
                    {
                        inst.ExpDate = exp;
                        if (subs[6] == "C")
                        {
                            inst.Type = "Call";
                        }
                        else if (subs[6] == "P")
                        {
                            inst.Type = "Put";
                        }
                        inst.Strike = Convert.ToDecimal(subs[7]);  //todo confirm how they handle non integers
                    }
                }
                else
                {
                    inst.Type = "Stock";
                }

                returnList.Add(inst);
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

            IndentifyMissingTypes(returnList);

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
                    // parse the description that is available
                    string pattern = @"(\w+)\sof\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)";
                    string[] substrings = Regex.Split(tr.Description, pattern, RegexOptions.IgnoreCase);

                    if (substrings.Count() != 8) Console.Write("ERROR");

                    tr.InsType = substrings[5];
                    tr.BuySell = "Expired";
                    tr.OpenClose = "Close";
                    tr.ExpireDate = Convert.ToDateTime(substrings[4]);
                    tr.Strike = Convert.ToDecimal(substrings[6]);
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
                Match m = Regex.Match(tr.Description, @"(Call|Put)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    tr.InsType = m.Value;

                    // Decompose description.
                    //string pattern = @"(\w+)\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                    //string pattern = @"(\w+)\s(\d+)\s([a-zA-Z0-9_\/]+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                    string pattern = @"(\w+)\s(\d+)\s([a-zA-Z0-9_\/]+)(\s\w+)*\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                    // (\w+)  \s      (\d+)          \s      ([a-zA-Z0-9_\/]+) (\s\w+)*        \s      ([0-9\/]+) \s      (\w+)  \s      ([0-9\.]+) \s     \@ \s      ([0-9\.]+)
                    // {verb} {space} {#ofContracts} {space} {symbol}          {optional word} {space} {date}     {space} {type} {space} {strike}   {space} @ {space} {strike}

                    // examples
                    // Bought 1 ROKU 03/20/20 Put 105.00 @ 8.20
                    // Bought 1 /CLK0 LOK0 04/16/20 Put 46.00 @ 1.1

                    string[] substrings = Regex.Split(tr.Description, pattern, RegexOptions.IgnoreCase);

                    int strs = substrings.Count();
                    if (strs == 9)
                    {
                        // equity
                        tr.ExpireDate = Convert.ToDateTime(substrings[4]);
                        tr.Strike = Convert.ToDecimal(substrings[6]);
                    }
                    else if (strs == 10)
                    {
                        // future
                        tr.ExpireDate = Convert.ToDateTime(substrings[5]);
                        tr.Strike = Convert.ToDecimal(substrings[7]);
                    }
                    else
                    {
                        Console.WriteLine(tr.Description + " failed regex parse");
                    }

                }
                else
                {
                    // stock transaction
                    tr.InsType = "Stock";
                }

                if (tr.TransactionSubcode.IndexOf("Buy") >= 0) tr.BuySell = "Buy";
                if (tr.TransactionSubcode.IndexOf("Sell") >= 0) tr.BuySell = "Sell";
                if (tr.TransactionSubcode.IndexOf("Close") >= 0) tr.OpenClose = "Close";
                if (tr.TransactionSubcode.IndexOf("Open") >= 0) tr.OpenClose = "Open";
            }

        }

        private static void IndentifyMissingTypes (TWTransactions trans)
        {
            if (trans.Count == 0) return;

            for (Int32 i = 0; i < trans.Count; i++)
            {
                TWTransaction tr = trans[i];

                if ((tr.TransactionCode == "Receive Deliver") && (tr.InsType is null))
                {
                    // the associated transaction is always next
                    TWTransaction tr2 = trans[i + 1];

                    if (tr.TransactionSubcode == "Exercise")
                    {
                        tr.Strike = tr2.Price;
                        if (tr2.BuySell == "Sell")
                            tr.InsType = "Put";
                        else if (tr2.BuySell == "Buy")
                            tr.InsType = "Call";
                    }
                    else if (tr.TransactionSubcode == "Assignment")
                    {
                        tr.Strike = tr2.Price;
                        if (tr2.BuySell == "Sell")
                            tr.InsType = "Call";
                        else if (tr2.BuySell == "Buy")
                            tr.InsType = "Put";
                    }
                }
            }
        }



        private static void SetHeaders ()
        {
            Web.Headers[HttpRequestHeader.ContentType] = "application/json";
            Web.Headers[HttpRequestHeader.AcceptEncoding] = "gzip, deflate, br";
        }

    }
}
