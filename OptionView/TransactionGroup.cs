using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace OptionView
{
    public class TransactionGroup
    {
        public string Symbol { get; set; }
        public int GroupID { get; set; }
        public decimal Cost { get; set; }
        public decimal Fees { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Strategy { get; set; }
        public string ExitStrategy { get; set; }
        public DateTime ActionDate { get; set; }
        public string Comments { get; set; }
        public decimal CapitalRequired { get; set; }
        public decimal Return { get; set; }
        public decimal AnnualReturn { get; set; }
        public bool EarningsTrade { get; set; }
        public bool NeutralStrategy { get; set; }
        public bool DefinedRisk { get; set; }
        public decimal Risk { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Year { get; set; }
        public DateTime EarliestExpiration { get; set; }
        public string TransactionText { get; set; }
        public int Account { get; set; }

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



        public void Update()
        {
            if (this.GroupID > 0)
            {
                // update group
                string sql = "UPDATE transgroup SET Strategy = @st, ExitStrategy = @ex, ActionDate = @ad, Comments = @cm, CapitalRequired = @ca, EarningsTrade = @ea, NeutralStrategy = @ns, DefinedRisk = @dr, Risk = @rs WHERE ID=@rw";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("st", this.Strategy);
                cmdUpd.Parameters.AddWithValue("ex", this.ExitStrategy);
                cmdUpd.Parameters.AddWithValue("ad", this.ActionDate);
                cmdUpd.Parameters.AddWithValue("cm", this.Comments);
                cmdUpd.Parameters.AddWithValue("ca", this.CapitalRequired);
                cmdUpd.Parameters.AddWithValue("ea", this.EarningsTrade);
                cmdUpd.Parameters.AddWithValue("ns", this.NeutralStrategy);
                cmdUpd.Parameters.AddWithValue("dr", this.DefinedRisk);
                cmdUpd.Parameters.AddWithValue("rs", this.Risk);
                cmdUpd.Parameters.AddWithValue("rw", this.GroupID);
                cmdUpd.ExecuteNonQuery();
            }
        }


        public static int Combine(int destinationGroup, int combineGroup)
        {
            Debug.WriteLine("Combine {0} into {1}", combineGroup, destinationGroup);

            if ((destinationGroup == 0) || (combineGroup == 0))
            {
                MessageBox.Show("Missing ID(s). Combine failed.", "Combine Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return 0;  // nothing happened
            }


            // move any transaction from group to new group
            string sql = "UPDATE Transactions SET TransGroupID = @new WHERE TransGroupID=@grp";
            SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("new", destinationGroup);
            cmdUpd.Parameters.AddWithValue("grp", combineGroup);
            int retval = cmdUpd.ExecuteNonQuery();

            // no reason to keep the old group around - may change to save but with some comments appended
            sql = "DELETE FROM TransGroup Where ID = @id";
            cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("id", combineGroup);
            retval = cmdUpd.ExecuteNonQuery();

            return 1; // combine completed
        }

        public string GetPerLotCost()
        {
            decimal defaultAmount = 0;

            foreach (KeyValuePair<string, Position> item in this.Holdings)
            {
                Position p = item.Value;
                if (defaultAmount == 0)
                {
                    defaultAmount = Math.Abs(p.Quantity);
                }
                else
                {
                    if (Math.Abs(p.Quantity) != defaultAmount) return " *";
                }
            }

            if (defaultAmount == 0) return " oopsie";
            if (defaultAmount == 1) return "";

            return " - " + String.Format("{0:C0}", this.Cost / defaultAmount) + "/lot";
        }
    }

}
