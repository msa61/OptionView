using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Diagnostics;


namespace OptionView
{
    public class HoldingsHelper
    {

        public static void Query()
        {
            try
            {
                // establish connection
                App.OpenConnection();

                string sql = "SELECT DISTINCT symbol from Transactions WHERE TransGroupID is NULL AND symbol != '' ORDER BY symbol";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Positions holdings = new Positions();

                    // save props of controlling record
                    string symbol = reader["Symbol"].ToString();
                    Debug.WriteLine("Found -> " + symbol);

                    sql = "SELECT DISTINCT Time FROM transactions WHERE symbol = @sym GROUP BY Time, Type, Strike, ExpireDate ORDER BY time";
                    SQLiteCommand timeCmd = new SQLiteCommand(sql, App.ConnStr);
                    timeCmd.Parameters.AddWithValue("sym", symbol);
                    SQLiteDataReader timeReader = timeCmd.ExecuteReader();
                    while (timeReader.Read())
                    {
                        DateTime transTime = timeReader.GetDateTime(0);
                        Debug.WriteLine("  Transaction Time -> " + transTime.ToString());

                        sql = "SELECT ID, ExpireDate, Strike, Quantity, Type FROM transactions WHERE symbol = @sym AND Time = @tm";
                        SQLiteCommand transCmd = new SQLiteCommand(sql, App.ConnStr);
                        transCmd.Parameters.AddWithValue("sym", symbol);
                        transCmd.Parameters.AddWithValue("tm", transTime);

                        SQLiteDataReader transReader = transCmd.ExecuteReader();
                        while (transReader.Read())
                        {
                            Int32 row = transReader.GetInt32(0);
                            DateTime expDate = (transReader.GetValue(1) == DBNull.Value) ? DateTime.MinValue : transReader.GetDateTime(1);
                            decimal strike = (transReader.GetValue(2) == DBNull.Value) ? 0 : transReader.GetDecimal(2);
                            decimal quantity = transReader.GetDecimal(3);
                            string type = transReader["Type"].ToString();

                            string key = (type == "Stock") ? symbol : symbol + expDate.ToString("yyMMMdd") + type + strike.ToString("#.0");
                            Debug.WriteLine("    Transactions -> " + key + "  quant: " + quantity.ToString());

                            holdings.AddTransaction(key, quantity, row);
                        }

                        if (holdings.IsAllClosed())
                        {
                            Debug.WriteLine("everything is closed out");

                            int newGroupID = DBUtilities.GetMax("SELECT max(TransGroupID) FROM Transactions") + 1;

                            List<int> rows = holdings.GetRows();
                            foreach (int r in rows)
                            {
                                sql = "UPDATE transactions SET TransGroupID = @id WHERE ID=@row";
                                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                                cmdUpd.Parameters.AddWithValue("id", newGroupID);
                                cmdUpd.Parameters.AddWithValue("row", r);
                                cmdUpd.ExecuteNonQuery();
                            }

                            holdings.Clear();
                        }

                        
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("QUERY: " + ex.Message);
            }

            //



            //SELECT* FROM(
            //SELECT type, strike, expiredate, sum(quantity) AS total FROM transactions WHERE symbol = 'GOOG' GROUP BY type, strike, expiredate )
            //WHERE total<> 0

            // aggragates lots that might have separated
            //SELECT  time, type, strike, expiredate, sum(quantity) AS total FROM transactions WHERE symbol = 'GOOG' GROUP BY  time, type, strike, expiredate 
        
        }
    }
}
