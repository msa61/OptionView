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

namespace OptionView
{
    public class GroupHistoryValue
    {
        public DateTime Time { get; set; }
        public decimal Value { get; set; }
        public decimal Underlying { get; set; }
        public decimal IV { get; set; }
        public decimal IVR { get; set; }
    }

    public class GroupHistory
    {
        public List<GroupHistoryValue> Values { get; set; } 
        public SortedList<DateTime, List<decimal>> Puts { get; set; }
        public SortedList<DateTime, List<decimal>> Calls { get; set; }
    }

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



        public static void WriteGroups(Portfolio portfolio)
        {
            OpenConnection();
            DateTime tm = GetLastGroupEntry();

            if (tm.AddHours(2) < DateTime.Now)
            {
                foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
                {
                    TransactionGroup grp = entry.Value;

                    // skip this if there isn't a current value
                    if (grp.CurrentValue != null)
                    {
                        decimal value = (grp.CurrentValue ?? 0) + grp.Cost;

                        string sql = "INSERT INTO GroupHistory(Date, GroupID, Value, Underlying, IV, IVR) Values (@dt,@id,@va,@ul,@iv,@ir)";
                        SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
                        cmd.Parameters.AddWithValue("dt", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("id", grp.GroupID);
                        cmd.Parameters.AddWithValue("va", value);
                        cmd.Parameters.AddWithValue("ul", grp.UnderlyingPrice);
                        cmd.Parameters.AddWithValue("iv", Math.Round(grp.ImpliedVolatility * 100, 1));
                        cmd.Parameters.AddWithValue("ir", Math.Round(grp.ImpliedVolatilityRank * 100, 1));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            CloseConnection();
        }

        public static DateTime GetLastGroupEntry()
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Max(Date) FROM GroupHistory", ConnStr);
            var obj = cmd.ExecuteScalar();
            return (obj == DBNull.Value) ? DateTime.MinValue : Convert.ToDateTime(obj);
        }

        public static GroupHistory GetGroup(TransactionGroup grp)
        {
            GroupHistory retval = new GroupHistory();
            retval.Values = new List<GroupHistoryValue>();

            OpenConnection();

            // last record of the day (and only week days)
            //string sql = "SELECT date, value, underlying FROM GroupHistory AS h ";
            //sql += "INNER JOIN (SELECT DISTINCT strftime('%Y-%m-%d', date) AS day, rowid FROM ";
            //sql += "(SELECT *, rowid, CAST(strftime('%w', date) AS Integer) as DoW FROM GroupHistory ";
            //sql += "WHERE GroupID = @grp AND  DoW > 0 AND DoW < 6 AND date < datetime('now') ORDER BY date DESC) ";
            //sql += "GROUP BY day HAVING max(rowid) ORDER BY day DESC) AS r ON h.rowid = r.rowid ";
            //sql += "ORDER BY day";

            // all records
            string sql = "SELECT date, value, underlying, IV, IVR FROM GroupHistory WHERE GroupID = @grp ORDER BY date";

            //string sql = "SELECT date, value, underlying, IV, IVR, CAST(strftime('%w', date) AS Integer) as DoW";
            //sql += " FROM GroupHistory WHERE GroupID = @grp AND  DoW > 0 AND DoW < 6 ORDER BY date";


            SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
            cmd.Parameters.AddWithValue("grp", grp.GroupID);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                GroupHistoryValue val = new GroupHistoryValue();
                val.Time  = reader.GetDateTime(0);
                if (reader["Value"] != DBNull.Value) val.Value = reader.GetDecimal(1);
                if (reader["Underlying"] != DBNull.Value) val.Underlying = reader.GetDecimal(2);
                if (reader["IV"] != DBNull.Value) val.IV = reader.GetDecimal(3);
                if (reader["IVR"] != DBNull.Value) val.IVR = reader.GetDecimal(4);

                retval.Values.Add(val);
            }

            retval.Calls = grp.GetOptionHistoryList("Call");
            retval.Puts = grp.GetOptionHistoryList("Put");

            CloseConnection();

            return retval;
        }

    }

}
