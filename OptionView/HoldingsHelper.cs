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



    }
}
