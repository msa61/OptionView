using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using FileHelpers;
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
                if (App.ConnStr == null) App.ConnStr = new SQLiteConnection("Data Source=transactions.sqlite;Version=3;");
                if (App.ConnStr.State == System.Data.ConnectionState.Closed ) App.ConnStr.Open();
                
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

        private static Transaction[] ParseInputFile( string filename )
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
                    else if ((record.TransactionSubcode == "Exercise") || (record.TransactionSubcode == "Expiration"))
                    {
                        record.Quantity *= -1;
                    }
                    else
                    {
                        // all that's left is Sell to Open and Buy to Open
                        record.InsType = "Stock";
                        string[] s = record.TransactionSubcode.Split(' ');
                        if (s.Length ==3)
                        {
                            record.BuySell = s[0];
                            record.OpenClose = s[2];
                        }
                    }

                    if (record.TransactionSubcode == "Expiration")
                    {
                        // parse the description that is available
                        string pattern = @"(\w+)\sof\s(\d+)\s(\w+)\s([0-9\/]+)\s(\w+)\s([0-9\.]+)";
                        string[] substrings = Regex.Split(record.Description, pattern, RegexOptions.IgnoreCase);

                        if (substrings.Count() != 8) Console.Write("ERROR");

                        record.BuySell = "Expired";
                        record.ExpireDate = Convert.ToDateTime(substrings[4]);
                        record.Strike = Convert.ToDecimal(substrings[6]);
                        record.Quantity *= -1;
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
                        Debug.WriteLine("UNEXPECTED TRADE TYPE: " + record.InsType);
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
                //(new SQLiteCommand("DELETE FROM transactions WHERE 1=1", conn)).ExecuteNonQuery();

                string sql = "INSERT INTO transactions(Time, TransType, TransSubType, SecID, Symbol, 'Buy-Sell', 'Open-Close', Quantity, ExpireDate, Strike, Type, Price, Fees, Amount, Description, Account)"
                        + " Values(@tm,@tt,@tst,@sid,@sym,@buy,@op,@qu,@exp,@str,@ty,@pr,@fe,@am,@des,@acc)";

                SQLiteTransaction sqlTransaction = App.ConnStr.BeginTransaction();

                foreach (var record in transactions)
                {
                    if (record.Time > lastDate)
                    {
                        cmd = new SQLiteCommand(sql, App.ConnStr);
                        cmd.Parameters.AddWithValue("tm", record.Time);
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
                Debug.WriteLine("ERROR: Save Transaction" + ex.Message);
            }


        }


        private static void UpdateNewTransactions()
        {
            try
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
                    Debug.WriteLine(tranType + " found -> " + symbol + ":" + type + " on " + date.ToString() + " (row:" + row + ")");

                    // find out how many other matching transactions might have happened on the same day as the Assignment/Exercise in question
                    SQLiteCommand stkQuery = new SQLiteCommand("SELECT Count(*) FROM transactions WHERE Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                    stkQuery.Parameters.AddWithValue("sym", symbol);
                    stkQuery.Parameters.AddWithValue("tt", tranType == "Assignment" ? "Buy" : "Sell");
                    stkQuery.Parameters.AddWithValue("tm", date);
                    stkQuery.Parameters.AddWithValue("qu", quantity * 100);

                    int rows = Convert.ToInt32(stkQuery.ExecuteScalar());
                    Debug.WriteLine("found " + rows.ToString() + " related to the " + tranType + " of " + symbol);

                    if (rows == 1)
                    {
                        // retrieve the price from the cooresponding transaction to use to find originating transaction
                        stkQuery = new SQLiteCommand("SELECT price FROM transactions WHERE Symbol = @sym AND TransType = 'Receive Deliver' AND [Buy-Sell] = @tt AND time = @tm AND quantity = @qu", App.ConnStr);
                        stkQuery.Parameters.AddWithValue("sym", symbol);
                        stkQuery.Parameters.AddWithValue("tt", tranType == "Assignment" ? "Buy" : "Sell");
                        stkQuery.Parameters.AddWithValue("tm", date);
                        stkQuery.Parameters.AddWithValue("qu", quantity * 100);
                        SQLiteDataReader stk = stkQuery.ExecuteReader();
                        stk.Read();

                        decimal price = stk.GetDecimal(0);
                        Debug.WriteLine("  found a " + tranType == "Assignment" ? "Buy" : "Sell" + " for " + symbol + " price: " + price.ToString());

                        // Find originating transaction for that strike/type in order to get the right expiration date
                        stkQuery = new SQLiteCommand("SELECT expireDate FROM transactions WHERE Symbol = @sym AND Type = @ty AND Strike = @st", App.ConnStr);
                        stkQuery.Parameters.AddWithValue("sym", symbol);
                        stkQuery.Parameters.AddWithValue("st", price);
                        stkQuery.Parameters.AddWithValue("ty", type);

                        stk = stkQuery.ExecuteReader();
                        stk.Read();
                        DateTime expDate = stk.GetDateTime(0);
                        Debug.WriteLine("  which expired on " + expDate.ToString());

                        // Update the Put/Call transaction in order to match up later
                        sql = "UPDATE transactions SET ExpireDate = @ex, Strike = @st WHERE ID=@row";
                        stkQuery = new SQLiteCommand(sql, App.ConnStr);
                        stkQuery.Parameters.AddWithValue("ex", expDate);
                        stkQuery.Parameters.AddWithValue("st", price);
                        stkQuery.Parameters.AddWithValue("row", row);
                        stkQuery.ExecuteNonQuery();
                    }
                    else
                    {
                        // either nothing found, error?   or multiple/complex
                        Debug.WriteLine("multiple assignments for " + symbol);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR UpdateNewRecords: " + ex.Message);
            }
        }
    }
}

