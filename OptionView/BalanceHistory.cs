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

            if (tm.AddHours(4) < DateTime.Now)
            {
                string sql = "INSERT INTO History(Date, Account, Balance, CapitalRequired) Values(@dt,@ac,@ba,@cr)";
                SQLiteCommand cmd = new SQLiteCommand(sql, ConnStr);
                cmd.Parameters.AddWithValue("dt", DateTime.Now);
                cmd.Parameters.AddWithValue("ac", account);
                cmd.Parameters.AddWithValue("ba", balance);
                cmd.Parameters.AddWithValue("cr", capRequired);
                cmd.ExecuteNonQuery();
            }
            CloseConnection();
        }

        public static DateTime GetLastEntry(string account)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Max(Date) FROM history Where Account = @ac", ConnStr);
            cmd.Parameters.AddWithValue("ac", account);
            var obj = cmd.ExecuteScalar();
            return (obj == DBNull.Value) ? DateTime.MinValue : Convert.ToDateTime(obj);
        }

        public static List<decimal> Get(string account)
        {
            List<decimal> retval = new List<decimal>();

            OpenConnection();

            string sql = "SELECT balance FROM history AS h ";
            sql += "INNER JOIN (SELECT DISTINCT strftime('%Y-%m-%d', date) AS day, rowid FROM ";
            sql += "(SELECT *, rowid FROM history WHERE account = @ac ORDER BY date DESC) ";
            sql += "GROUP BY day) AS r ON h.rowid = r.rowid ";
            sql += "LIMIT 11";

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


    }



}
