using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptionView
{
    public class TransactionGroup
    {
        public string Symbol { get; set; }
        public int GroupID { get; set; }
        public decimal Cost;
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
            X = 13;
            Y = 13;
            Comments = "";
            Holdings = new Positions();
            Transactions = new Transactions();
        }
    }



}
