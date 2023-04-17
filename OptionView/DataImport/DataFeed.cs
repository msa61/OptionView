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

                if (!Double.IsNaN(item.Change))
                {
                    Quote qt = new Quote();
                    qt.Price = Convert.ToDecimal(item.Price);
                    qt.Change = Convert.ToDecimal(item.Change);

                    if (!DataFeed.ReturnPriceList.ContainsKey(buf.Symbol))
                    {
                        DataFeed.ReturnPriceList.Add(buf.Symbol, qt);
                        DataFeed.symbolCount -= 1;
                    }
                }
                else
                {
                    Debug.WriteLine("X");
                }
            }
        }
    }

    public class ProfileEventListener : IDxProfileListener
    {
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
    }

    public class UnderlyingEventListener : IDxUnderlyingListener
    {
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
        static StreamingParams streaming = null;


        static public Greeks GetGreeks(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Greeks from Datafeed");

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


        static public Dictionary<string, Quote> GetPrices(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Prices from Datafeed");

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


        static public Dictionary<string, string> GetProfiles(List<string> symbols)
        {
            if (streaming == null) streaming = TastyWorks.StreamingInfo();

            App.UpdateStatusMessage("Profiles from Datafeed");

            var listener = new ProfileEventListener();
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

            var listener = new UnderlyingEventListener();
            connection = new NativeConnection(streaming.Address, streaming.Token, connect => { });

            try
            {
                ReturnProfileList.Clear();

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

    }
}











