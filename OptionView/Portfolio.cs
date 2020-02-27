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


namespace OptionView
{

    public class Portfolio : Dictionary<int,TransactionGroup>
    {
        public Portfolio()
        {
        }

                
        static public TransactionGroup MapTransactionGroup(SQLiteDataReader reader)
        {
            TransactionGroup grp = new TransactionGroup();

            grp.Account = Convert.ToInt32(reader["Account"]);
            grp.Symbol = reader["Symbol"].ToString();
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

        private static bool HasColumn (DbDataReader reader, string column)
        {
            return (reader.GetOrdinal(column) >= 0);
        }

        public void GetCurrentHoldings()
        {
            try
            {
                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                string sql = "SELECT *, date(ActionDate) AS TodoDate FROM transgroup AS tg LEFT JOIN";
                sql += " (SELECT transgroupid, SUM(amount) AS Cost, SUM(Fees) AS Fees, datetime(MIN(time)) AS startTime, datetime(MAX(time)) AS endTime from transactions GROUP BY transgroupid) AS t ON tg.id = t.transgroupid";
                sql += " WHERE tg.Open = 1";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader readerGroup = cmd.ExecuteReader();
                while (readerGroup.Read())
                {
                    TransactionGroup grp = MapTransactionGroup(readerGroup);

                    // step thru open holdings
                    sql = "SELECT * FROM (SELECT symbol, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(quantity) AS total, sum(amount) as amount FROM transactions";
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

                        grp.Holdings.Add(reader["symbol"].ToString(), reader["type"].ToString(), expDate, strike, quantity, amount, 0, "", 0);
                    }

                    grp.EarliestExpiration = FindEarliestDate(grp.Holdings);

                    this.Add(grp.GroupID, grp);
                }  // end of transaction group loop

            }
            catch (Exception ex)
            {
                Debug.WriteLine("CurrentHoldings: " + ex.Message);
            }

        }

        private DateTime FindEarliestDate(Positions positions)
        {
            DateTime ret = DateTime.MaxValue;

            foreach(KeyValuePair<string, Position> item in positions)
            {
                Position p = item.Value;
                if ((p.ExpDate > DateTime.MinValue) && (p.ExpDate < ret)) ret = p.ExpDate;
            }

            return ret;
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

                        grp.Account = Convert.ToInt32(reader["Account"]);
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
