﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptionView
{
    public class Underlying
    {
        public string Symbol;
        public int TransactionGroup;
        public decimal Cost;
        public int X;
        public int Y;
        public string Comments;
        public Positions Holdings;


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

    public class Portfolio : List<Underlying>
    {
        public Portfolio()
        {
        }


    }

}