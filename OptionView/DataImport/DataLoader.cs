using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using FileHelpers;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.IO;
using System.Security.Principal;
using DxLink;
using System.Web.Util;


namespace OptionView
{
    public class DataLoader
    {
        private static Dictionary<string, TWPositions> twpositions = null;
        private static Dictionary<string, TWCapitalRequirements> twMarginData = null;

        public static void Load(Accounts accounts)
        {
            try
            {
                // backup transaction db for debugging purposes
                File.Copy("transactions.sqlite", "transactions-backup.sqlite", true);

                // establish db connection
                App.OpenConnection();

                if (TastyWorks.InitiateSession(Config.GetEncryptedProp("Username"), Config.GetEncryptedProp("Password")))
                {
                    // cache the current positions for details required to establish default risk and capreq
                    twpositions = new Dictionary<string, TWPositions>();
                    twMarginData = new Dictionary<string, TWCapitalRequirements>();
                    foreach (Account a in accounts)
                    {
                        if (a.Active)
                        {
                            // retrieve Tastyworks positions for given account
                            TWPositions pos = TastyWorks.Positions(a.ID);
                            twpositions.Add(a.ID, pos);

                            // retrieve the current capital requirement and underlying price
                            TWCapitalRequirements mar = TastyWorks.MarginData(a.ID);
                            twMarginData.Add(a.ID, mar);
                        }
                    }

                    // proceed with transactions from all accounts
                    foreach (Account a in accounts)
                    {
                        if (a.Active)
                        {
                            App.Logger.Debug("Load account: " + a.ID.ToString());

                            // retrieve Tastyworks transactions for the past month
                            TWTransactions transactions = TastyWorks.Transactions(a.ID, DateTime.Today.AddDays(-30), null);

                            int addCnt = SaveTransactions(transactions);  // transfer transaction array to database

                            App.Logger.Info(addCnt.ToString() + " transactions added to the database");

                            // save latest transaction for next upload
                            SQLiteCommand cmd = new SQLiteCommand("SELECT max(time) FROM transactions WHERE Account = @ac", App.ConnStr);
                            cmd.Parameters.AddWithValue("ac", a.ID);
                            SQLiteDataReader rdr = cmd.ExecuteReader();
                            string propName = "LastDate-" + a.ID;
                            if (rdr.Read()) Config.SetProp(propName, rdr[0].ToString());
                        }
                    }

                    UpdateNewTransactions();  // matches unassociated assignments and exercises

                }
                else
                {
                    MessageBox.Show("Login to TastyWorks failed", "Error");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("ERROR Load method: " + ex.Message);
                MessageBox.Show(ex.Message, "Sync Error");
            }
        }


        // write transactions loaded from webservice into the database
        //
        private static int SaveTransactions(TWTransactions transactions)
        {
            int returnCount = 0;

            try
            {
                SQLiteCommand cmd;

                if (transactions.Count() == 0) return 0;

                string propName = "LastDate-" + transactions[0].AccountRef;
                DateTime lastDate = Config.GetDateProp(propName);

                int maxLoadID = DBUtilities.GetMax("SELECT Max(LoadGroupID) FROM transactions");
                int newLoadID = maxLoadID + 1; 

                //(new SQLiteCommand("DELETE FROM transactions WHERE 1=1", conn)).ExecuteNonQuery();

                string sql = "INSERT INTO transactions(Time, LoadGroupID, TransType, TransSubType, TransID, Symbol, 'Buy-Sell', 'Open-Close', Quantity, ExpireDate, Strike, Type, Price, Fees, Amount, Description, Account, UnderlyingPrice, TwSymbol)"
                        + " Values(@tm,@lg,@tt,@tst,@tid,@sym,@buy,@op,@qu,@exp,@str,@ty,@pr,@fe,@am,@des,@acc,@up,@tw)";

                SQLiteTransaction sqlTransaction = App.ConnStr.BeginTransaction();

                foreach (var record in transactions)
                {
                    if (record.Time.Trim(TimeSpan.TicksPerSecond) > lastDate)
                    {
                        cmd = new SQLiteCommand(sql, App.ConnStr);
                        cmd.Parameters.AddWithValue("tm", record.Time.Trim(TimeSpan.TicksPerSecond));
                        cmd.Parameters.AddWithValue("lg", newLoadID);  // load group id
                        cmd.Parameters.AddWithValue("tt", record.TransactionCode);
                        cmd.Parameters.AddWithValue("tst", record.TransactionSubcode);
                        cmd.Parameters.AddWithValue("tid", record.TransID);
                        cmd.Parameters.AddWithValue("sym", record.Symbol);
                        cmd.Parameters.AddWithValue("buy", record.BuySell);
                        cmd.Parameters.AddWithValue("op", record.OpenClose);
                        cmd.Parameters.AddWithValue("qu", record.Quantity);
                        cmd.Parameters.AddWithValue("exp", record.ExpireDate);
                        cmd.Parameters.AddWithValue("str", record.Strike);
                        cmd.Parameters.AddWithValue("ty", record.InsType);
                        cmd.Parameters.AddWithValue("pr", record.Price);
                        cmd.Parameters.AddWithValue("fe", record.Fees);
                        cmd.Parameters.AddWithValue("am", record.Amount);
                        cmd.Parameters.AddWithValue("des", record.Description);
                        cmd.Parameters.AddWithValue("acc", record.AccountRef);
                        cmd.Parameters.AddWithValue("tw", record.TwSymbol);
                        decimal underlyingPrice = 0;
                        if (record.Symbol != null)  // need to skip for non-security transactions
                        {
                            string symbol = record.Symbol;
                            if (symbol.Substring(0,1) == "/") symbol = TastyWorks.GetStreamingSymbol(symbol);
                            Quote q = App.DxHandler.GetQuote(symbol).Result;
                            underlyingPrice = q.Price;
                        }
                        cmd.Parameters.AddWithValue("up", underlyingPrice);

                        App.Logger.Debug(String.Format("Adding record: {0} {1} {2}", record.Time.Trim(TimeSpan.TicksPerSecond), record.TransID, record.Description));
                        cmd.ExecuteNonQuery();
                        returnCount++;
                    }
                }

                App.Logger.Debug("Save Transaction: committing new records");
                sqlTransaction.Commit();

            }
            catch (Exception ex)
            {
                App.Logger.Error("Save Transaction: " + ex.Message);
                if (ex.GetType() == typeof(AggregateException))
                {
                    AggregateException exs = (AggregateException)ex;
                    for (int i = 0; i < exs.InnerExceptions.Count; i++)
                    {
                        App.Logger.Error("      cont: " + exs.InnerExceptions[i].Message);
                    }
                    App.Logger.Error("      stack:" + exs.StackTrace);
                }
            }

            return returnCount;
        }



        private static void UpdateNewTransactions()
        {
            try
            {
                MatchExpirations();
                UpdateUngroupedTransactions();
            }
            catch (Exception ex)
            {
                App.Logger.Error("UpdateNewTransactions: " + ex.Message);
            }
        }


        // by default, expiration transactions do not include the correct quantity
        // the quantity is always positive regardless of whether a long or short is being removed
        // this function will search for originating transaction to determine quantity
        //
        private static void MatchExpirations()
        {
            string sql = "SELECT id, time, ExpireDate, Strike, Quantity, * FROM transactions WHERE TransGroupID is NULL AND TransSubType = 'Expiration' ORDER BY time";
            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // save props of controlling record
                Int32 row = reader.GetInt32(0);
                DateTime date = reader.GetDateTime(1);
                DateTime expDate = reader.GetDateTime(2);
                decimal strike = reader.GetDecimal(3);
                decimal quantity = reader.GetDecimal(4);
                string account = reader["Account"].ToString();
                string symbol = reader["Symbol"].ToString();
                string type = reader["Type"].ToString();


                SQLiteCommand cmdStk = new SQLiteCommand("SELECT [Buy-Sell] FROM transactions WHERE Account = @ac AND Symbol = @sym AND TransType = 'Trade' AND Type = @ty AND ExpireDate = @ex AND Strike = @st and Time < @tm AND [Open-Close] = 'Open' ORDER BY Time DESC", App.ConnStr);
                cmdStk.Parameters.AddWithValue("ac", account);
                cmdStk.Parameters.AddWithValue("sym", symbol);
                cmdStk.Parameters.AddWithValue("ty", type);
                cmdStk.Parameters.AddWithValue("ex", expDate);
                cmdStk.Parameters.AddWithValue("st", strike);
                cmdStk.Parameters.AddWithValue("tm", date);


                SQLiteDataReader stk = cmdStk.ExecuteReader();
                if (stk.Read())
                {
                    string buySell = stk["Buy-Sell"].ToString();
                    App.Logger.Debug("MatchExpirations: found " + buySell + " related to the " + symbol + " " + type + " " + strike.ToString() + " " + expDate.ToString("MMM-d"));

                    if (buySell == "Buy")
                    {
                        // Update by negating the quantity for buys
                        sql = "UPDATE transactions SET Quantity = @qu WHERE ID=@row";
                        cmdStk = new SQLiteCommand(sql, App.ConnStr);
                        cmdStk.Parameters.AddWithValue("qu", -Math.Abs(quantity));
                        cmdStk.Parameters.AddWithValue("row", row);
                        cmdStk.ExecuteNonQuery();
                    }
                }


            }
        }

        // searches database for transactions without groupID (which only exists on new records
        // for these new records, other transactions are searched to determine if they are part of a roll or a new position
        // 
        private static void UpdateUngroupedTransactions()
        {
            string symbol = "";
            string account = "";

            try
            {
                App.Logger.Debug("Entering UpdateUngroupedTransactions...");
                int i = 0;

                // establish connection
                App.OpenConnection();

                // get list of symbols that aren't in a group yet
                string sql = "SELECT DISTINCT account, symbol from Transactions ";
                sql += "WHERE ((TransGroupID is NULL) OR (TransGroupID = 0)) AND symbol != '' AND TransType != 'Money Movement' ";
                sql += "ORDER BY symbol";
                //string sql = "SELECT DISTINCT symbol from Transactions WHERE TransGroupID is NULL AND symbol = 'AMD' ORDER BY symbol";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    App.Logger.Debug("Ungroupd record# " + ++i);

                    // save props of controlling record
                    account = reader["Account"].ToString();
                    symbol = reader["Symbol"].ToString();
                    App.Logger.Debug("UpdateUngroupedTransactions: Found -> " + account + "/" + symbol);


                    // query all of the transactions in this account, for given symbol that are either part of an open chain or not part of chain yet
                    sql = "SELECT transactions.ID AS ID, transgroup.ID as tID, datetime(Time) AS TransTime, TransType, TransSubType, TransGroupID, datetime(ExpireDate) AS ExpireDate, Strike, Quantity, Type, Price, [Open-Close], Amount";
                    sql += " FROM transactions";
                    sql += " LEFT JOIN transgroup ON transgroupid = transgroup.id";
                    sql += " WHERE transactions.account = @ac AND transactions.symbol = @sym AND (transgroup.Open = 1 OR transgroup.Open IS NULL) AND Transactions.TransType != 'Money Movement'";  // the money movement filter removes dividends
                    sql += " ORDER BY Time";
                    SQLiteCommand cmdTrans = new SQLiteCommand(sql, App.ConnStr);
                    cmdTrans.Parameters.AddWithValue("ac", account);
                    cmdTrans.Parameters.AddWithValue("sym", symbol);

                    // 
                    SQLiteDataAdapter da = new SQLiteDataAdapter(cmdTrans);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    PurgeSymbolChanges(dt);  // remove transactions that don't impact holdings or calculations

                    while (dt.Rows.Count > 0)
                    {
                        App.Logger.Debug("UpdateUngroupedTransactions: Num of rows found: " + dt.Rows.Count.ToString());

                        int numOfRows = dt.Rows.Count;
                        string t = dt.Rows[0]["TransTime"].ToString(); // maintain sqlite date format as a string, initiate process with the earliest time
                        UpdateTransactionChain(account, symbol, dt, t);

                        if (dt.Rows.Count == numOfRows) break;  // nothing happened, so avoid endless loop
                        // conintue with remaining rows to create additional groups
                    }

                }  // end of symbol loop
            }
            catch (Exception ex)
            {
                App.Logger.Error("UpdateUngroupedTransactions: " + ex.Message + "(" + symbol + ")");
            }

        }

        private static void PurgeSymbolChanges(DataTable dt)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow r = dt.Rows[i];

                if ((r["TransType"].ToString() == "Receive Deliver") && (r["TransSubType"].ToString() == "Symbol Change"))
                {
                    r.Delete();
                    dt.AcceptChanges();
                    i--;
                }
            }
        }



        //
        // Header function for setting up recursion
        // starts with data table of all possible transaction, account/symbol and a particular transaction time
        // chain is built from there
        //
        private static void UpdateTransactionChain(string account, string symbol, DataTable dt, string time)
        {
            Positions holdings = new Positions();
            SortedList<string, string> times = new SortedList<string, string>();

            // start recursion
            App.Logger.Debug("UpdateTransactionChain: start recursions for " + account + "/" + symbol);
            UpdateTransactionsInGroup(account, symbol, dt, time, holdings, times);
            App.Logger.Debug("UpdateTransactionChain: First pass of chain completed.  Retrieving manually combined or dividend transactions...");


            // retrieve existing group id
            int groupID = holdings.GroupID();
            App.Logger.Debug("Current chain GroupID: " + groupID.ToString());

            // Collect remaining transactions that have been manually combined (or dividends) as they won't be processed in main path (skip altogether then groupID is 0 as this indicates new group)
            if (groupID > 0)
            {
                DataRow[] remainingRows = dt.Select("TransGroupID = " + groupID.ToString());
                while (remainingRows.Length > 0)
                {
                    string t = remainingRows[0]["TransTime"].ToString(); // maintain sqlite date format as a string 
                    UpdateTransactionsInGroup(account, symbol, dt, t, holdings, times);

                    //refresh
                    remainingRows = dt.Select("TransGroupID = " + groupID.ToString());
                }
            }
            App.Logger.Debug("UpdateTransactionChain: Chain completed for GroupID: " + groupID.ToString());

            try
            {
                if (holdings.Count > 0)  // abort this, there was nothing left in the rowset after the dividends were separately processed
                {
                    if (groupID > 0)  // existing group
                    {
                        // update chain status

                        if (holdings.hasAssignment)
                        {
                            // Add comment and todo when assigned

                            string newComments = "";
                            //get current comment
                            string sqlSel = "SELECT comments FROM TransGroup WHERE ID = @id";
                            SQLiteCommand cmd = new SQLiteCommand(sqlSel, App.ConnStr);
                            cmd.Parameters.AddWithValue("id", groupID);
                            SQLiteDataReader reader = cmd.ExecuteReader();
                            if (reader.Read())
                            {
                                string stamp = "Assigned " + holdings.AssignmentDate.ToString("M/d");
                                newComments = reader["Comments"].ToString();
                                if (newComments.IndexOf(stamp) == -1)
                                {
                                    if (newComments.Length > 0) newComments += "\n";
                                    newComments += stamp;

                                    sqlSel = "UPDATE TransGroup SET ActionDate = @dt, Comments = @cm WHERE ID=@row";
                                    cmd = new SQLiteCommand(sqlSel, App.ConnStr);
                                    cmd.Parameters.AddWithValue("dt", DateTime.Today);
                                    cmd.Parameters.AddWithValue("cm", newComments);
                                    cmd.Parameters.AddWithValue("row", groupID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // update regardless of assignments
                        string sql = "UPDATE TransGroup SET Open = @op WHERE ID=@row";
                        SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                        cmdUpd.Parameters.AddWithValue("op", !holdings.IsAllClosed());
                        cmdUpd.Parameters.AddWithValue("row", groupID);
                        cmdUpd.ExecuteNonQuery();
                    }
                    else  // new group
                    {
                        string sql = "INSERT INTO TransGroup(Account, Symbol, Open, Strategy, DefinedRisk, NeutralStrategy, CapitalRequired, OriginalCapRequired, Risk) Values(@ac,@sym,@op,@str,@dr,@ns,@cap,@cap2,@rsk)";
                        SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                        cmd.Parameters.AddWithValue("ac", account);
                        cmd.Parameters.AddWithValue("sym", symbol);
                        cmd.Parameters.AddWithValue("op", !holdings.IsAllClosed());
                        string strat = GuessStrategy(holdings);
                        cmd.Parameters.AddWithValue("str", strat);
                        cmd.Parameters.AddWithValue("dr", DefaultDefinedRisk(strat));
                        cmd.Parameters.AddWithValue("ns", DefaultNeutralStrategy(strat));
                        decimal risk = DefaultRisk(account, strat, holdings);
                        cmd.Parameters.AddWithValue("rsk", DefaultRisk(strat, risk, holdings));

                        // there might not be in entry for positions that have opened and closed between syncs
                        decimal capReq = 0;
                        if (twMarginData[account].ContainsKey(symbol)) capReq = twMarginData[account][symbol];
                        cmd.Parameters.AddWithValue("cap", capReq);
                        cmd.Parameters.AddWithValue("cap2", capReq);
                        cmd.ExecuteNonQuery();

                        groupID = DBUtilities.GetMax("SELECT max(id) FROM TransGroup");
                    }
                }
                else
                {
                    App.Logger.Debug("UpdateTransactionChain: empty pass (only dividends)");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("UpdateTransactionChain: " + ex.Message + "(groupID: " + groupID.ToString() + ")");
            }


            try
            {
                List<int> rows = holdings.GetRowNumbers();
                foreach (int r in rows)  // assign all identified transactions to the the current group
                {
                    // update all of the rows in the chain
                    string sql = "UPDATE transactions SET TransGroupID = @id WHERE ID=@row";
                    SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                    cmdUpd.Parameters.AddWithValue("id", groupID);
                    cmdUpd.Parameters.AddWithValue("row", r);
                    cmdUpd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("ProcessTransactionChain (Updating transactions): " + ex.Message );
            }
        }


        private static string GuessStrategy(Positions positions)
        {
            string retval = "";

            if (positions.Count == 1)
            {
                if (positions.ElementAt(0).Value.Quantity == 0)
                {
                    retval = positions.ElementAt(0).Value.InitialQuantity < 0 ? "Short " : "Long ";
                }
                else
                {
                    retval = positions.ElementAt(0).Value.Quantity < 0 ? "Short " : "Long ";
                }
                retval += positions.ElementAt(0).Value.Type;
            }
            else if (positions.Count == 2)
            {
                if (positions.ElementAt(0).Value.ExpDate != positions.ElementAt(1).Value.ExpDate)
                {
                    retval = "Calendar Spread";
                }
                else if (positions.ElementAt(0).Value.Type == positions.ElementAt(1).Value.Type)
                {
                    if ((positions.ElementAt(0).Value.Quantity + positions.ElementAt(1).Value.Quantity) != 0)
                    {
                        retval = "Ratio " + positions.ElementAt(0).Value.Type + " Spread";
                    }
                    else
                    {
                        retval = "Vertical " + positions.ElementAt(0).Value.Type + " Spread";
                    }
                }
                else if (positions.ElementAt(0).Value.Strike == positions.ElementAt(1).Value.Strike)
                {
                    retval = "Straddle";
                }
                else
                {
                    retval = "Strangle";
                }
            }
            else if (positions.Count == 4)
            {
                decimal sum = 0;
                DateTime expDate = DateTime.MinValue;
                bool dateError = false;

                foreach (KeyValuePair<string, Position> item in positions)
                {
                    Position p = item.Value;
                    sum += p.Quantity;
                    if (expDate == DateTime.MinValue)
                        expDate = p.ExpDate;  // set date first time thru
                    else if (expDate != p.ExpDate)
                        dateError = true;   // set flag if any dates don't match

                    if (sum == 0)
                    {
                        if (dateError)
                            retval = "Calendar Spread";
                        else
                            retval = "Iron Condor";
                    }
                }

            }

            return (retval += "");
        }


        private static int DefaultDefinedRisk(string strat)
        {
            if (strat.Length >= 8)
            {
                strat = strat.Substring(0, 8);
                if ((strat == "Iron Con") || (strat == "Vertical")) return 1;
            }
            return 0;
        }

        private static int DefaultNeutralStrategy(string strat)
        {
            if (strat.Length >= 8)
            {
                strat = strat.Substring(0, 8);
                if ((strat == "Iron Con") || (strat == "Straddle") || (strat == "Strangle")) return 1;
            }
            return 0;
        }

        private static decimal DefaultRisk(string account, string strat, Positions positions)
        {
            try
            {
                if ((strat == "") || positions.Count == 0)
                {
                    App.Logger.Warn("DefaultRisk called without required parameters");
                    return 0;
                }

                decimal multiplier = 100;

                TWPosition twpos = FindTWPosition(account, positions.ElementAt(0).Value);
                if (twpos != null) multiplier = twpos.Multiplier;


                if (strat.Length >= 8)
                {
                    strat = strat.Substring(0, 8);
                    if ((strat == "Iron Con") || (strat == "Vertical"))
                    {
                        Dictionary<string, decimal> strikeRange = new Dictionary<string, decimal> { { "Call", 0 }, { "Put", 0 } };

                        foreach (KeyValuePair<string, Position> item in positions)
                        {
                            Position p = item.Value;
                            strikeRange[p.Type] += (p.Strike * p.Quantity);
                        }
                        if (Math.Abs(strikeRange["Put"]) > Math.Abs(strikeRange["Call"]))
                        {
                            return Math.Abs(strikeRange["Put"]) * multiplier;
                        }
                        else
                        {
                            return Math.Abs(strikeRange["Call"]) * multiplier;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("DefaultRisk: " + ex.Message);
            }
            return 0;
        }

        private static decimal DefaultRisk(string strat, decimal capital, Positions positions)
        {
            if (strat.Length >= 8)
            {
                strat = strat.Substring(0, 8);
                if ((strat == "Iron Con") || (strat == "Vertical"))
                {
                    decimal total = 0;
                    foreach (KeyValuePair<string, Position> item in positions)
                    {
                        Position p = item.Value;
                        total += p.Amount;
                    }
                    return (capital - total);
                }
            }
            return 0;
        }

        private static TWPosition FindTWPosition (string account, Position pos)
        {
            // this loop could be eliminated if the long symbol name gets persisted in database
            foreach (KeyValuePair<string, TWPosition> p in twpositions[account])
            {
                TWPosition twpos = p.Value;
                if ((pos.Symbol == twpos.Symbol) && (pos.Type == twpos.Type) && (pos.Strike == twpos.Strike) && (pos.ExpDate == twpos.ExpDate))
                {
                    return twpos;
                }
            }

            return null;
        }

        private static DataRow[] SelectTimeSpan(DataTable dt, string time, int seconds = 15)
        {
            List<DataRow> retset = new List<DataRow>();
            DateTime cTime = DateTime.Parse(time);
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DateTime rTime = DateTime.Parse(dt.Rows[i]["TransTime"].ToString());
                if ((rTime >= cTime) && (rTime <= cTime.AddSeconds(15))) retset.Add(dt.Rows[i]);
            }
            return retset.ToArray();
        }


        private static void UpdateTransactionsInGroup(string account, string symbol, DataTable dt, string time, Positions holdings, SortedList<string, string> times)
        {
            App.Logger.Debug("Entering UpdateTransactionsInGroup (" + symbol + "   " + time.ToString() + ")");

            // Collect all 'opens' for the selected time
            DataRow[] rows = SelectTimeSpan(dt, time);  // DataRow[] rows = dt.Select("TransTime = '" + time + "'");

            if (rows.Count() == 0) return;  // nothing found
            bool somethingAdded = false;

            for (int i = 0; i < rows.Count(); i++)
            {
                DataRow r = rows[i];
                DateTime expDate = DateTime.MinValue;
                if (r["ExpireDate"] != DBNull.Value) expDate = Convert.ToDateTime(r.Field<string>("ExpireDate").ToString());
                string type = r.Field<string>("Type").ToString();
                decimal strike = 0;
                if (r["Strike"] != DBNull.Value) strike = (decimal)r.Field<Double>("Strike");
                Int32 quantity = (Int32)r.Field<Int64>("Quantity");
                decimal amount = 0;
                if (r["Amount"] != DBNull.Value) amount = (decimal)r.Field<Double>("Amount");
                Int32 row = (Int32)r.Field<Int64>("ID");
                string openClose = "";
                if (r["Open-Close"] != DBNull.Value) openClose = r.Field<string>("Open-Close").ToString();
                int grpID = 0;
                if (r["TransGroupID"] != DBNull.Value) grpID = (int)r.Field<Int64>("TransGroupID");
                string recTime = "";
                if (r["TransTime"] != DBNull.Value) recTime = r.Field<string>("TransTime");

                if (openClose == "Open")
                {
                    bool process = true;
                    if ((type == "Stock") && (r.Field<string>("TransType").ToString() != "Trade"))
                    {
                        // need to handle stock activity at expiration differently since there are 'opens' that can cause 
                        // all other transactions for symbol to be sucked up into this chain
                        decimal price = (decimal)r.Field<Double>("Price");
                        // there is an option with this price already in the chain
                        process = holdings.Includes("", Convert.ToDateTime(time).Date, price);
                    }

                    if (process)
                    {
                        // add transaction to the chain
                        string key = holdings.Add(symbol, type, expDate, strike, quantity, amount, null, row, openClose, grpID, 0);
                        App.Logger.Debug("    Opening transaction added to holdings: " + key + "    " + r.Field<Int64>("Quantity").ToString() + "   " + time.ToString() + "   row: " + r.Field<Int64>("id").ToString());

                        // add the associated time to the hierarchy for chain
                        if (recTime != time) App.Logger.Warn("Record time doesn't match primary query time: " + recTime + " != " + time);
                        if (!times.ContainsKey(recTime)) times.Add(recTime, recTime);
                        somethingAdded = true;
                    }
                }
                else if (type == "Dividend")
                {
                    App.Logger.Debug("UpdateTransactionsInGroup: Dividend found (" + time.ToString() + ")");

                    // find stock of same symbol in an open position
                    string sql = "SELECT t.TransGroupID FROM transactions AS t LEFT JOIN transgroup AS tg ON transgroupid = tg.id WHERE tg.Open = 1 AND t.Account = @ac AND t.Symbol = @sym AND t.Type = 'Stock'";
                    SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("ac", account);
                    cmd.Parameters.AddWithValue("sym", symbol);
                    var obj = cmd.ExecuteScalar();

                    if (obj != DBNull.Value)    // if stock found
                    {
                        int groupID = Convert.ToInt32(obj);

                        // update row with dividend with matching transaction group (this needs to be updated here because this does get attached to a 'holdings' transaction)
                        sql = "UPDATE transactions SET TransGroupID = @id WHERE ID=@row";
                        SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                        cmdUpd.Parameters.AddWithValue("id", groupID);
                        cmdUpd.Parameters.AddWithValue("row", r.Field<Int64>("ID"));
                        cmdUpd.ExecuteNonQuery();
                    }

                    // remove row from table immediately (so that i will be ignored in remaining processing)
                    r.Delete();
                    i--;
                    dt.AcceptChanges();
                    rows = dt.Select("TransTime = '" + time + "'");
                }
            }

            if (!somethingAdded) return;

            // run thru what is found, and search for matching/closing transactions
            for (int i = 0; i < holdings.Count; i++)
            {
                Position p = holdings[holdings.Keys.ElementAt(i)];

                if (p.Quantity != 0)
                {
                    Func<DataRow, bool> func;

                    // define the appropriate linq filter
                    if (p.Type == "Stock")
                        func = r => (r.Field<string>("Type")) == p.Type
                                    && r.Field<string>("Open-Close") == "Close"
                                    && Convert.ToDateTime(r.Field<string>("TransTime")) >= Convert.ToDateTime(time);
                    else
                        func = r => (r.Field<string>("Type")) == p.Type
                                    && (decimal)r.Field<Double>("Strike") == p.Strike
                                    && Convert.ToDateTime(r.Field<string>("ExpireDate")) == p.ExpDate && r.Field<string>("Open-Close") == "Close"
                                    && Convert.ToDateTime(r.Field<string>("TransTime")) >= Convert.ToDateTime(time);

                    var closingRow = dt.AsEnumerable()
                                       .Where(func);

                    // generally should only be one transaction, but occassionaly the closing transaction can split lots accross multiple transactions
                    foreach (DataRow r in closingRow)
                    {
                        bool process = true;
                        if ((r.Field<string>("Type") == "Stock") && (r.Field<string>("TransType").ToString() != "Trade"))
                        {
                            // need to handle stock activity at expiration differently since there are 'closes' that can cause 
                            // all other transactions for symbol to be sucked up into this chain
                            decimal price = (decimal)r.Field<Double>("Price");
                            // there is an option with this price already in the chain
                            process = holdings.Includes("", Convert.ToDateTime(time).Date, price);
                        }

                        if (process)
                        {
                            int grpID = 0;
                            if ((r["TransGroupID"] != DBNull.Value) && (Convert.ToInt64(r["TransGroupID"]) != 0)) grpID = (int)r.Field<Int64>("TransGroupID");

                            string key = holdings.Add(p.Symbol, p.Type, p.ExpDate, p.Strike, (Int32)r.Field<Int64>("Quantity"), (Int32)r.Field<Int64>("ID"), r.Field<string>("Open-Close").ToString(), grpID);
                            App.Logger.Debug("    Closing transaction added to holdings: " + key + "    " + r.Field<Int64>("Quantity").ToString() + "   " + time.ToString() + "   row: " + r.Field<Int64>("id").ToString());

                            if (r.Field<String>("TransSubType") == "Assignment")
                            {
                                holdings.hasAssignment = true;
                                holdings.AssignmentDate = Convert.ToDateTime(r.Field<string>("TransTime")).Date;
                            }

                            // add the associated time to the hierarchy for chain
                            string t = r.Field<string>("TransTime");
                            if (!times.ContainsKey(t)) times.Add(t, t);
                        }

                        if (p.Quantity == 0) break;
                    }
                }
            }

            // purge out collected rows from original table with all transactions for a given symbol
            List<int> idList = holdings.GetRowNumbers();
            foreach (int id in idList)
            {
                rows = dt.Select("ID = " + id.ToString());
                for (int i = 0; i < rows.Count(); i++)
                    rows[i].Delete();
                dt.AcceptChanges();
            }

            // recurse for anything else (after the start) in the transaction table that might open new positions
            for (int i = 0; i < times.Count(); i++)
            {
                string t = times[times.Keys[i]];
                if (Convert.ToDateTime(t) > Convert.ToDateTime(time))
                    UpdateTransactionsInGroup(account, symbol, dt, t, holdings, times);
            }

            App.Logger.Debug("Exiting UpdateTransactionsInGroup (" + symbol + "     rows left: " + dt.Rows.Count + ")");

        }
    }

}

