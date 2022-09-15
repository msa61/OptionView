using OptionView.DataImport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}
