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
    public class TransactionGroup
    {
        public string Symbol { get; set; }
        public int GroupID { get; set; }
        public decimal Cost { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Strategy { get; set; }
        public string ExitStrategy { get; set; }
        public string Comments { get; set; }
        public decimal CapitalRequired { get; set; }
        public bool EarningsTrade { get; set; }
        public bool DefinedRisk { get; set; }
        public decimal Risk { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime EarliestExpiration { get; set; }

        public Positions Holdings { get; set; }
        public Transactions Transactions { get; set; }
        private int shiftAmount = 0; 


        public TransactionGroup()
        {
            Symbol = "undef";
            Initialize();
        }
        public TransactionGroup(string sym)
        {
            Symbol = sym;
            Initialize();
        }

        private void Initialize()
        {
            GroupID = 0;
            Cost = 0;
            X = 13 + shiftAmount;
            Y = 13 + shiftAmount;
            Comments = "";
            Holdings = new Positions();
            Transactions = new Transactions();

            shiftAmount += 15;
        }



        public void UpdateTransactionGroup()
        {
            if (this.GroupID > 0)
            {
                // update group
                string sql = "UPDATE transgroup SET Strategy = @st, ExitStrategy = @ex, Comments = @cm, CapitalRequired = @ca, EarningsTrade = @ea, DefinedRisk = @dr, Risk = @rs WHERE ID=@rw";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("st", this.Strategy);
                cmdUpd.Parameters.AddWithValue("ex", this.ExitStrategy);
                cmdUpd.Parameters.AddWithValue("cm", this.Comments);
                cmdUpd.Parameters.AddWithValue("ca", this.CapitalRequired);
                cmdUpd.Parameters.AddWithValue("ea", this.EarningsTrade);
                cmdUpd.Parameters.AddWithValue("dr", this.DefinedRisk);
                cmdUpd.Parameters.AddWithValue("rs", this.Risk);
                cmdUpd.Parameters.AddWithValue("rw", this.GroupID);
                cmdUpd.ExecuteNonQuery();
            }
        }

    }



}
