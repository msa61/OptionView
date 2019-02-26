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

namespace OptionView
{
    public class DataLoader
    {
        private static Transaction[] transactions = null;


        public static void Load(string filename)
        {
            try
            {
                transactions = ParseInputFile(filename);

                // establish connection
                App.OpenConnection();

                SaveTransactions();  // transfer transaction array to database
                UpdateNewTransactions();  // matches unassociated asignments and exercises

                // save latest transaction for next upload
                SQLiteCommand cmd = new SQLiteCommand("SELECT max(time) FROM transactions", App.ConnStr);
                SQLiteDataReader rdr = cmd.ExecuteReader();
                if (rdr.Read()) Config.SetProp("LastDate", rdr[0].ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR DataLoader: " + ex.Message);
            }
        }

        private static Transaction[] ParseInputFile(string filename)
        {
            FileHelperEngine<Transaction> engine = new FileHelperEngine<Transaction>();
            Transaction[] records = engine.ReadFile(filename);

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
                        // nothing to do
                    }
                    else if (record.TransactionSubcode == "Exercise")
                    {
                        record.Quantity *= -1;
                    }
                    else if (record.TransactionSubcode == "Expiration")
                    {
                        // parse the description that is available
                        string pattern = @"(\w+)\sof\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)";
                        string[] substrings = Regex.Split(record.Description, pattern, RegexOptions.IgnoreCase);

                        if (substrings.Count() != 8) Console.Write("ERROR");

                        record.BuySell = "Expired";
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
                        string pattern = @"(\w+)\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)\s\@\s([0-9\.]+)";
                        string[] substrings = Regex.Split(record.Description, pattern, RegexOptions.IgnoreCase);

                        if (substrings.Count() != 9) Console.Write("ERROR");


                        if (substrings[1] == "Bought") record.BuySell = "Buy";
                        if (substrings[1] == "Sold") record.BuySell = "Sell";
                        record.ExpireDate = Convert.ToDateTime(substrings[4]);

                        if (record.TransactionSubcode.IndexOf("Close") > 0) record.OpenClose = "Close";
                        if (record.TransactionSubcode.IndexOf("Open") > 0) record.OpenClose = "Open";

                        record.Strike = Convert.ToDecimal(substrings[6]);
                    }
                    else
                    {
                        // stock transaction
                        record.InsType = "Stock";
                    }
                }

            }

            return records;
        }


        private static void SaveTransactions()
        {
            try
            {
                SQLiteCommand cmd;

                DateTime lastDate = Config.GetDateProp("LastDate");

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
                        cmd.Parameters.AddWithValue("acc", record.AccountRef);

                        cmd.ExecuteNonQuery();
                    }
                }

                sqlTransaction.Commit();

            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: Save Transaction: " + ex.Message);
            }


        }


        private static void UpdateNewTransactions()
        {
            try
            {
                MatchAssignmentsOrExercises();
                MatchExpirations();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR UpdateNewRecords: " + ex.Message);
            }
        }

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
                string symbol = reader["Symbol"].ToString();
                string type = reader["Type"].ToString();
                string tranType = reader["TransSubType"].ToString();
                Int32 quantity = reader.GetInt32(2);
                Debug.WriteLine(tranType + " found " + tranType + " -> " + symbol + ":" + type + " on " + date.ToString() + " (row:" + row + ")");

                // find out how many other matching transactions might have happened on the same day as the Assignment/Exercise in question
                SQLiteCommand cmdStk = new SQLiteCommand("SELECT Count(*) FROM transactions WHERE Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                cmdStk.Parameters.AddWithValue("sym", symbol);
                cmdStk.Parameters.AddWithValue("tt", ((tranType == "Assignment") ^ (type == "Call")) ? "Buy" : "Sell");
                cmdStk.Parameters.AddWithValue("tm", date);
                cmdStk.Parameters.AddWithValue("qu", quantity * 100 * ((type == "Call") ? -1 : 1));

                int rows = Convert.ToInt32(cmdStk.ExecuteScalar());
                Debug.WriteLine("found " + rows.ToString() + " related to the " + tranType + " of " + symbol);

                if (rows == 1)
                {
                    // retrieve the strike from the cooresponding transaction to use to find originating transaction
                    cmdStk = new SQLiteCommand("SELECT price FROM transactions WHERE Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                    cmdStk.Parameters.AddWithValue("sym", symbol);
                    cmdStk.Parameters.AddWithValue("tt", ((tranType == "Assignment") ^ (type == "Call")) ? "Buy" : "Sell");
                    cmdStk.Parameters.AddWithValue("tm", date);
                    cmdStk.Parameters.AddWithValue("qu", quantity * 100 * ((type == "Call") ? -1 : 1));
                    SQLiteDataReader stk = cmdStk.ExecuteReader();
                    stk.Read();

                    decimal price = stk.GetDecimal(0);
                    Debug.WriteLine("  found a " + tranType == "Assignment" ? "Buy" : "Sell" + " for " + symbol + " price: " + price.ToString());

                    // Find originating transaction for that strike/type in order to get the right expiration date
                    cmdStk = new SQLiteCommand("SELECT datetime(expireDate) AS ExpireDate FROM transactions WHERE Symbol = @sym AND Type = @ty AND Strike = @st", App.ConnStr);
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
                    Debug.WriteLine("  which expired on " + expDate.ToString());

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

        private static void MatchExpirations()
        {
            string sql = "SELECT id, time, ExpireDate, Strike, Quantity, * FROM transactions WHERE TransSubType = 'Expiration' ORDER BY time";
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
                string symbol = reader["Symbol"].ToString();
                string type = reader["Type"].ToString();


                SQLiteCommand cmdStk = new SQLiteCommand("SELECT [Buy-Sell] FROM transactions WHERE Symbol = @sym AND TransType = 'Trade' AND Type = @ty AND ExpireDate = @ex AND Strike = @st and Time < @tm AND [Open-Close] = 'Open' ORDER BY Time DESC", App.ConnStr);
                cmdStk.Parameters.AddWithValue("sym", symbol);
                cmdStk.Parameters.AddWithValue("ty", type);
                cmdStk.Parameters.AddWithValue("ex", expDate);
                cmdStk.Parameters.AddWithValue("st", strike);
                cmdStk.Parameters.AddWithValue("tm", date);


                SQLiteDataReader stk = cmdStk.ExecuteReader();
                if (stk.Read())
                {
                    string buySell = stk["Buy-Sell"].ToString();
                    Debug.WriteLine("found " + buySell + " related to the " + symbol + " " + type + " " + strike.ToString() + " " + expDate.ToString());

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
    }
}

