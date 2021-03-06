﻿using System;
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


namespace OptionView
{

    public class Portfolio : Dictionary<int, TransactionGroup>
    {
        private Dictionary<string, TWPositions> twpositions = null;   // cache for current value lookup
        private TWMarketInfos twmarketinfo = null;
        private Accounts accounts = null;

        public Portfolio()
        {
        }


        static public TransactionGroup MapTransactionGroup(SQLiteDataReader reader)
        {
            TransactionGroup grp = new TransactionGroup();

            grp.Account = reader["Account"].ToString();
            grp.AccountName = reader["Name"].ToString().SafeSubstring(0, 4);
            grp.Symbol = reader["Symbol"].ToString();
            // handle options
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
            if (reader["TodoDate"] != DBNull.Value) grp.ActionDate = Convert.ToDateTime(reader["TodoDate"].ToString());  // use the "formatted" version date
            if (reader["Comments"] != DBNull.Value) grp.Comments = reader["Comments"].ToString();
            if (reader["CapitalRequired"] != DBNull.Value) grp.CapitalRequired = Convert.ToDecimal(reader["CapitalRequired"]);
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

        public void GetCurrentHoldings(Accounts acc)
        {
            accounts = acc;

            try
            {
                // always start with an empty list
                this.Clear();
                twpositions = null;

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

                        grp.Holdings.Add(reader["symbol"].ToString(), reader["type"].ToString(), expDate, strike, quantity, amount, transTime, 0, "", 0);
                    }

                    grp.EarliestExpiration = FindEarliestDate(grp.Holdings);
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
            decimal currentValue = 0;
            decimal previousCloseValue = 0;

            try
            {
                // retrieve current data from tastyworks for this a subsequent passes
                if (twpositions == null)
                {
                    if (TastyWorks.InitiateSession(Config.GetEncryptedProp("Username"), Config.GetEncryptedProp("Password")))
                    {
                        List<string> symbols = new List<string>();

                        twpositions = new Dictionary<string, TWPositions>();
                        foreach (KeyValuePair<string, string> a in accounts)
                        {
                            // retrieve Tastyworks positions for given account
                            TWPositions pos = TastyWorks.Positions(a.Key);
                            twpositions.Add(a.Key, pos);

                            foreach (KeyValuePair<string, TWPosition> p in pos)
                            {
                                if (!symbols.Contains(p.Value.Symbol)) symbols.Add(p.Value.Symbol);
                            }
                        }

                        twmarketinfo = TastyWorks.MarketInfo(symbols);  // get IV's
                    }
                }

                // ensure that positions got instanciated AND that the particular account isn't empty
                if ((twpositions != null) && (twpositions.Count > 0) && (twpositions[grp.Account] != null))
                {
                    foreach (KeyValuePair<string, Position> item in grp.Holdings)
                    {
                        Position pos = item.Value;

                        // this loop could be eliminated if the long symbol name gets persisted in database
                        foreach (KeyValuePair<string,TWPosition> p in twpositions[grp.Account])
                        {
                            TWPosition twpos = p.Value;
                            if ((pos.Symbol == twpos.Symbol) && (pos.Type == twpos.Type) && (pos.Strike == twpos.Strike) && (pos.ExpDate == twpos.ExpDate))
                            {
                                //Debug.WriteLine(twpos.Market);
                                currentValue += pos.Quantity * twpos.Market;
                                previousCloseValue += pos.Quantity * twpos.PreviousClose * twpos.Multiplier;

                                // capture current details while we have it
                                pos.Market = twpos.Market;
                                pos.Multiplier = twpos.Multiplier;
                                pos.UnderlyingPrice = twpos.UnderlyingPrice;
                            }
                        }
                    }
                }

                grp.CurrentValue = currentValue;
                grp.PreviousCloseValue = previousCloseValue;
                grp.ChangeFromPreviousClose = currentValue - previousCloseValue;
                if (twmarketinfo.ContainsKey(grp.ShortSymbol))
                {
                    grp.ImpliedVolatility = twmarketinfo[grp.ShortSymbol].ImpliedVolatility;
                    grp.ImpliedVolatilityRank = twmarketinfo[grp.ShortSymbol].ImpliedVolatilityRank;
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
            if (TastyWorks.InitiateSession(Config.GetEncryptedProp("Username"), Config.GetEncryptedProp("Password")))
            {
                overallPositions = new Dictionary<string, TWPositions>();
                foreach (KeyValuePair<string, string> a in accounts)
                {
                    // retrieve Tastyworks positions for given account
                    TWPositions pos = TastyWorks.Positions(a.Key);
                    overallPositions.Add(a.Key, pos);
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
                        if (reader["Strategy"] != DBNull.Value) grp.Strategy = reader["Strategy"].ToString();
                        if (reader["ExitStrategy"] != DBNull.Value) grp.ExitStrategy = reader["ExitStrategy"].ToString();
                        if (reader["EarningsTrade"] != DBNull.Value) grp.EarningsTrade = (Convert.ToInt32(reader["EarningsTrade"]) == 1);
                        if (reader["NeutralStrategy"] != DBNull.Value) grp.NeutralStrategy = (Convert.ToInt32(reader["NeutralStrategy"]) == 1);
                        if (reader["Comments"] != DBNull.Value) grp.Comments = reader["Comments"].ToString();

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
