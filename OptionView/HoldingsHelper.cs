using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;


namespace OptionView
{
    public class HoldingsHelper
    {
        public static bool test(object x)
        {
            return true;
        }



        public static void UpdateTilePosition(string tag, int x, int y)
        {
            try
            {
                // establish connection
                App.OpenConnection();

                // update all of the rows in the chain
                string sql = "UPDATE transgroup SET x = @x, y = @y WHERE ID=@row";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("x", x);
                cmdUpd.Parameters.AddWithValue("y", y);
                cmdUpd.Parameters.AddWithValue("row", tag);
                cmdUpd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateTilePosition: " + ex.Message);
            }
        }




        public static Portfolio CurrentHoldings()
        {
            Portfolio portfolio = new Portfolio();

            try
            {
                // establish connection
                App.OpenConnection();

                string sql = "SELECT * FROM transgroup AS tg LEFT JOIN";
                sql += " (SELECT transgroupid, SUM(amount) AS Cost, datetime(MIN(time)) AS startTime, datetime(MAX(time)) AS endTime, datetime(MIN(expireDate)) AS EarliestExpiration from transactions GROUP BY transgroupid) AS t ON tg.id = t.transgroupid";
                sql += " WHERE tg.Open = 1";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader readerGroup = cmd.ExecuteReader();
                while (readerGroup.Read())
                {
                    Underlying underlying = new Underlying();

                    underlying.Symbol = readerGroup["Symbol"].ToString();
                    underlying.TransactionGroup = Convert.ToInt32(readerGroup["ID"]); // readerGroup
                    underlying.Cost = Convert.ToDecimal(readerGroup["Cost"]);
                    if (readerGroup["X"] != DBNull.Value) underlying.X = Convert.ToInt32(readerGroup["X"]);
                    if (readerGroup["Y"] != DBNull.Value) underlying.Y = Convert.ToInt32(readerGroup["Y"]);
                    if (readerGroup["Comments"] != DBNull.Value) underlying.Comments = readerGroup["Comments"].ToString();
                    if (readerGroup["Strategy"] != DBNull.Value) underlying.Strategy = readerGroup["Strategy"].ToString();
                    if (readerGroup["ExitStrategy"] != DBNull.Value) underlying.ExitStrategy = readerGroup["ExitStrategy"].ToString();
                    if (readerGroup["CapitalRequired"] != DBNull.Value) underlying.CapitalRequired = Convert.ToDecimal(readerGroup["CapitalRequired"]);
                    if (readerGroup["EarningsTrade"] != DBNull.Value) underlying.EarningsTrade = (Convert.ToInt32(readerGroup["EarningsTrade"]) == 1);
                    if (readerGroup["DefinedRisk"] != DBNull.Value) underlying.DefinedRisk = (Convert.ToInt32(readerGroup["DefinedRisk"]) == 1);
                    if (readerGroup["Risk"] != DBNull.Value) underlying.Risk = Convert.ToDecimal(readerGroup["Risk"]);
                    if (readerGroup["startTime"] != DBNull.Value) underlying.StartTime = Convert.ToDateTime(readerGroup["startTime"].ToString());
                    if (readerGroup["endTime"] != DBNull.Value) underlying.EndTime = Convert.ToDateTime(readerGroup["endTime"].ToString());
                    if (readerGroup["EarliestExpiration"] != DBNull.Value) underlying.EarliestExpiration = Convert.ToDateTime(readerGroup["EarliestExpiration"].ToString());

                    // step thru open holdings
                    sql = "SELECT * FROM (SELECT symbol, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(quantity) AS total, sum(amount) as amount FROM transactions";
                    sql += " GROUP BY symbol, type, expiredate, strike) WHERE (transgroupid = @gr) and (total <> 0)";
                    cmd = new SQLiteCommand(sql, App.ConnStr);
                    cmd.Parameters.AddWithValue("gr", underlying.TransactionGroup);

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

                        underlying.Holdings.AddTransaction(reader["symbol"].ToString(), reader["type"].ToString(), expDate, strike, quantity, amount, 0, "", 0);
                    }

                    portfolio.Add(underlying.TransactionGroup, underlying);

                }  // end of transaction group loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CurrentHoldings: " + ex.Message );
            }

            return portfolio;
        }

        public static void UpdateTransactionGroup(Underlying u)
        {
            if (u.TransactionGroup > 0)
            {
                // update group
                string sql = "UPDATE transgroup SET ExitStrategy = @ex, Comments = @cm, CapitalRequired = @ca, EarningsTrade = @ea, DefinedRisk = @dr, Risk = @rs WHERE ID=@row";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("ex", u.ExitStrategy);
                cmdUpd.Parameters.AddWithValue("cm", u.Comments);
                cmdUpd.Parameters.AddWithValue("ca", u.CapitalRequired);
                cmdUpd.Parameters.AddWithValue("ea", u.EarningsTrade);
                cmdUpd.Parameters.AddWithValue("dr", u.DefinedRisk);
                cmdUpd.Parameters.AddWithValue("rs", u.Risk);
                cmdUpd.Parameters.AddWithValue("row", u.TransactionGroup);
                cmdUpd.ExecuteNonQuery();
            }
        }


        public static void UpdateNewTransactions()
        {
            string symbol = "";

            try
            {
                // establish connection
                App.OpenConnection();

                string sql = "SELECT DISTINCT symbol from Transactions WHERE TransGroupID is NULL AND symbol != '' AND TransType != 'Money Movement' ORDER BY symbol";
                //string sql = "SELECT DISTINCT symbol from Transactions WHERE TransGroupID is NULL AND symbol = 'AMD' ORDER BY symbol";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {

                    // save props of controlling record
                    symbol = reader["Symbol"].ToString();
                    Debug.WriteLine("Found -> " + symbol);

                    sql = "SELECT transactions.ID AS ID, transgroup.ID as tID, datetime(Time) AS TransTime, TransType, TransGroupID, datetime(ExpireDate) AS ExpireDate, Strike, Quantity, Type, Price, [Open-Close]";
                    sql += " FROM transactions";
                    sql += " LEFT JOIN transgroup ON transgroupid = transgroup.id";
                    sql += " WHERE transactions.symbol = @sym AND (transgroup.Open = 1 OR transgroup.Open IS NULL)";
                    sql += " ORDER BY Time";

                    SQLiteCommand cmdTrans = new SQLiteCommand(sql, App.ConnStr);
                    cmdTrans.Parameters.AddWithValue("sym", symbol);

                    // 
                    SQLiteDataAdapter da = new SQLiteDataAdapter(cmdTrans);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    while (dt.Rows.Count > 0)
                    {
                        int numOfRows = dt.Rows.Count;
                        string t = dt.Rows[0]["TransTime"].ToString(); // maintain sqlite date format as a string 
                        ProcessTransactionChain(symbol, dt, t);

                        if (dt.Rows.Count == numOfRows) break;  // nothing happened, so avoid endless loop
                    }


                }  // end of symbol loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateNewTransactions: " + ex.Message + "(" + symbol + ")");
            }

 


            //SELECT* FROM(
            //SELECT type, strike, expiredate, sum(quantity) AS total FROM transactions WHERE symbol = 'GOOG' GROUP BY type, strike, expiredate )
            //WHERE total<> 0

            // aggregates lots that might have separated
            //SELECT  time, type, strike, expiredate, sum(quantity) AS total FROM transactions WHERE symbol = 'GOOG' GROUP BY  time, type, strike, expiredate 
        
        }



        //
        // Header function for setting up recursion
        // starts with data table of all possible transaction, symbol and a particular transaction time
        // chain is built from there
        //
        private static void ProcessTransactionChain(string symbol, DataTable dt, string time)
        {
            Positions holdings = new Positions();
            SortedList<string, string> times = new SortedList<string, string>();

            // start recursion
            ProcessTransactionGroup(symbol, dt, time, holdings, times);
            Debug.WriteLine("Chain completed");


            // retrieve existing group id
            int groupID = holdings.GroupID();

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
                groupID = DBUtilities.GetMax("SELECT max(TransGroupID) FROM Transactions") + 1;

                string sql = "INSERT INTO TransGroup(ID, Symbol, Open) Values(@id,@sym,@op)";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                cmd.Parameters.AddWithValue("id", groupID);
                cmd.Parameters.AddWithValue("sym", symbol);
                cmd.Parameters.AddWithValue("op", !holdings.IsAllClosed());
                cmd.ExecuteNonQuery();
            }

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



        private static void ProcessTransactionGroup(string symbol, DataTable dt, string time, Positions holdings, SortedList<string, string> times)
         {
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
                        string key = holdings.AddTransaction(symbol, type, expDate, strike, quantity, row, openClose, grpID);
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

                            string key = holdings.AddTransaction(p.Symbol, p.Type, p.ExpDate, p.Strike, (Int32)r.Field<Int64>("Quantity"), (Int32)r.Field<Int64>("ID"), r.Field<string>("Open-Close").ToString(), grpID);
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
                    ProcessTransactionGroup(symbol, dt, t, holdings, times);
            }


        }
    }
}
