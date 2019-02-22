using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace OptionView
{
    public class Position
    {
        public string Symbol;
        public decimal Quantity;
        public List<int> Rows;

        public Position ()
        {
            Quantity = 0;
            Rows = new List<int>();

        }
 
    }


    public class Positions : Dictionary<string,Position>
    {
        public Positions ()
        {
            
        }

        public Position AddTransaction( string key, decimal quant, int row)
        {
            Position p = null;
            if (this.ContainsKey(key))
            {
                p = this[key];
                p.Quantity += quant;
                p.Rows.Add(row);
            }
            else
            {
                p = new Position();
                p.Quantity = quant;
                p.Rows.Add(row);
                this.Add(key, p);
            }


            return p;
        }


        public bool IsAllClosed()
        {
            bool ret = true;
            foreach (KeyValuePair<string, Position> item in this)
            {
                if (item.Value.Quantity != 0)
                {
                    ret = false;
                    break;
                }
            }
            return ret;
        }

        public void DumpToDebug()
        {
            bool ret = true;
            foreach (KeyValuePair<string, Position> item in this)
            {
                if (item.Value.Quantity != 0)
                {
                    Debug.WriteLine(item.Key + " : " + item.Value.Quantity);
                }
            }
        }

        public List<int> GetRows()
        {
            List<int> ret = new List<int>();

            foreach (KeyValuePair<string, Position> item in this)
            {
                ret.AddRange(item.Value.Rows);
            }

            return ret;
        }
    }
}
