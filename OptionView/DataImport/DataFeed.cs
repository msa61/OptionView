using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using com.dxfeed.api;
using com.dxfeed.api.connection;
using com.dxfeed.api.data;
using com.dxfeed.api.events;
using com.dxfeed.native;




namespace OptionView.DataImport
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



    public class GreekEventListener : IDxGreeksListener
    {
        public void OnGreeks<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxGreeks
        {
            foreach (TE item in buf)
            {
                Debug.WriteLine($"Listening to {buf.Symbol}  IV: {item.Volatility}");

                Greek grk = new Greek();
                grk.Price = item.Price;
                grk.Volatility = item.Volatility;
                grk.Delta = item.Delta;
                grk.Theta = item.Theta;
                grk.Gamma = item.Gamma;
                grk.Rho = item.Rho;
                grk.Vega = item.Vega;

                DataFeed.AddGreek(buf.Symbol, grk);
                DataFeed.symbolCount -= 1;
            }
        }
    }

    public class Quote
    {
        public decimal Price { set; get; }
        public decimal Change { set; get; }
    }

    public class TradeEventListener : IDxTradeListener
    {
        public void OnTrade<TB, TE>(TB buf) where TB : IDxEventBuf<TE> where TE : IDxTrade
        {
            foreach (TE item in buf)
            {
                Debug.WriteLine($"Listening to {buf.Symbol}  Price: {item.Price}");

                Quote qt = new Quote();
                qt.Price = Convert.ToDecimal(item.Price);
                qt.Change = Convert.ToDecimal(item.Change);

                DataFeed.AddTrade(buf.Symbol, qt);
                DataFeed.symbolCount -= 1;
            }
        }
    }





    static class DataFeed
    {
        static public int symbolCount = 0;
        static private IDxSubscription subsciption = null;
        static private NativeConnection connection = null;
        static private Greeks ReturnList = new Greeks();
        static private Dictionary<string, Quote> ReturnPriceList = new Dictionary<string, Quote>();
        static StreamingParams streaming = null;


        static public void AddGreek(string sym, Greek greek)
        {
            if (!ReturnList.ContainsKey(sym)) ReturnList.Add(sym, greek);
        }

        static public Greeks GetGreeks(List<string> symbols)
        {
            App.UpdateLoadStatusMessage("Greeks from Datafeed");

            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            var listener = new GreekEventListener();
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


        static public void AddTrade(string sym, Quote qt)
        {
            if (!ReturnPriceList.ContainsKey(sym)) ReturnPriceList.Add(sym, qt);
        }

        static public Dictionary<string, Quote> GetPrices(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            var listener = new TradeEventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnPriceList.Clear();

                subsciption = connection.CreateSubscription(EventType.Trade, listener);
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

            return new Dictionary<string, Quote>(ReturnPriceList);

        }


        static public decimal GetPrice(string symbol)
        {
            if (symbol == null) return 0;

            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            var listener = new TradeEventListener();
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

            return ReturnPriceList.ContainsKey(symbol) ? Convert.ToDecimal(ReturnPriceList[symbol].Price) : 0;
        }


    }
}











