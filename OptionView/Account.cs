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
    public class Account
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public decimal NetLiq { get; set; }
        public decimal OptionBuyingPower { get; set; }
    }

    public class Accounts : List<Account>
    {
        public Accounts()
        {
            try
            {
                // always start with an empty list
                this.Clear();

                // establish connection
                App.OpenConnection();

                string sql = "SELECT * FROM accounts";

                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Account acct = new Account();
                    if (reader["ID"] != DBNull.Value) acct.ID = reader["ID"].ToString();
                    if (reader["Name"] != DBNull.Value) acct.Name = reader["Name"].ToString();
                    if (reader["Active"] != DBNull.Value) acct.Active = Convert.ToBoolean(reader["Active"]);

                    if (acct.ID.Length > 0) this.Add(acct);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Account Load: " + ex.Message);
            }
        }

        public List<Account> Active()
        {
            List<Account> retval = new List<Account>();
            foreach (Account a in this)
            {
                if (a.Active) retval.Add(a);
            }
            return retval;
        }

    }
}