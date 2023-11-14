using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Controls;
using System.Security.Principal;
using System.Windows.Controls.Primitives;
using OptionView.DataImport;
using System.Diagnostics;

namespace OptionView
{

    internal class BalanceHistory
    {
        private static SQLiteConnection ConnStr = null;

        public static void OpenConnection()
        {
            try
            {
                if (! File.Exists("balancehistory.sqlite"))
                {
                    MessageBox.Show("History database not found", "OpenConnection");
                }
                if (ConnStr == null) ConnStr = new SQLiteConnection("Data Source=balancehistory.sqlite;Version=3;");
                if (ConnStr.State == System.Data.ConnectionState.Closed) ConnStr.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "OpenConnection");
            }
        }

        public static void CloseConnection()
        {
            if ((ConnStr != null) && ConnStr.State == ConnectionState.Open) ConnStr.Close();
        }


        public static void Write (string account, decimal balance, decimal capRequired)
        {
            OpenConnection();
            DateTime tm = GetLastEntry(account);

            if ((tm.AddHours(4) < DateTime.UtcNow) && (balance > 0))
            {
                string sql = "INSERT INTO AccountHistory(Date, Account, Balance, CapitalRequired) Values (@dt,@ac,@ba,@cr)";
                SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
                cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("ac", account);
                cmd.Parameters.AddWithValue("ba", balance);
                cmd.Parameters.AddWithValue("cr", capRequired);
                cmd.ExecuteNonQuery();
            }

            CloseConnection();
        }

        public static void TimeStamp ()
        {
            OpenConnection();

            string sql = "INSERT INTO UpdateHistory(TimeStamp) Values (@dt)";
            SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
            cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
            cmd.ExecuteNonQuery();

            CloseConnection();
        }

        public static DateTime GetLastEntry(string account)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Max(Date) FROM AccountHistory Where Account = @ac", ConnStr);
            cmd.Parameters.AddWithValue("ac", account);
            var obj = cmd.ExecuteScalar();
            return (obj == DBNull.Value) ? DateTime.MinValue : Convert.ToDateTime(obj);
        }

        public static List<decimal> Get(string account)
        {
            List<decimal> retval = new List<decimal>();

            OpenConnection();

            string sql = "SELECT balance FROM AccountHistory AS h ";
            sql += "INNER JOIN (SELECT DISTINCT strftime('%Y-%m-%d', date) AS day, rowid FROM ";
            sql += "(SELECT *, rowid, CAST(strftime('%w', date) AS Integer) as DoW FROM AccountHistory ";
            sql += "WHERE account = @ac AND  DoW > 0 AND DoW < 6 AND date < datetime('now') ORDER BY date DESC) ";
            sql += "GROUP BY day HAVING max(rowid) ORDER BY day DESC LIMIT 11) AS r ON h.rowid = r.rowid ";
            sql += "ORDER BY day";

            SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
            cmd.Parameters.AddWithValue("ac", account);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                decimal val = 0;
                if (reader["Balance"] != DBNull.Value) val = reader.GetDecimal(0);

                if (val != 0) retval.Add(val);
            }
            CloseConnection();

            return retval;
        }

        public static List<decimal> Get(Accounts accounts)
        {
            List<decimal> retval = null;

            foreach(Account a in accounts)
            {
                List<decimal> l = Get(a.Name);

                if (retval == null)
                {
                    retval = l;
                }
                else
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        retval[i] += l[i];
                    }
                }

            }

            return retval;
        }

        public static List<decimal> GetChange(object obj)
        {
            List<decimal> values = null;
            if (obj.GetType() == typeof(string))
            {
                values = Get((string)obj);
            }
            else if (obj.GetType() == typeof(Accounts))
            {
                values = Get((Accounts)obj);
            }

            List<decimal> retval = new List<decimal>();

            if ((values != null) && (values.Count > 1))
            {
                for (int i = 1; i < values.Count; i++)
                {
                    retval.Add(values[i] - values[i - 1]);
                }
            }

            return retval;
        }

        public static List<decimal> YTDMinMax(string account)
        {
            List<decimal> retval = new List<decimal>();

            OpenConnection();

            string sql = "SELECT Min(balance) as MinBalance, Max(balance) as MaxBalance FROM AccountHistory ";
            sql += "WHERE account = @ac AND strftime('%Y',date) = strftime('%Y', date('now'))";

            SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
            cmd.Parameters.AddWithValue("ac", account);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader["MinBalance"] != DBNull.Value) retval.Add(reader.GetDecimal(0));
                if (reader["MaxBalance"] != DBNull.Value) retval.Add(reader.GetDecimal(1));
            }
            CloseConnection();

            return retval;
        }


        public GroupHistory GetHistoryValues()
        {
            GroupHistory retval = new GroupHistory();
            retval.Values = new List<GroupHistoryValue>();

            // retrieve all positions over time grouped by dates of transactions
            SortedList<DateTime, Positions> historicalPositions = this.GetOptionPositionHistory();

            List<string> symbols = new List<string>();
            foreach (KeyValuePair<DateTime, Positions> p in historicalPositions)
            {
                Positions positions = p.Value;
                List<string> allSymbols = allSymbols = positions.Select(x => x.Value.StreamingSymbol).ToList();
                foreach (string s in allSymbols)
                {
                    if (!symbols.Any(x => x.Equals(s))) symbols.Add(s);
                }
            }
            // add underlying if not already included
            if (!symbols.Any(s => s.Equals(this.StreamingSymbol))) symbols.Add(this.StreamingSymbol);

            // get data
            Dictionary<string, Candles> lst = DataFeed.GetHistory(symbols, this.StartTime.Date);

            if (lst.Count > 0)
            {
                int currentPositionIndex = 0;

                DateTime today = DateTime.Today;
                for (DateTime date = this.StartTime.Date; date <= today; date = date.AddDays(1))
                {
                    DateTime nextDate = GetNextHistoricTime(historicalPositions, currentPositionIndex);
                    if (date >= nextDate) currentPositionIndex++;

                    KeyValuePair<DateTime, Positions> node = historicalPositions.ElementAt(currentPositionIndex);
                    DateTime currentTime = node.Key;
                    Positions currentPositions = node.Value;
                    //Debug.WriteLine($"Today: {date}    index: {currentPositionIndex}    current: {currentTime}    next: {nextDate}");

                    decimal? currentValue = null;
                    foreach (KeyValuePair<string, Position> p in currentPositions)
                    {
                        Position pos = p.Value;

                        if (!lst[pos.StreamingSymbol].ContainsKey(date))
                        {
                            currentValue = null;
                            break;
                        }

                        decimal multipler = 1;
                        if (pos.Type != "Stock")
                        {
                            multipler = 100;
                            var x = this.Holdings.Where(y => y.Value.Type == pos.Type);
                            if (x.Count() > 0) multipler = this.Holdings.Where(z => z.Value.Type == pos.Type).Select(z => z.Value.Multiplier).First();
                        }

                        //Debug.WriteLine($"     {pos.StreamingSymbol}  {lst[pos.StreamingSymbol][date].Price}    mult: {multipler}");
                        if (currentValue == null) currentValue = 0;
                        currentValue += pos.Quantity * lst[pos.StreamingSymbol][date].Price * multipler;
                    }

                    if (currentValue != null)
                    {
                        decimal accumlativeCost = this.GetCostAtDate(date);
                        //Debug.WriteLine($"  date: {date}    value: {currentValue}   cost: {grp.Cost}    new cost: {accumlativeCost}");

                        GroupHistoryValue val = new GroupHistoryValue();
                        val.Time = date;
                        val.Value = (currentValue ?? 0) + accumlativeCost;
                        if ((this.StreamingSymbol != null) && (lst.ContainsKey(this.StreamingSymbol)))
                        {
                            Candles candles = lst[this.StreamingSymbol];
                            val.Underlying = candles[date].Price;
                            val.IV = candles[date].IV * 100;
                            //Debug.WriteLine($"  underlying: {val.Underlying}   iv: {val.IV}");
                        }

                        retval.Values.Add(val);
                    }
                }

            }

            retval.Calls = this.GetOptionStrikeHistory("Call");
            retval.Puts = this.GetOptionStrikeHistory("Put");

            return retval;
        }

    }

}
