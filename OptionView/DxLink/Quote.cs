using System;


namespace DxLink
{
    public enum QuoteStatus
    {
        None = 0,
        Trade = 1,
        Quote = 2,
        Profile = 4,
        Greek = 8,
        TheoPrice = 16,
        Summary = 32,
        AllEquity = Trade | Quote | Profile,
        All = Trade | Quote | Greek | Profile
    }

    public class Quote 
    {
        public string Symbol { get; set; }
        public string Description { get; set; }

        public decimal Price { get; set; }
        public decimal Change { get; set; }
        public decimal LastPrice { get; set; }
        public DateTime LastTrade { get; set; }
        public double Volume { get; set; }

        public decimal AskPrice { get; set; }
        public decimal BidPrice { get; set; }
        public decimal Spread { get; set; }

        public double Delta { get; set; }
        public double RawDelta;
        public double IV { get; set; }
        public double GreekPrice { get; set; }
        public double Theta { get; set; }
        public double TheoPrice { get; set; }


        public Quote()
        {

        }
        public Quote(string symbol)
        {
            Symbol = symbol;
        }


        public override string ToString()
        {
            return String.Format($"Sym: {Symbol}  price: {LastPrice}  ask: {AskPrice}  bid: {BidPrice}   {Description}");
        }


    }

}
