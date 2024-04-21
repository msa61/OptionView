using System;
using System.Collections.Generic;


namespace DxLink
{ 
    public class Greek
    {
        public double Price { set; get; }
        public double Volatility { set; get; }
        public double Delta { set; get; }
        public double WeightedDelta { set; get; }
        public double Theta { set; get; }
        public double Gamma { set; get; }
        public double Rho { set; get; }
        public double Vega { set; get; }
    }

    public class Greeks : Dictionary<string, Greek>
    {
        public Greeks()
        {

        }
        public Greeks(Dictionary<string, Greek> list)
        {
            foreach (KeyValuePair<string, Greek> pair in list)
            {
                this.Add(pair.Key, pair.Value);
            }
        }
    }

}
