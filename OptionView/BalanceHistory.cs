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



        public static void WriteGroups(Portfolio portfolio)
        {
            OpenConnection();

            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;
                DateTime tm = GetLastGroupEntry(grp);

                if (portfolio.dataCache.GroupHistory.ContainsKey(grp.GroupID))
                {
                    GroupHistory gp = portfolio.dataCache.GroupHistory[grp.GroupID];
                    if (tm == DateTime.MinValue)
                    {
                        // set start date with history if nothing in the database
                        tm = gp.Values.Min(x => x.Key);
                    }
                    else
                    {
                        // move to first empty date
                        tm = tm.AddDays(1);
                    }
                    if (gp != null)
                    {
                        for (DateTime day = tm; day <= DateTime.Today; day = day.AddDays(1))
                        {
                            Debug.WriteLine($"day: {day}");
                            if (gp.Values.ContainsKey(day))
                            {
                                GroupHistoryValue hv = gp.Values[day];

                                string sql = "INSERT INTO GroupHistory(Date, GroupID, Value, Underlying, IV) Values (@dt,@id,@va,@ul,@iv)";
                                SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
                                cmd.Parameters.AddWithValue("dt", day);
                                cmd.Parameters.AddWithValue("id", grp.GroupID);
                                cmd.Parameters.AddWithValue("va", hv.Value);
                                cmd.Parameters.AddWithValue("ul", hv.Underlying);
                                cmd.Parameters.AddWithValue("iv", Math.Round(hv.IV, 1));
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }

            CloseConnection();
        }

        public static DateTime GetLastGroupEntry(TransactionGroup grp)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Max(Date) FROM GroupHistory WHERE GroupID = @grp", ConnStr);
            cmd.Parameters.AddWithValue("grp", grp.GroupID);
            var obj = cmd.ExecuteScalar();
            return (obj == DBNull.Value) ? DateTime.MinValue : Convert.ToDateTime(obj).Date;
        }


        // this method is still used to save history data for groups so that something could be viewed in the results tab
        public static GroupHistory GetGroup(TransactionGroup grp)
        {
            GroupHistory retval = new GroupHistory();
            retval.Values = new Dictionary<DateTime, GroupHistoryValue>();

            OpenConnection();

            // all records
            string sql = "SELECT date, value, underlying, IV, IVR FROM GroupHistory WHERE GroupID = @grp ORDER BY date";


            SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
            cmd.Parameters.AddWithValue("grp", grp.GroupID);
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                GroupHistoryValue val = new GroupHistoryValue();
                val.Time = reader.GetDateTime(0);
                if (reader["Value"] != DBNull.Value) val.Value = reader.GetDecimal(1);
                if (reader["Underlying"] != DBNull.Value) val.Underlying = reader.GetDecimal(2);
                if (reader["IV"] != DBNull.Value) val.IV = reader.GetDecimal(3);

                retval.Values.Add(val.Time, val);
            }

            retval.Calls = grp.GetOptionStrikeHistory("Call");
            retval.Puts = grp.GetOptionStrikeHistory("Put");

            CloseConnection();

            return retval;
        }

    }

}
