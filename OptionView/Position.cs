﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace OptionView
{
    public class Position : Transaction
    {
        // add property
        public List<int> Rows { get; set; }
        public decimal Market { get; set; }
        public decimal Multiplier { get; set; }
        public decimal UnderlyingPrice { get; set; }

        public Position ()
        {
            Rows = new List<int>();
            TransType = "not used";
        }
 
    }


    public class Positions : SortedDictionary<string,Position>
    {
        private int groupID = 0;

        public Positions ()
        {
            
        }

        public string Add(Position pos)
        {
            string key = (pos.Type == "Stock") ? pos.Symbol : pos.Symbol + pos.ExpDate.ToString("yyMMdd") + pos.Strike.ToString("0000.0") + pos.Type;

            this.Add(key, pos);
            return key;
        }

        public string Add(string symbol, string type, DateTime expDate, decimal strike, decimal quant, decimal amount, DateTime? transTime, int row, string openClose, int grpID)
        {
            string key = (type == "Stock") ? symbol : symbol + expDate.ToString("yyMMdd") + strike.ToString("0000.0") + type;

            Position p = null;
            if (this.ContainsKey(key))
            {
                p = this[key];
                p.Quantity += quant;
                p.Amount += amount;
                if ((transTime != null) && (transTime < p.TransTime)) p.TransTime = (DateTime)transTime;
                p.Rows.Add(row);
            }
            else
            {
                p = new Position();
                p.Symbol = symbol;
                p.ExpDate = expDate;
                p.Strike = strike;
                p.Type = type;
                p.Quantity = quant;
                p.Amount = amount;
                if (transTime != null) p.TransTime = (DateTime)transTime;
                p.Rows.Add(row);
                this.Add(key, p);
            }

            p.TransType = openClose;
            if (grpID > 0) groupID = grpID;

            return key;
        }

        public string Add(string symbol, string type, DateTime expDate, decimal strike, decimal quant, int row, string openClose, int grpID)
        {
            return Add(symbol, type, expDate, strike, quant, 0.0m, null, row, openClose, grpID);
        }

        public int GroupID()
        {
            return groupID;
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

        public bool Includes (string type, DateTime expDate, decimal strike)
        {
            bool ret = false;
            foreach (KeyValuePair<string, Position> item in this)
            {
                // leave out Call/Put for the time being
                //if ((item.Value.Type == type) && (item.Value.ExpDate == expDate) && (item.Value.Strike == strike))
                // for early exercise this doesn't work
                //if ((item.Value.ExpDate == expDate) && (item.Value.Strike == strike))
                if (item.Value.Strike == strike)
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }


        public override string ToString()
        {
            string ret = "";
            foreach (KeyValuePair<string, Position> item in this)
            {
                ret += item.Key + " : " + item.Value.Quantity.ToString() + "\n";
            }
            return ret;
        }

        public void DumpToDebug()
        {
            foreach (KeyValuePair<string, Position> item in this)
            {
                if (item.Value.Quantity != 0)
                {
                    Debug.WriteLine(item.Key + " : " + item.Value.Quantity);
                }
            }
        }

        public List<int> GetRowNumbers()
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
