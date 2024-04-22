using System;
using System.Collections.Generic;
using System.Linq;


namespace DxLink
{
    public enum SubscriptionType
    {
        None = 0,
        Trade = 1,
        Quote = 2,
        Profile = 4,
        Greek = 8,
        TheoPrice = 16,
        Summary = 32,
        TimeSeries = 64,
        AllEquity = Trade | Quote | Profile,
        All = Trade | Quote | Greek | Profile
    }

    public class Subscription
    {
        public Quote Quote {  get; set; }
        public Candles Candles { get; set; }
        public SubscriptionType Type { get; set; }
        public SubscriptionType Status { get; set; }
        public bool IsComplete { get { return (Status == SubscriptionType.None); }}
        public bool Stream { get; set; }

        public Subscription(string symbol, SubscriptionType type, bool leaveOpen = false)
        {
            Quote = new Quote(symbol);
            Candles = new Candles();
            Type = type;
            Status = type;
            Stream = leaveOpen; 
        }
    }

    public class Subscriptions : Dictionary<string, Subscription>
    {
        public Subscriptions()
        {
        }

        public Subscription Add(string symbol, SubscriptionType type = SubscriptionType.All)
        {
            Subscription retval =null;
            if ((symbol.Length < 6) && (type == SubscriptionType.All)) type = SubscriptionType.AllEquity;
            type &= ~SubscriptionType.Greek;  // TEMPORARY TODO - REMOVE LINE ONCE GREEKS ARE AVAILABLE
            if (this.ContainsKey(symbol))
            {
                retval = this[symbol];
                this[symbol].Candles = new Candles();  // reset in event of multiple ts requests
            }
            else
            {
                retval = new Subscription(symbol, type);
            }
            base.Add(symbol, retval);
            return retval;
        }
        public Subscriptions Add(List<string> symbols, SubscriptionType type = SubscriptionType.All)
        {
            Subscriptions retlist = new Subscriptions();
            foreach (string symbol in symbols)
            {
                Subscription newSub = Add(symbol, type);
                retlist.Add(symbol, newSub);
            }
            return retlist;
        }
        public new void Remove(string symbol)
        {
            if (this.ContainsKey(symbol)) base.Remove(symbol);
        }
        public void Remove(List<string> symbols)
        {
            foreach (string symbol in symbols)
            {
                Remove(symbol);
            }
        }
        public bool Subscribed(string symbol)
        {
            return this.ContainsKey(symbol);
        }


        public bool IsComplete()
        {
            int remaining = this.Count - this.Count(x => (x.Value.IsComplete == true));
            return (remaining == 0);
        }

        public DxStatusParams GetStatus()
        {
            DxStatusParams retval = new DxStatusParams();

            retval.Count = this.Count;
            retval.RemainingTrade = this.Count(x => (x.Value.Status & SubscriptionType.Trade) == SubscriptionType.Trade);
            retval.RemainingQuote = this.Count(x => (x.Value.Status & SubscriptionType.Quote) == SubscriptionType.Quote);
            retval.RemainingProfile = this.Count(x => (x.Value.Status & SubscriptionType.Profile) == SubscriptionType.Profile);
            retval.RemainingGreek = this.Count(x => (x.Value.Status & SubscriptionType.Greek) == SubscriptionType.Greek);
            retval.RemainingTimeSeries = this.Count(x => (x.Value.Status & SubscriptionType.TimeSeries) == SubscriptionType.TimeSeries);

            return retval;
        }

        public string GetStatusText()
        {
            DxStatusParams stats = GetStatus();

            string retval = string.Format($"Total: {stats.Count}  trade: {stats.RemainingTrade}  quote: {stats.RemainingQuote} profile: {stats.RemainingProfile} greek: {stats.RemainingGreek} timeseries: {stats.RemainingTimeSeries}");
            string missingTrade = "";
            //foreach (KeyValuePair<string, DxQuote> pair in this)
            //{
            //    QuoteStatus status = ((DxQuote)pair.Value).Status;
            //    if ((status & QuoteStatus.Trade) != QuoteStatus.Trade) missingTrade += pair.Key + ", ";
            //}

            return retval + "\n" + missingTrade;
        }


    }

}
