using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptionView
{
    public class Underlying
    {
        public string Symbol { get; set; }
        public int TransactionGroup { get; set; }
        public decimal Cost;
        public int X { get; set; }
        public int Y { get; set; }
        public string Strategy { get; set; }
        public string ExitStrategy { get; set; }
        public string Comments { get; set; }
        public Positions Holdings { get; set; }
        public decimal CapitalRequired { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime EarliestExpiration { get; set; }


        public Underlying()
        {
            Symbol = "undef";
            Initialize();
        }
        public Underlying(string sym)
        {
            Symbol = sym;
        }
        private void Initialize()
        {
            TransactionGroup = 0;
            Cost = 0;
            X = 13;
            Y = 13;
            Comments = "";
            Holdings = new Positions();
        }
    }

    public class UnderlyingGrid
    {
        public string Symbol { get; set; }
        public decimal Cost { get; set; }
        public string Strategy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }


        public class Portfolio : Dictionary<int,Underlying>
    {
        public Portfolio()
        {
        }


    }

}
