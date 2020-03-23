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

namespace OptionView
{
    public class DataLoader
    {
        private static TransactionLoader[] transactions = null;


        public static void Load(string filename)
        {
            try
            {
                transactions = ParseInputFile(filename);

                // establish connection
                App.OpenConnection();

                Int32 accountNum = SaveTransactions();  // transfer transaction array to database
                UpdateNewTransactions();  // matches unassociated asignments and exercises

                // save latest transaction for next upload
                SQLiteCommand cmd = new SQLiteCommand("SELECT max(time) FROM transactions WHERE Account = @ac", App.ConnStr);
                cmd.Parameters.AddWithValue("ac", accountNum);
                SQLiteDataReader rdr = cmd.ExecuteReader();
                string propName = "LastDate-" + accountNum;
                if (rdr.Read()) Config.SetProp(propName, rdr[0].ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR DataLoader: " + ex.Message);
                MessageBox.Show(ex.Message, "Load Error");
            }
        }

        private static TransactionLoader[] ParseInputFile(string filename)
        {
            FileHelperEngine<TransactionLoader> engine = new FileHelperEngine<TransactionLoader>();
            TransactionLoader[] records = engine.ReadFile(filename);

            foreach (var record in records)
            {
                //global changes
                if (record.InsType == "C") record.InsType = "Call";
                if (record.InsType == "P") record.InsType = "Put";
                if ((record.TransactionSubcode == "Sell to Open") || (record.TransactionSubcode == "Sell to Close")) record.Quantity *= -1;


                if (record.TransactionCode == "Receive Deliver")
                {
                    if (record.TransactionSubcode == "Assignment")
                    {
                        record.OpenClose = "Close";
                    }
                    else if (record.TransactionSubcode == "Exercise")
                    {
                        record.OpenClose = "Close";
                        record.Quantity *= -1;
                    }
                    else if (record.TransactionSubcode == "Expiration")
                    {
                        // parse the description that is available
                        string pattern = @"(\w+)\sof\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)";
                        string[] substrings = Regex.Split(record.Description, pattern, RegexOptions.IgnoreCase);

                        if (substrings.Count() != 8) Console.Write("ERROR");

                        record.BuySell = "Expired";
                        record.OpenClose = "Close";
                        record.ExpireDate = Convert.ToDateTime(substrings[4]);
                        record.Strike = Convert.ToDecimal(substrings[6]);
                    }
                    else
                    {
                        // all that's left is Sell to Open and Buy to Open
                        record.InsType = "Stock";
                        string[] s = record.TransactionSubcode.Split(' ');
                        if (s.Length == 3)
                        {
                            record.BuySell = s[0];
                            record.OpenClose = s[2];
                        }
                    }

                }
                else if (record.TransactionCode == "Trade")
                {
                    if (record.InsType == "Call" || record.InsType == "Put")
                    {
                        // Find matches.
                        //string pattern = @"(\w+)\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                        //string pattern = @"(\w+)\s(\d+)\s([a-zA-Z0-9_\/]+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                        string pattern = @"(\w+)\s(\d+)\s([a-zA-Z0-9_\/]+)(\s\w+)*\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                        // (\w+)  \s      (\d+)          \s      ([a-zA-Z0-9_\/]+) (\s\w+)*        \s      ([0-9\/]+) \s      (\w+)  \s      ([0-9\.]+) \s     \@ \s      ([0-9\.]+)
                        // {verb} {space} {#ofContracts} {space} {symbol}          {optional word} {space} {date}     {space} {type} {space} {strike}   {space} @ {space} {strike}

                        // examples
                        // Bought 1 ROKU 03/20/20 Put 105.00 @ 8.20
                        // Bought 1 /CLK0 LOK0 04/16/20 Put 46.00 @ 1.1

                        string[] substrings = Regex.Split(record.Description, pattern, RegexOptions.IgnoreCase);

                        int strs = substrings.Count();
                        if (strs == 9)
                        {
                            // equity
                            record.ExpireDate = Convert.ToDateTime(substrings[4]);
                            record.Strike = Convert.ToDecimal(substrings[6]);
                        }
                        else if (strs == 10)
                        {
                            // future
                            record.ExpireDate = Convert.ToDateTime(substrings[5]);
                            record.Strike = Convert.ToDecimal(substrings[7]);
                        }
                        else
                        {
                            Console.WriteLine(record.Description + " failed regex parse");
                        }
                    }
                    else
                    {
                        // stock transaction
                        record.InsType = "Stock";
                    }

                    if (record.TransactionSubcode.IndexOf("Buy") >= 0) record.BuySell = "Buy";
                    if (record.TransactionSubcode.IndexOf("Sell") >= 0) record.BuySell = "Sell";
                    if (record.TransactionSubcode.IndexOf("Close") >= 0) record.OpenClose = "Close";
                    if (record.TransactionSubcode.IndexOf("Open") >= 0) record.OpenClose = "Open";

                }

            }

            return records;
        }

        // write transactions loaded from cvs into the database
        //
        private static Int32 SaveTransactions()
        {
            Int32 accountNum = 0;
            try
            {
                SQLiteCommand cmd;

                if (transactions.Count() == 0) return 0;

                if (transactions[0].AccountRef == "Individual...89")
                {
                    accountNum = 6789;
                }
                else if (transactions[0].AccountRef == "Roth IRA...60")
                {
                    accountNum = 1760;
                }
                else
                {
                    accountNum = Convert.ToInt32(transactions[0].AccountRef.Substring(transactions[0].AccountRef.Length - 2));
                }

                string propName = "LastDate-" + accountNum;
                DateTime lastDate = Config.GetDateProp(propName);

                int maxLoadID = DBUtilities.GetMax("SELECT Max(LoadGroupID) FROM transactions");
                int newLoadID = maxLoadID + 1; 

                //(new SQLiteCommand("DELETE FROM transactions WHERE 1=1", conn)).ExecuteNonQuery();

                string sql = "INSERT INTO transactions(Time, LoadGroupID, TransType, TransSubType, SecID, Symbol, 'Buy-Sell', 'Open-Close', Quantity, ExpireDate, Strike, Type, Price, Fees, Amount, Description, Account)"
                        + " Values(@tm,@lg,@tt,@tst,@sid,@sym,@buy,@op,@qu,@exp,@str,@ty,@pr,@fe,@am,@des,@acc)";

                SQLiteTransaction sqlTransaction = App.ConnStr.BeginTransaction();

                foreach (var record in transactions)
                {
                    if (record.Time > lastDate)
                    {
                        cmd = new SQLiteCommand(sql, App.ConnStr);
                        cmd.Parameters.AddWithValue("tm", record.Time);
                        cmd.Parameters.AddWithValue("lg", newLoadID);  // load group id
                        cmd.Parameters.AddWithValue("tt", record.TransactionCode);
                        cmd.Parameters.AddWithValue("tst", record.TransactionSubcode);
                        cmd.Parameters.AddWithValue("sid", record.SecurityID);
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
                        cmd.Parameters.AddWithValue("acc", accountNum);

                        cmd.ExecuteNonQuery();
                    }
                }

                sqlTransaction.Commit();

            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: Save Transaction: " + ex.Message);
            }

            return accountNum;
        }


        private static void UpdateNewTransactions()
        {
            try
            {
                MatchAssignmentsOrExercises();
                MatchExpirations();
                UpdateTransactionGroups();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR UpdateNewRecords: " + ex.Message);
            }
        }


        // by default, transactions do not have the strike price and expiration date correctly set if the happen to be closed due to assignment or exercise
        // this function will search for associated transaction to flesh these values out
        //
        private static void MatchAssignmentsOrExercises()
        {
            // null strike indicates a new record that hasn't been matched to its cooresponding/resulting transaction
            // retrieve all cases
            string sql = "SELECT id, time, quantity, * FROM transactions WHERE (TransSubType = 'Assignment' OR TransSubType = 'Exercise') AND Strike is NULL ORDER BY time";
            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // save props of controlling record
                Int32 row = reader.GetInt32(0);
                DateTime date = reader.GetDateTime(1);
                Int32 account = Convert.ToInt32(reader["Account"]);
                string symbol = reader["Symbol"].ToString();
                string type = reader["Type"].ToString();
                string tranType = reader["TransSubType"].ToString();
                Int32 quantity = reader.GetInt32(2);
                Debug.WriteLine(tranType + " found " + tranType + " -> " + account + "/" + symbol + ":" + type + " on " + date.ToString() + " (row:" + row + ")");

                // find out how many other matching transactions might have happened on the same day as the Assignment/Exercise in question
                SQLiteCommand cmdStk = new SQLiteCommand("SELECT Count(*) FROM transactions WHERE Account = @ac AND Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                cmdStk.Parameters.AddWithValue("ac", account);
                cmdStk.Parameters.AddWithValue("sym", symbol);
                cmdStk.Parameters.AddWithValue("tt", ((tranType == "Assignment") ^ (type == "Call")) ? "Buy" : "Sell");
                cmdStk.Parameters.AddWithValue("tm", date);
                cmdStk.Parameters.AddWithValue("qu", quantity * 100 * ((type == "Call") ? -1 : 1));

                int rows = Convert.ToInt32(cmdStk.ExecuteScalar());
                Debug.WriteLine("found " + rows.ToString() + " related to the " + tranType + " of " + symbol);

                if (rows == 1)
                {
                    // retrieve the strike from the cooresponding transaction to use to find originating transaction
                    cmdStk = new SQLiteCommand("SELECT price FROM transactions WHERE Account = @ac AND Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                    cmdStk.Parameters.AddWithValue("ac", account);
                    cmdStk.Parameters.AddWithValue("sym", symbol);
                    cmdStk.Parameters.AddWithValue("tt", ((tranType == "Assignment") ^ (type == "Call")) ? "Buy" : "Sell");
                    cmdStk.Parameters.AddWithValue("tm", date);
                    cmdStk.Parameters.AddWithValue("qu", quantity * 100 * ((type == "Call") ? -1 : 1));
                    SQLiteDataReader stk = cmdStk.ExecuteReader();
                    stk.Read();

                    decimal price = stk.GetDecimal(0);
                    Debug.WriteLine("  found a " + (tranType == "Assignment" ? "Buy" : "Sell") + " for " + symbol + " price: " + price.ToString());

                    // Find originating transaction for that strike/type in order to get the right expiration date
                    cmdStk = new SQLiteCommand("SELECT datetime(expireDate) AS ExpireDate FROM transactions WHERE Account = @ac AND Symbol = @sym AND Type = @ty AND Strike = @st", App.ConnStr);
                    cmdStk.Parameters.AddWithValue("ac", account);
                    cmdStk.Parameters.AddWithValue("sym", symbol);
                    cmdStk.Parameters.AddWithValue("st", price);
                    cmdStk.Parameters.AddWithValue("ty", type);

                    // there may be more than one instance of sym/type/strike
                    SQLiteDataAdapter da = new SQLiteDataAdapter(cmdStk);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    bool found = false;
                    int i = 0;
                    DateTime expDate = DateTime.MinValue;
                    while ((!found) && (i < dt.Rows.Count))
                    {
                        // let's look for row where the expire data matches the date the assignment/exercise happen
                        expDate = Convert.ToDateTime(dt.Rows[i][0]);
                        if (date.Date == expDate) found = true;
                        i++;
                    }

                    // if no match between transaction date and expire date, use whatever we can find
                    if (!found && (dt.Rows.Count == 1))
                        expDate = Convert.ToDateTime(dt.Rows[0][0]);

                    //stk = cmdStk.ExecuteReader();
                    //stk.Read();
                    //DateTime expDate = stk.GetDateTime(0);
                    Debug.WriteLine("  which expired on " + expDate.ToString("MMM-d"));

                    if (expDate > DateTime.MinValue)
                    {
                        // Update the Put/Call transaction in order to match up later
                        sql = "UPDATE transactions SET ExpireDate = @ex, Strike = @st WHERE ID=@row";
                        cmdStk = new SQLiteCommand(sql, App.ConnStr);
                        cmdStk.Parameters.AddWithValue("ex", expDate);
                        cmdStk.Parameters.AddWithValue("st", price);
                        cmdStk.Parameters.AddWithValue("row", row);
                        cmdStk.ExecuteNonQuery();
                    }
                }
                else
                {
                    // either nothing found, error?   or multiple/complex
                    Debug.WriteLine("multiple assignments for " + symbol);
                }
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
                Int32 account = Convert.ToInt32(reader["Account"]);
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
                    Debug.WriteLine("MatchExpirations: found " + buySell + " related to the " + symbol + " " + type + " " + strike.ToString() + " " + expDate.ToString("MMM-d"));

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
        private static void UpdateTransactionGroups()
        {
            string symbol = "";
            Int32 account = 0;

            try
            {
                // establish connection
                App.OpenConnection();

                string sql = "SELECT DISTINCT account, symbol from Transactions WHERE TransGroupID is NULL AND symbol != '' AND TransType != 'Money Movement' ORDER BY symbol";
                //string sql = "SELECT DISTINCT symbol from Transactions WHERE TransGroupID is NULL AND symbol = 'AMD' ORDER BY symbol";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {

                    // save props of controlling record
                    account = Convert.ToInt32(reader["Account"]);
                    symbol = reader["Symbol"].ToString();
                    Debug.WriteLine("UpdateTransactionGroups: Found -> " + account + "/" + symbol);


                    // query all of the transactions in this account, for given symbol that are either part of an open chain or not part of chain yet
                    sql = "SELECT transactions.ID AS ID, transgroup.ID as tID, datetime(Time) AS TransTime, TransType, TransGroupID, datetime(ExpireDate) AS ExpireDate, Strike, Quantity, Type, Price, [Open-Close], Amount";
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

                    while (dt.Rows.Count > 0)
                    {
                        int numOfRows = dt.Rows.Count;
                        string t = dt.Rows[0]["TransTime"].ToString(); // maintain sqlite date format as a string 
                        ProcessTransactionChain(account, symbol, dt, t);

                        if (dt.Rows.Count == numOfRows) break;  // nothing happened, so avoid endless loop
                    }


                }  // end of symbol loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateTransactionGroups: " + ex.Message + "(" + symbol + ")");
            }

        }



        //
        // Header function for setting up recursion
        // starts with data table of all possible transaction, account/symbol and a particular transaction time
        // chain is built from there
        //
        private static void ProcessTransactionChain(Int32 account, string symbol, DataTable dt, string time)
        {
            Positions holdings = new Positions();
            SortedList<string, string> times = new SortedList<string, string>();

            // start recursion
            ProcessTransactionGroup(account, symbol, dt, time, holdings, times);
            Debug.WriteLine("First pass of chain completed.  Retrieving manually matched transactions...");


            // retrieve existing group id
            int groupID = holdings.GroupID();
            Debug.WriteLine("GroupID: " + groupID.ToString());

            // Collect remaining transactions that have been manually merged
            if (groupID > 0)
            {
                DataRow[] remainingRows = dt.Select("TransGroupID = " + groupID.ToString());
                while (remainingRows.Length > 0)
                {
                    string t = remainingRows[0]["TransTime"].ToString(); // maintain sqlite date format as a string 
                    ProcessTransactionGroup(account, symbol, dt, t, holdings, times);

                    //refresh
                    remainingRows = dt.Select("TransGroupID = " + groupID.ToString());
                }
            }
            Debug.WriteLine("Chain completed for GroupID: " + groupID.ToString());

            try
            {
                if (groupID > 0)
                {
                    // update chain status
                    string sql = "UPDATE TransGroup SET Open = @op WHERE ID=@row";
                    SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                    cmdUpd.Parameters.AddWithValue("op", !holdings.IsAllClosed());
                    cmdUpd.Parameters.AddWithValue("row", groupID);
                    cmdUpd.ExecuteNonQuery();
                }
                else
                {
                    string sql = "INSERT INTO TransGroup(Account, Symbol, Open, Strategy, DefinedRisk, NeutralStrategy, CapitalRequired, Risk) Values(@ac,@sym,@op,@str,@dr,@ns,@cap,@rsk)";
                    SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("ac", account);
                    cmd.Parameters.AddWithValue("sym", symbol);
                    cmd.Parameters.AddWithValue("op", !holdings.IsAllClosed());
                    string strat = GuessStrategy(holdings);
                    cmd.Parameters.AddWithValue("str", strat);
                    cmd.Parameters.AddWithValue("dr", DefaultDefinedRisk(strat));
                    cmd.Parameters.AddWithValue("ns", DefaultNeutralStrategy(strat));
                    decimal capital = DefaultCapital(strat, holdings);
                    cmd.Parameters.AddWithValue("cap", capital);
                    cmd.Parameters.AddWithValue("rsk", DefaultRisk(strat, capital, holdings));
                    cmd.ExecuteNonQuery();

                    groupID = DBUtilities.GetMax("SELECT max(id) FROM TransGroup");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ProcessTransactionChain (Update/insert TransGroup): " + ex.Message + "(groupID: " + groupID.ToString() + ")");
            }


            try
            {
                List<int> rows = holdings.GetRowNumbers();
                foreach (int r in rows)
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
                Debug.WriteLine("ProcessTransactionChain (Updating transactions): " + ex.Message );
            }
        }


        private static string GuessStrategy(Positions positions)
        {
            string retval = "";

            if (positions.Count == 2)
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

            return (retval += "?");
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

        private static decimal DefaultCapital(string strat, Positions positions)
        {
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
                        return Math.Abs(strikeRange["Put"]) * 100;
                    }
                    else
                    {
                        return Math.Abs(strikeRange["Call"]) * 100;
                    }
                }
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




        private static void ProcessTransactionGroup(Int32 account, string symbol, DataTable dt, string time, Positions holdings, SortedList<string, string> times)
        {
            Debug.WriteLine("Entering ProcessTransactionGroup (" + symbol + ")");

            // Collect all 'opens' for the selected time
            DataRow[] rows = dt.Select("TransTime = '" + time + "'");

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
                string openClose = r.Field<string>("Open-Close").ToString();
                int grpID = 0;
                if (r["TransGroupID"] != DBNull.Value) grpID = (int)r.Field<Int64>("TransGroupID");

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
                        string key = holdings.Add(symbol, type, expDate, strike, quantity, amount, row, openClose, grpID);
                        Debug.WriteLine("    Opening transaction added to holdings: " + key + "    " + r.Field<Int64>("Quantity").ToString() + "   " + time.ToString());

                        // add the associated time to the hierarchy for chain
                        if (!times.ContainsKey(time)) times.Add(time, time);
                        somethingAdded = true;
                    }
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
                            if (r["TransGroupID"] != DBNull.Value) grpID = (int)r.Field<Int64>("TransGroupID");

                            string key = holdings.Add(p.Symbol, p.Type, p.ExpDate, p.Strike, (Int32)r.Field<Int64>("Quantity"), (Int32)r.Field<Int64>("ID"), r.Field<string>("Open-Close").ToString(), grpID);
                            Debug.WriteLine("    Closing transaction added to holdings: " + key + "    " + r.Field<Int64>("Quantity").ToString() + "   " + time.ToString());

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

            // recurse for anything else in the transaction that might open new positions
            for (int i = 0; i < times.Count(); i++)
            {
                string t = times[times.Keys[i]];
                if (Convert.ToDateTime(t) > Convert.ToDateTime(time))
                    ProcessTransactionGroup(account, symbol, dt, t, holdings, times);
            }

            Debug.WriteLine("Exiting ProcessTransactionGroup (" + symbol + ")");

        }
    }
}

