using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
//using com.dxfeed.api;
//using com.dxfeed.api.connection;
//using com.dxfeed.api.data;
//using com.dxfeed.api.events;
//using com.dxfeed.native;

/*

namespace OptionView
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

    public class Greeks : Dictionary<string,Greek>
    {
        public Greeks ()
        {

        }
        public Greeks (Dictionary<string,Greek> list)
        {
            foreach (KeyValuePair<string,Greek> pair in list)
            {
                this.Add(pair.Key, pair.Value);
            }
        }
    }

    public class Quote
    {
        [Flags] public enum State { None = 0, Trade = 1, Quote = 2, All = 3 };

        public decimal Price { set; get; }
        public decimal LastPrice { set; get; }
        public decimal Change { set; get; }
        public State Complete = State.None;
    }


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



    public class EventListener : IDxTradeListener, IDxProfileListener, IDxUnderlyingListener, IDxGreeksListener, IDxCandleListener, IDxQuoteListener
    {
        public void OnTrade<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxTrade
        {
            foreach (TE item in buf)
            {
                Debug.WriteLine($"Listening to {buf.Symbol}  Price: {item.Price}");

                Quote qt = null;
                if (DataFeed.ReturnPriceList.ContainsKey(buf.Symbol))
                {
                    qt = DataFeed.ReturnPriceList[buf.Symbol];
                }
                else
                { 
                    qt = new Quote();
                    DataFeed.ReturnPriceList.Add(buf.Symbol, qt);
                }
                if (!Double.IsNaN(item.Price)) qt.LastPrice = Convert.ToDecimal(item.Price);
                if (!Double.IsNaN(item.Change)) qt.Change = Convert.ToDecimal(item.Change);
                qt.Complete |= Quote.State.Trade;
            }
        }
        public void OnQuote<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxQuote
        { 
            foreach (TE item in buf)
            {
                Debug.WriteLine($"Listening to {buf.Symbol}  AskPrice: {item.AskPrice}  BidPrice: {item.BidPrice}");
                Debug.WriteLine("       Average: {0}", (item.AskPrice + item.BidPrice) / 2);

                Quote qt = null;
                if (DataFeed.ReturnPriceList.ContainsKey(buf.Symbol))
                {
                    qt = DataFeed.ReturnPriceList[buf.Symbol];
                }
                else
                {
                    qt = new Quote();
                    DataFeed.ReturnPriceList.Add(buf.Symbol, qt);
                }
                if (!Double.IsNaN(item.AskPrice) && !Double.IsNaN(item.BidPrice)) qt.Price = Convert.ToDecimal((item.AskPrice + item.BidPrice) / 2);
                qt.Complete |= Quote.State.Quote;
            }
        }

        public void OnProfile<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxProfile
        {
            foreach (TE item in buf)
            {
                //Debug.WriteLine($"Listening to {buf.Symbol}"); //  Price: {item.Price}");

                if (!DataFeed.ReturnProfileList.ContainsKey(item.EventSymbol))
                {
                    DataFeed.ReturnProfileList.Add(item.EventSymbol, item.Description);
                    DataFeed.symbolCount -= 1;
                }
            }
        }

        public void OnUnderlying<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxUnderlying
        {
            foreach (TE item in buf)
            {
                //Debug.WriteLine($"Listening to {buf.Symbol}"); //  Price: {item.Price}");

                double volume = (Double.IsNaN(item.OptionVolume)) ? 0 : item.OptionVolume;
                if (!DataFeed.ReturnOptionVolumeList.ContainsKey(item.EventSymbol))
                {
                    DataFeed.ReturnOptionVolumeList.Add(item.EventSymbol, volume);
                    DataFeed.symbolCount -= 1;
                }
            }
        }

        public void OnGreeks<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxGreeks
        {
            foreach (TE item in buf)
            {
                //Debug.WriteLine($"Listening to {buf.Symbol}  IV: {item.Volatility}");

                Greek grk = new Greek();
                grk.Price = item.Price;
                grk.Volatility = item.Volatility;
                grk.Delta = item.Delta;
                grk.Theta = item.Theta;
                grk.Gamma = item.Gamma;
                grk.Rho = item.Rho;
                grk.Vega = item.Vega;

                if (!DataFeed.ReturnList.ContainsKey(buf.Symbol))
                {
                    DataFeed.ReturnList.Add(buf.Symbol, grk);
                    DataFeed.symbolCount -= 1;
                }
            }
        }

        //int x = 0;
        int firstSymbolLength = 0;
        public void OnCandle<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxCandle
        {
            foreach (TE item in buf)
            {
                //Debug.WriteLine($"Listening to {buf.Symbol}  {++x}   date: {item.Time}   price: {item.Close}  {item.ImpVolatility}");

                string sym = buf.Symbol.Replace("{=d}", "");

                Candle cdl = new Candle();
                cdl.Day = item.Time;
                cdl.Price = Convert.ToDecimal(item.Close);
                if (!Double.IsNaN(item.ImpVolatility)) cdl.IV = Convert.ToDecimal(item.ImpVolatility);

                if (!DataFeed.ReturnCandleList.ContainsKey(sym))
                {
                    DataFeed.ReturnCandleList.Add(sym, new Candles());
                }
                if (!DataFeed.ReturnCandleList[sym].ContainsKey(cdl.Day))
                { 
                    DataFeed.ReturnCandleList[sym].Add(cdl.Day, cdl);
                }

                if ((cdl.Day == DataFeed.FirstDate) && (firstSymbolLength == 0))
                {
                    firstSymbolLength = DataFeed.ReturnCandleList[sym].Count;
                    //Debug.WriteLine($"  first date found: {DataFeed.FirstDate}   length = {firstSymbolLength}");
                }

                bool allSymbolsComplete = false;
                if ((firstSymbolLength > 0) && (DataFeed.ReturnCandleList.Count == DataFeed.symbolCount))
                {
                    allSymbolsComplete = true;
                    foreach (KeyValuePair<string, Candles> c in DataFeed.ReturnCandleList)
                    {
                        Candles cdls = c.Value;
                        allSymbolsComplete &= (cdls.Count == firstSymbolLength);

                        //Debug.WriteLine($"  checking length of: {c.Key, 16}   length = {cdls.Count}    status: {allSymbolsComplete}");
                    }
                }

                if (allSymbolsComplete)
                {
                    DataFeed.symbolCount = 0;
                }
            }
        }

    }


    static class DataFeed
    {
        static public int symbolCount = 0;
        static private IDxSubscription subsciption = null;
        static private NativeConnection connection = null;
        static internal Greeks ReturnList = new Greeks();
        static internal Dictionary<string, Quote> ReturnPriceList = new Dictionary<string, Quote>();
        static internal Dictionary<string, string> ReturnProfileList = new Dictionary<string, string>();
        static internal Dictionary<string, double> ReturnOptionVolumeList = new Dictionary<string, double>();
        static internal Dictionary<string, Candles> ReturnCandleList = new Dictionary<string, Candles>();
        static internal DateTime FirstDate = DateTime.MinValue;
        static StreamingParams streaming = null;


        static public Greeks GetGreeks(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Greeks from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnList.Clear();

                subsciption = connection.CreateSubscription(EventType.Greeks, listener);
                symbolCount = symbols.Count;

                foreach (string sym in symbols)
                {
                    subsciption.AddSymbol(sym);
                }

                //Debug.WriteLine("waiting...");
                int i = 100;
                while ((symbolCount > 0) && (i > 0))
                {
                    Thread.Sleep(100);
                    i--;
                }
                //Debug.WriteLine("done...");
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return new Greeks(ReturnList);

        }


        static public Dictionary<string, Quote> GetPrices(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Prices from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnPriceList.Clear();

                subsciption = connection.CreateSubscription(EventType.Trade | EventType.Quote, listener);
                symbolCount = symbols.Count;

                foreach (string sym in symbols)
                {
                    subsciption.AddSymbol(sym);
                }

                //Debug.WriteLine("waiting...");
                int i = 100;
                while (i > 0)
                {
                    bool done = true;
                    foreach (string sym in symbols)
                    {
                        if (ReturnPriceList.ContainsKey(sym))
                        {
                            Quote qt = ReturnPriceList[sym];
                            if (qt.Complete != Quote.State.All) done = false;
                        }
                        else
                        {
                            done = false;
                        }
                        if (!done) break;
                    }
                    if (done) break;


                    Thread.Sleep(100);
                    i--;
                }
                Debug.WriteLine("done...  Requested: {0}, left {1}, returned {2}, timer {3}", symbols.Count, symbolCount, ReturnPriceList.Count, i);
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return new Dictionary<string, Quote>(ReturnPriceList);

        }


        static public decimal GetPrice(string symbol)
        {
            if (symbol == null) return 0;

            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Price from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnPriceList.Clear();

                subsciption = connection.CreateSubscription(EventType.Trade, listener);
                symbolCount = 1;
                subsciption.AddSymbol(symbol);

                //Debug.WriteLine("waiting...");
                int i = 100;
                while ((symbolCount > 0) && (i > 0))
                {
                    Thread.Sleep(100);
                    i--;
                }
                //Debug.WriteLine("done...");
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return ReturnPriceList.ContainsKey(symbol) ? Convert.ToDecimal(ReturnPriceList[symbol].LastPrice) : 0;
        }


        static public Dictionary<string, string> GetProfiles(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Profiles from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnProfileList.Clear();

                subsciption = connection.CreateSubscription(EventType.Profile, listener);
                symbolCount = symbols.Count;

                foreach (string sym in symbols)
                {
                    subsciption.AddSymbol(sym);
                }

                //Debug.WriteLine("waiting...");
                int i = 100;
                while ((symbolCount > 0) && (i > 0))
                {
                    Thread.Sleep(100);
                    i--;
                }
                //Debug.WriteLine("done...");
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return new Dictionary<string, string>(ReturnProfileList);

        }

        static public Dictionary<string, double> GetVolumes(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Volumes from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnOptionVolumeList.Clear();

                subsciption = connection.CreateSubscription(EventType.Underlying, listener);
                symbolCount = symbols.Count;

                foreach (string sym in symbols)
                {
                    subsciption.AddSymbol(sym);
                }

                //Debug.WriteLine("waiting...");
                int i = 100;
                while ((symbolCount > 0) && (i > 0))
                {
                    Thread.Sleep(100);
                    i--;
                }
                //Debug.WriteLine("done...");
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return new Dictionary<string, double>(ReturnOptionVolumeList);

        }

        static public Dictionary<string, Candles> GetHistory(List<string> symbols, DateTime startTime)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Volumes from Datafeed");

            var listener = new EventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnCandleList.Clear();
                FirstDate = startTime;

                subsciption = connection.CreateSubscription(startTime, listener);
                symbolCount = symbols.Count;

                foreach (string sym in symbols)
                {
                    subsciption.AddSymbol(sym + "{=1d}");
                }

                //Debug.WriteLine("waiting...");
                int i = 100;
                while ((symbolCount > 0) && (i > 0))
                {
                    Thread.Sleep(100);
                    i--;
                }
                //Debug.WriteLine("done...");
            }
            catch (DxException dxException)
            {
                Debug.WriteLine($"Datafeed: Native exception occurred: {dxException.Message}");
            }
            catch (Exception exc)
            {
                Debug.WriteLine($"Datafeed: Exception occurred: {exc.Message}");
            }
            finally
            {
                subsciption?.Dispose();
            }

            return new Dictionary<string, Candles>(ReturnCandleList);

        }

    }
}






*/

