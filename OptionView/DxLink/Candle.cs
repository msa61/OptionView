using System;
using System.Collections.Generic;

namespace DxLink
{
    public class Candle
    {
        public DateTime Day { set; get; }
        public Decimal Price { set; get; }
        public Decimal IV { set; get; }
    }

    public class Candles : SortedList<DateTime, Candle>
    {
        public Candles()
        {

        }
        public Candles(Dictionary<string, Candle> list)
        {
            foreach (KeyValuePair<string, Candle> pair in list)
            {
                this.Add(pair.Value.Day, pair.Value);
            }
        }
    }
}
