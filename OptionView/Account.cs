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
    //public class Account
    //{
    //    public Int32 ID { get; set; }
    //    public string Name { get; set; }
    //}

    public class Accounts : Dictionary<string,string>
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
                    string id = "";
                    string name = "";
                    if (reader["ID"] != DBNull.Value) id = reader["ID"].ToString();
                    if (reader["Name"] != DBNull.Value) name = reader["Name"].ToString();

                    if (id.Length > 0) this.Add(id, name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Account Load: " + ex.Message);
            }
        }

    }
}