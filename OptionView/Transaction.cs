using OptionView.DataImport;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace OptionView
{
    public class Transaction
    {
        public DateTime TransTime { get; set; }
        public string TransType { get; set; }
        public string Symbol { get; set; }
        public DateTime ExpDate { get; set; }
        public string ExpDateText { get; set; }
        public decimal Strike { get; set; }
        public string Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public Greek GreekData { get; set; }

        // the following properties are only for display of raw transaction data
        public string TransSubType { get; set; }
        public string Description { get; set; }
        public string Account { get; set; }
        public decimal Price { get; set; }
        public decimal Fees { get; set; }
        public int GroupID { get; set; }
        public decimal UnderlyingPrice { get; set; }


        public Transaction()
        {
            Quantity = 0;
        }

    }


    public class Transactions : List<Transaction>
    {
        public Transactions()
        {

        }
        public void GetRecent()
        {
            // query all of the transactions in this account, for given symbol that are either part of an open chain or not part of chain yet
            string sql = "SELECT tg.Account, tg.Symbol, tg.Open, datetime(Time) AS TransTime, TransType, TransSubType, TransGroupID, datetime(ExpireDate) AS ExpireDate, Strike, Quantity, Type, Price, Fees, Amount, Description";
            sql += " FROM transactions";
            sql += " LEFT JOIN transgroup AS tg ON transgroupid = tg.id";
            sql += " WHERE time > date('now', '-30 day')";
            sql += " ORDER BY Time DESC";
            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);

            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Transaction t = new Transaction();

                if (reader["Strike"] != DBNull.Value) t.Strike = Convert.ToDecimal(reader["Strike"]);
                if (reader["Quantity"] != DBNull.Value) t.Quantity = Convert.ToDecimal(reader["Quantity"]);
                if (reader["Amount"] != DBNull.Value) t.Amount = Convert.ToDecimal(reader["Amount"]);
                if (reader["ExpireDate"] != DBNull.Value) t.ExpDate = Convert.ToDateTime(reader["ExpireDate"].ToString());
                DateTime transTime = DateTime.MinValue;
                if (reader["TransTime"] != DBNull.Value) transTime = Convert.ToDateTime(reader["TransTime"].ToString());
                t.TransTime = DateTime.SpecifyKind(transTime, DateTimeKind.Utc);

                if (reader["TransType"] != DBNull.Value) t.TransType = reader["TransType"].ToString();
                if (reader["TransSubType"] != DBNull.Value) t.TransSubType = reader["TransSubType"].ToString();
                if (reader["Symbol"] != DBNull.Value) t.Symbol = reader["Symbol"].ToString();
                if (reader["Description"] != DBNull.Value) t.Description = reader["Description"].ToString();
                if (reader["Account"] != DBNull.Value) t.Account = reader["Account"].ToString();
                if (reader["Type"] != DBNull.Value) t.Type = reader["Type"].ToString();
                if (reader["Price"] != DBNull.Value) t.Price = Convert.ToDecimal(reader["Price"]);
                if (reader["Fees"] != DBNull.Value) t.Fees = Convert.ToDecimal(reader["Fees"]);
                if (reader["TransGroupID"] != DBNull.Value) t.GroupID = Convert.ToInt32(reader["TransGroupID"]);

                this.Add(t);
            }
        }
    }

}
