using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using OptionView.DataImport;

namespace OptionView
{

    public class Portfolio : Dictionary<int, TransactionGroup>
    {
        private Dictionary<string, TWPositions> twPositions = null;      // cache for current value lookup
        private Dictionary<string, TWMargins> twReqCapital = null;       // cache of maintenance requirement values from tw
        private TWMarketInfos twMarketInfo = null;                       // cache of IV data
        private Greeks optionGreeks = null;                              // cache of greek data
        private double SPYPrice = 0;
        public double VIXPrice { get; set; } = 0;
        private Accounts accounts = null;

        public Portfolio()
        {
            if (!App.OfflineMode) TastyWorks.InitiateSession(Config.GetEncryptedProp("Username"), Config.GetEncryptedProp("Password"));
        }


        static public TransactionGroup MapTransactionGroup(SQLiteDataReader reader)
        {
            TransactionGroup grp = new TransactionGroup();

            grp.Account = reader["Account"].ToString();
            grp.AccountName = reader["Name"].ToString().SafeSubstring(0, 4);
            grp.Symbol = reader["Symbol"].ToString();
            // handle futures that have prefix
            if (grp.Symbol.Substring(0,1) == "/")
            {
                grp.ShortSymbol = grp.Symbol.Substring(0, 3);
            }
            else
            {
                grp.ShortSymbol = grp.Symbol;
            }
            grp.GroupID = Convert.ToInt32(reader["ID"]); // readerGroup
            grp.Cost = Convert.ToDecimal(reader["Cost"]);
            grp.Fees = Convert.ToDecimal(reader["Fees"]);
            if (reader["X"] != DBNull.Value) grp.X = Convert.ToInt32(reader["X"]);
            if (reader["Y"] != DBNull.Value) grp.Y = Convert.ToInt32(reader["Y"]);
            if (reader["Strategy"] != DBNull.Value) grp.Strategy = reader["Strategy"].ToString();
            // set default value when read for the first time
            if (reader["ExitStrategy"] == DBNull.Value)
                grp.ExitStrategy = "50% profit";
            else
                grp.ExitStrategy = reader["ExitStrategy"].ToString();
            if (reader["TodoDate"] != DBNull.Value) grp.ActionDate = Convert.ToDateTime(reader["TodoDate"].ToString());  // use the "formatted" ("ToDoDate") version date, actual field is ActionDate
            if (reader["ActionText"] != DBNull.Value) grp.ActionText = reader["ActionText"].ToString();
            if (reader["Comments"] != DBNull.Value) grp.Comments = reader["Comments"].ToString();
            if (reader["CapitalRequired"] != DBNull.Value) grp.CapitalRequired = Convert.ToDecimal(reader["CapitalRequired"]);
            if (reader["OriginalCapRequired"] != DBNull.Value) grp.OriginalCapitalRequired = Convert.ToDecimal(reader["OriginalCapRequired"]);
            if (reader["EarningsTrade"] != DBNull.Value) grp.EarningsTrade = (Convert.ToInt32(reader["EarningsTrade"]) == 1);
            if (reader["NeutralStrategy"] != DBNull.Value) grp.NeutralStrategy = (Convert.ToInt32(reader["NeutralStrategy"]) == 1);
            if (reader["DefinedRisk"] != DBNull.Value) grp.DefinedRisk = (Convert.ToInt32(reader["DefinedRisk"]) == 1);
            if (reader["Risk"] != DBNull.Value) grp.Risk = Convert.ToDecimal(reader["Risk"]);
            if (reader["startTime"] != DBNull.Value) grp.StartTime = Convert.ToDateTime(reader["startTime"].ToString());
            if (reader["endTime"] != DBNull.Value) grp.EndTime = Convert.ToDateTime(reader["endTime"].ToString());
            if (HasColumn(reader, "Year") && (reader["Year"] != DBNull.Value)) grp.Year = Convert.ToInt32(reader["Year"]);

            return grp;
        }

        private static bool HasColumn(DbDataReader reader, string column)
        {
            return (reader.GetOrdinal(column) >= 0);
        }

        public void GetCurrentHoldings(Accounts acc, Boolean minorUpdate = false)
        {
            App.UpdateLoadStatusMessage("Get current holdings");

            accounts = acc;

            try
            {
                if (!minorUpdate)
                {
                    twPositions = null;  // clear cache
                }
                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                string sql = "SELECT *, date(ActionDate) AS TodoDate FROM transgroup AS tg LEFT JOIN";
                sql += " (SELECT transgroupid, SUM(amount) AS Cost, SUM(Fees) AS Fees, datetime(MIN(time)) AS startTime, datetime(MAX(time)) AS endTime from transactions GROUP BY transgroupid) AS t ON tg.id = t.transgroupid";
                sql += " LEFT JOIN accounts AS a ON tg.Account = a.ID";
                sql += " WHERE tg.Open = 1";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader readerGroup = cmd.ExecuteReader();
                while (readerGroup.Read())
                {
                    TransactionGroup grp = MapTransactionGroup(readerGroup);

                    // step thru open holdings
                    sql = "SELECT * FROM (SELECT symbol, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(quantity) AS total, sum(amount) as amount, datetime(Time) AS TransTime FROM transactions";
                    sql += " WHERE (transgroupid = @gr) GROUP BY symbol, type, expiredate, strike) WHERE (total <> 0)";
                    cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("gr", grp.GroupID);

                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        //decimal strike = reader["strike"].ToString();
                        decimal strike = 0m;
                        if (reader["Strike"] != DBNull.Value) strike = Convert.ToDecimal(reader["Strike"]);
                        decimal quantity = 0.0m;
                        if (reader["Total"] != DBNull.Value) quantity = Convert.ToDecimal(reader["Total"]);
                        decimal amount = 0.0m;
                        if (reader["Amount"] != DBNull.Value) amount = Convert.ToDecimal(reader["Amount"]);
                        DateTime expDate = DateTime.MinValue;
                        if (reader["ExpireDate"] != DBNull.Value) expDate = Convert.ToDateTime(reader["ExpireDate"].ToString());
                        DateTime transTime = DateTime.MinValue;
                        if (reader["TransTime"] != DBNull.Value) transTime = Convert.ToDateTime(reader["TransTime"].ToString());
                        transTime = DateTime.SpecifyKind(transTime, DateTimeKind.Utc);

                        grp.Holdings.Add(reader["symbol"].ToString(), reader["type"].ToString(), expDate, strike, quantity, amount, transTime, 0, "", 0, 0);
                    }

                    grp.EarliestExpiration = FindEarliestDate(grp.Holdings);
                    App.UpdateLoadStatusMessage("Retrieve current data - " + grp.Symbol);
                    RetrieveCurrentData(grp);

                    this.Add(grp.GroupID, grp);
                }  // end of transaction group loop

            }
            catch (Exception ex)
            {
                Console.WriteLine("CurrentHoldings: " + ex.Message);
            }

        }

        private DateTime FindEarliestDate(Positions positions)
        {
            DateTime ret = DateTime.MaxValue;

            foreach (KeyValuePair<string, Position> item in positions)
            {
                Position p = item.Value;
                if ((p.ExpDate > DateTime.MinValue) && (p.ExpDate < ret)) ret = p.ExpDate;
            }

            return ret;
        }

        private void RetrieveCurrentData(TransactionGroup grp)
        {
            decimal? currentValue = null;
            decimal previousCloseValue = 0;

            if (App.OfflineMode) return;

            try
            {
                // retrieve and cache current data from tastyworks for this a subsequent passes
                if (twPositions == null)
                {
                    App.UpdateLoadStatusMessage("Fetching current data for cache");

                    if (TastyWorks.ActiveSession())
                    {
                        List<string> symbols = new List<string>();
                        List<string> optionSymbols = new List<string>();

                        twPositions = new Dictionary<string, TWPositions>();
                        twReqCapital = new Dictionary<string, TWMargins>();
                        foreach (Account a in accounts)
                        {
                            if (a.Active)
                            {
                                // retrieve Tastyworks positions for given account
                                TWPositions pos = TastyWorks.Positions(a.ID);
                                twPositions.Add(a.ID, pos);

                                if (pos != null)
                                {
                                    symbols.Add("SPY");
                                    foreach (KeyValuePair<string, TWPosition> p in pos)
                                    {
                                        if (!symbols.Contains(p.Value.Symbol)) symbols.Add(p.Value.Symbol);

                                        // individual option list
                                        if (p.Value.Type.Substring(0, 1) != "S")
                                        {
                                            if (!optionSymbols.Contains(p.Value.ShortOptionSymbol)) optionSymbols.Add(p.Value.ShortOptionSymbol);
                                        }
                                    }
                                }

                                // retreive cap requirements for holdings in this account
                                TWMargins mar = TastyWorks.MarginData(a.ID);
                                twReqCapital.Add(a.ID, mar);
                            }
                        }

                        twMarketInfo = TastyWorks.MarketInfo(symbols);  // get IV's
                        optionGreeks = DataFeed.GetGreeks(optionSymbols);

                        //foreach (KeyValuePair<string, Greek> g in optionGreeks)
                        //{
                        //    Debug.WriteLine("Greek: {0}, Delta{1}", g.Key, g.Value.Delta);
                        //}

                        List<string> pSymbols = new List<string> { "SPY", "VIX" };
                        Dictionary<string, double> prices = DataFeed.GetPrices(pSymbols);
                        if (prices.ContainsKey("SPY")) SPYPrice = prices["SPY"];
                        if (prices.ContainsKey("VIX")) VIXPrice = prices["VIX"];
                        
                    }
                }

                // ensure that positions got instanciated AND that the particular account isn't empty
                if ((twPositions != null) && (twPositions.Count > 0) && (twPositions[grp.Account] != null))
                {
                    foreach (KeyValuePair<string, Position> item in grp.Holdings)
                    {
                        Position pos = item.Value;

                        // this loop could be eliminated if the long symbol name gets persisted in database
                        foreach (KeyValuePair<string,TWPosition> p in twPositions[grp.Account])
                        {
                            TWPosition twpos = p.Value;
                            if ((pos.Symbol == twpos.Symbol) && (pos.Type == twpos.Type) && (pos.Strike == twpos.Strike) && (pos.ExpDate == twpos.ExpDate))
                            {
                                //Debug.WriteLine(twpos.Market);
                                if (currentValue == null) currentValue = 0;  // initialize now that we've found a match
                                currentValue += pos.Quantity * twpos.Market;
                                previousCloseValue += pos.Quantity * twpos.PreviousClose * twpos.Multiplier;

                                // capture current details while we have it
                                pos.Market = twpos.Market;
                                pos.Multiplier = twpos.Multiplier;
                                pos.UnderlyingPrice = twpos.UnderlyingPrice;

                                // capture the underlying price from the first position for the overall group
                                if (grp.UnderlyingPrice == 0) grp.UnderlyingPrice = twpos.UnderlyingPrice;

                                // update groups order status based on any of items constituent holdings
                                grp.OrderActive = twpos.OrderActive;

                                if (optionGreeks.ContainsKey(twpos.ShortOptionSymbol))
                                {
                                    pos.GreekData = optionGreeks[twpos.ShortOptionSymbol];

                                    grp.GreekData.Delta += pos.GreekData.Delta * Decimal.ToDouble(pos.Quantity) * Decimal.ToDouble(pos.Multiplier);
                                    grp.GreekData.Theta += pos.GreekData.Theta * Decimal.ToDouble(pos.Quantity) * Decimal.ToDouble(pos.Multiplier);
                                }

                                break;
                            }
                        }
                    }
                }

                if (currentValue != null)
                {
                    grp.CurrentValue = currentValue;
                    grp.PreviousCloseValue = previousCloseValue;
                    grp.ChangeFromPreviousClose = (currentValue ?? 0) - previousCloseValue;
                }
                if ((twMarketInfo != null) && (twMarketInfo.ContainsKey(grp.ShortSymbol)))
                {
                    grp.ImpliedVolatility = twMarketInfo[grp.ShortSymbol].ImpliedVolatility;
                    grp.ImpliedVolatilityRank = twMarketInfo[grp.ShortSymbol].ImpliedVolatilityRank;
                    grp.DividendYield = twMarketInfo[grp.ShortSymbol].DividendYield;

                    grp.GreekData.WeightedDelta = Convert.ToDouble(grp.UnderlyingPrice) * grp.GreekData.Delta * twMarketInfo[grp.ShortSymbol].Beta / SPYPrice;
                }

                // update current capital requirements from tw
                if (twReqCapital.ContainsKey(grp.Account) & twReqCapital[grp.Account].ContainsKey(grp.Symbol))
                {
                    decimal capReq = twReqCapital[grp.Account][grp.Symbol].CapitalRequirement;
                    if ((capReq > 0) && ((capReq != grp.CapitalRequired)) || (grp.OriginalCapitalRequired == 0))
                    {
                        // this cleans up legacy groups without an original
                        if (grp.OriginalCapitalRequired == 0) grp.OriginalCapitalRequired = grp.CapitalRequired;
                        grp.CapitalRequired = capReq;
                        grp.Update();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RetrieveCurrentData: " + ex.Message);
            }

        }


        public string ValidateCurrentHoldings()
        {
            string returnValue = "";
            Dictionary<string, TWPositions> overallPositions = null;

            // always start with clean data
            if (TastyWorks.ActiveSession())
            {
                overallPositions = new Dictionary<string, TWPositions>();
                foreach (Account a in accounts)
                {
                    if (a.Active)
                    {
                        // retrieve Tastyworks positions for given account
                        TWPositions pos = TastyWorks.Positions(a.ID);
                        overallPositions.Add(a.ID, pos);
                    }
                }
            }
            else
            {
                MessageBox.Show("Login to TastyWorks failed", "Error");
                return "LoginFailed";
            }


            foreach (KeyValuePair<string, TWPositions> item  in overallPositions)
            {
                // cycle thru each account
                TWPositions accountPositions = item.Value;

                // skip if the account is empty of positions
                // and confirm each aligns with what is in current database by
                // iterating thru all the positions in the current account
                // 
                int i = 0;
                while ((accountPositions != null) && (i < accountPositions.Count))
                {
                    TWPosition position = accountPositions.ElementAt(i).Value;

                    // iterate thru each group
                    foreach (KeyValuePair<int, TransactionGroup> grpItem in this)
                    {
                        TransactionGroup grp = grpItem.Value;

                        // examine closer if right account and underlying
                        if ((item.Key == grp.Account) && (position.Symbol == grp.Symbol))
                        {
                            // iterate thru everthing in the group
                            int j = 0;
                            while ((j < grp.Holdings.Count) && (position.Quantity != 0))
                            {
                                Position dbpos = grp.Holdings[grp.Holdings.Keys.ElementAt(j)];

                                // look for matching security
                                if ((position.Type == dbpos.Type) && (position.Strike == dbpos.Strike) && (position.ExpDate == dbpos.ExpDate))
                                {
                                    position.Quantity -= dbpos.Quantity;
                                    grp.Holdings.Remove(grp.Holdings.Keys.ElementAt(j));
                                }
                                else
                                {
                                    j++;
                                }

                            }
                        }

                    }

                    if (position.Quantity == 0)
                    {
                        accountPositions.Remove(accountPositions.ElementAt(i).Key);
                    }
                    else
                    {
                        i++;
                    }
                }
            }


            // catch anything left over in database
            foreach (KeyValuePair<int, TransactionGroup> grpItem in this)
            {
                TransactionGroup grp = grpItem.Value;

                if (grp.Holdings.Count > 0)
                {
                    // something left
                    if (returnValue.Length == 0) returnValue = "Unmatched positons in the database:\n";
                    returnValue += grp.Holdings.ToString();
                }
            }
            // add anything left from the TW query
            foreach (KeyValuePair<string, TWPositions> positionsPair in overallPositions)
            {
                bool firstPass = true;

                // nothing to do if account is empty
                if (positionsPair.Value != null)
                {
                    foreach (KeyValuePair<string,TWPosition> p in positionsPair.Value)
                    {
                        TWPosition pos = p.Value;

                        if (firstPass)
                        {
                            firstPass = false;
                            if (returnValue.Length > 0) returnValue += "\n";
                            returnValue += "Unmatched from TastyWorks account:\n";
                        }
                        returnValue += (pos.Type == "Stock") ? pos.Symbol : pos.Symbol + pos.ExpDate.ToString("yyMMdd") + pos.Strike.ToString("0000.0") + pos.Type + " : " + pos.Quantity.ToString() + "\n";
                    }
                }
            }


            return returnValue;
        }


        public double GetWeightedDelta(string acct = "")
        {
            double retval = 0;
            foreach (KeyValuePair<int, TransactionGroup> grpItem in this)
            {
                TransactionGroup grp = grpItem.Value;
                // don't include earnings trades in the overall weighted delta as it skews the portfolios deltas for core holdings
                if (((grp.Account == acct) || (acct == "")) && (grp.EarningsTrade == false)) retval += grp.GreekData.WeightedDelta;
            }

            return retval;
        }
        public double GetTheta(string acct = "")
        {
            double retval = 0;
            foreach (KeyValuePair<int, TransactionGroup> grpItem in this)
            {
                TransactionGroup grp = grpItem.Value;
                if ((grp.Account == acct) || (acct == "")) retval += grp.GreekData.Theta;
            }

            return retval;
        }
        public decimal GetAccountCapRequired(string acct = "", bool incStock = false)  // incStock == everything
        {
            decimal retval = 0;
            foreach (KeyValuePair<int, TransactionGroup> grpItem in this)
            {
                TransactionGroup grp = grpItem.Value;
                if (((grp.Account == acct) || (acct == "")) && (incStock || !HasStock(grp.Holdings)))  retval += grp.CapitalRequired;
            }

            return retval;
        }
        private bool HasStock(Positions positions)
        {
            foreach (KeyValuePair<string,Position> pos in positions)
            {
                Position p = pos.Value;
                if (p.Type == "Stock") return true;
            }
            return false;
        }
    }


    public class PortfolioResults : List<TransactionGroup>
    {
        public PortfolioResults()
        {
        }

        public void GetResults()
        {
            try
            {

                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                // step thru defined groups
                string sql = "SELECT *, date(ActionDate) AS TodoDate FROM transgroup AS tg LEFT JOIN";
                sql += " (SELECT transgroupid, SUM(amount) AS Cost, SUM(Fees) AS Fees, datetime(MIN(time)) AS startTime, datetime(MAX(time)) AS endTime, STRFTIME(\"%Y\", MIN(time)) AS Year FROM transactions GROUP BY transgroupid) AS t ON tg.id = t.transgroupid";
                sql += " LEFT JOIN accounts AS a ON tg.Account = a.ID";
                sql += " WHERE tg.Open = 0 AND Cost IS NOT NULL";
                sql += " ORDER BY endTime";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader readerGroup = cmd.ExecuteReader();
                while (readerGroup.Read())
                {
                    //Debug.WriteLine("GetResults/Group: " + readerGroup["ID"].ToString());

                    TransactionGroup grp = Portfolio.MapTransactionGroup(readerGroup);

                    // step thru open holdings
                    sql = "SELECT datetime(time) AS time, datetime(expiredate) AS ExpireDate, * FROM transactions WHERE (transgroupid = @gr) ORDER BY time";
                    cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("gr", grp.GroupID);

                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        //Debug.WriteLine("GetResults/Transactions: " + reader["ID"].ToString());
                        string x = reader["ID"].ToString();

                        Transaction t = new Transaction();

                        if (reader["Time"] != DBNull.Value) t.TransTime = Convert.ToDateTime(reader["Time"].ToString());
                        t.TransTime = DateTime.SpecifyKind(t.TransTime, DateTimeKind.Utc);
                        if (reader["Type"] != DBNull.Value) t.Type = reader["Type"].ToString();
                        if (reader["TransSubType"] != DBNull.Value) t.TransType = reader["TransSubType"].ToString();

                        if (reader["Strike"] != DBNull.Value) t.Strike = Convert.ToDecimal(reader["Strike"]);
                        if (reader["Quantity"] != DBNull.Value) t.Quantity = Convert.ToDecimal(reader["Quantity"]);
                        if (reader["Amount"] != DBNull.Value) t.Amount = Convert.ToDecimal(reader["Amount"]);
                        if (reader["ExpireDate"] != DBNull.Value)
                        {
                            t.ExpDate = Convert.ToDateTime(reader["ExpireDate"].ToString());
                            if (t.ExpDate > DateTime.MinValue)  t.ExpDateText = t.ExpDate.ToString("dd MMM yyyy");
                        }

                        if (reader["Description"] != DBNull.Value) grp.TransactionText += reader["Description"].ToString() + System.Environment.NewLine;
                        if (reader["UnderlyingPrice"] != DBNull.Value) t.UnderlyingPrice = Convert.ToDecimal(reader["UnderlyingPrice"]);

                        grp.Transactions.Add(t);

                        // add some transaction stats
                        if ((grp.CapitalRequired > 0) && (grp.Strategy.SafeSubstring(0, 8).ToUpper() != "CALENDAR"))
                        {
                            // don't bother if CapReq not defined
                            grp.Return = grp.Cost / grp.CapitalRequired;

                            // annualize it
                            TimeSpan span = grp.EndTime - grp.StartTime;
                            if (span.TotalMinutes > 0) grp.AnnualReturn = grp.Return * 525600m / (decimal)span.TotalMinutes;  // this error happens when data is corrupted and group is flagged as closed when it only has initial opening transactions
                        }
                    }

                    this.Add(grp);

                }  // end of transaction group loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetResults: " + ex.Message);
            }

        }
        
       
        
    }

 
    public class PortfolioTodos : List<TransactionGroup>
    {
        public PortfolioTodos()
        {
        }

        public void GetTodos()
        {
            try
            {

                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                string sql = "SELECT *, date(ActionDate) AS TodoDate FROM transgroup ";
                sql += " WHERE Open = 1";
                sql += " ORDER BY TodoDate";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    DateTime todoDate = DateTime.MinValue;

                    if (reader["TodoDate"] != DBNull.Value)
                        todoDate = Convert.ToDateTime(reader["TodoDate"].ToString());  // use the "formatted" version date

                    if (todoDate > DateTime.MinValue)
                    {
                        TransactionGroup grp = new TransactionGroup();

                        grp.ActionDate = todoDate;

                        grp.Account = reader["Account"].ToString();
                        grp.Symbol = reader["Symbol"].ToString();
                        grp.GroupID = Convert.ToInt32(reader["ID"]); // readerGroup
                        if (reader["Strategy"] != DBNull.Value) grp.Strategy = reader["Strategy"].ToString();
                        if (reader["ExitStrategy"] != DBNull.Value) grp.ExitStrategy = reader["ExitStrategy"].ToString();
                        if (reader["EarningsTrade"] != DBNull.Value) grp.EarningsTrade = (Convert.ToInt32(reader["EarningsTrade"]) == 1);
                        if (reader["NeutralStrategy"] != DBNull.Value) grp.NeutralStrategy = (Convert.ToInt32(reader["NeutralStrategy"]) == 1);
                        if (reader["Comments"] != DBNull.Value) grp.Comments = reader["Comments"].ToString();
                        if (reader["ActionText"] != DBNull.Value) grp.ActionText = reader["ActionText"].ToString();

                        this.Add(grp);
                    }

                }  // end of transaction group loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetTodos: " + ex.Message);
            }

        }


    }


}
