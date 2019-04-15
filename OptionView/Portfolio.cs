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

    public class Portfolio : Dictionary<int,TransactionGroup>
    {
        public Portfolio()
        {
        }

        public void GetCurrentHoldings()
        {
            try
            {
                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                string sql = "SELECT * FROM transgroup AS tg LEFT JOIN";
                sql += " (SELECT transgroupid, SUM(amount) AS Cost, datetime(MIN(time)) AS startTime, datetime(MAX(time)) AS endTime, datetime(MIN(expireDate)) AS EarliestExpiration from transactions GROUP BY transgroupid) AS t ON tg.id = t.transgroupid";
                sql += " WHERE tg.Open = 1";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader readerGroup = cmd.ExecuteReader();
                while (readerGroup.Read())
                {
                    TransactionGroup grp = new TransactionGroup();

                    grp.Symbol = readerGroup["Symbol"].ToString();
                    grp.GroupID = Convert.ToInt32(readerGroup["ID"]); // readerGroup
                    grp.Cost = Convert.ToDecimal(readerGroup["Cost"]);
                    if (readerGroup["X"] != DBNull.Value) grp.X = Convert.ToInt32(readerGroup["X"]);
                    if (readerGroup["Y"] != DBNull.Value) grp.Y = Convert.ToInt32(readerGroup["Y"]);
                    if (readerGroup["Comments"] != DBNull.Value) grp.Comments = readerGroup["Comments"].ToString();
                    if (readerGroup["Strategy"] != DBNull.Value) grp.Strategy = readerGroup["Strategy"].ToString();
                    if (readerGroup["ExitStrategy"] != DBNull.Value) grp.ExitStrategy = readerGroup["ExitStrategy"].ToString();
                    if (readerGroup["CapitalRequired"] != DBNull.Value) grp.CapitalRequired = Convert.ToDecimal(readerGroup["CapitalRequired"]);
                    if (readerGroup["EarningsTrade"] != DBNull.Value) grp.EarningsTrade = (Convert.ToInt32(readerGroup["EarningsTrade"]) == 1);
                    if (readerGroup["DefinedRisk"] != DBNull.Value) grp.DefinedRisk = (Convert.ToInt32(readerGroup["DefinedRisk"]) == 1);
                    if (readerGroup["Risk"] != DBNull.Value) grp.Risk = Convert.ToDecimal(readerGroup["Risk"]);
                    if (readerGroup["startTime"] != DBNull.Value) grp.StartTime = Convert.ToDateTime(readerGroup["startTime"].ToString());
                    if (readerGroup["endTime"] != DBNull.Value) grp.EndTime = Convert.ToDateTime(readerGroup["endTime"].ToString());
                    if (readerGroup["EarliestExpiration"] != DBNull.Value) grp.EarliestExpiration = Convert.ToDateTime(readerGroup["EarliestExpiration"].ToString());

                    // step thru open holdings
                    sql = "SELECT * FROM (SELECT symbol, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(quantity) AS total, sum(amount) as amount FROM transactions";
                    sql += " GROUP BY symbol, type, expiredate, strike) WHERE (transgroupid = @gr) and (total <> 0)";
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

                    this.Add(grp.GroupID, grp);

                }  // end of transaction group loop
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CurrentHoldings: " + ex.Message);
            }

        }

    }

}
