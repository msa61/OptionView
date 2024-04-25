using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;


namespace DxLink
{
    public class DxHandler
    {
        public event DxHandlerEvent dxHandlerEvent;

        public enum DxDebugLevel
        {
            None,
            Primary,
            Verbose
        }
        public enum TimeSeriesType
        {
            Day,
            Hour,
            Minute
        }
        public DxDebugLevel DebugLevel = DxDebugLevel.None;

        private DxStream dxStream;
        private Subscriptions subscriptions = new Subscriptions();  // list of all quotes being managed by the handler
        private MessageWindow dxWindow = null;
        private int lastTSChannel = -1;

        private List<string> ignoreList = new List<string>() { "NDX", "XSP", "RUT", "VIX", "SPX" };

        public DxHandler(string webSocketUrl, string token, MessageWindow msgWindow, DxDebugLevel dbgLevel = DxDebugLevel.None)
        {
            dxWindow = msgWindow;
            DebugLevel = dbgLevel;

            // initial websocket layer
            dxStream = new DxStream(webSocketUrl, token, this);
            dxStream.MessageReceived += DxMessageReceivedHandler;
        }

        public void Close()
        {
            CloseDxStream();

            if (dxWindow != null)
            {
                dxWindow.Close();
                dxWindow = null;
            }
        }

        private void CloseDxStream()
        {
            if (dxStream != null)
            {
                _ = dxStream.Close();
                dxStream.MessageReceived -= DxMessageReceivedHandler;
                dxStream = null;
            }
        }


        public void MessageWindow(string message)
        {
            if (dxWindow == null) return;
            if (((Dispatcher)dxWindow.Dispatcher).CheckAccess())
            {
                dxWindow.WriteMessage(message);
            }
            else
            {
                dxWindow.Dispatcher.Invoke(() =>
                {
                    dxWindow.WriteMessage(message);
                });
            }
        }

        public void Subscribe (string symbol, SubscriptionType type = SubscriptionType.All)
        {
            if (ignoreList.Contains(symbol)) return;

            Subscription sub = subscriptions.Add(symbol, type);
            sub.Stream = true;
            dxStream.Subscribe(symbol, SubscriptionType.All);
        }

        public async Task<Quote> GetQuote(string symbol)
        {
            if (ignoreList.Contains(symbol)) return new Quote();

            Subscription retval = subscriptions.Add(symbol);
            if (dxWindow != null) dxWindow.WriteStatus(subscriptions.GetStatusText());

            dxStream.Subscribe(symbol, SubscriptionType.All);
            await WaitForJobComplete();

            subscriptions.Remove(symbol);  // remove from main list, return reference to one item in the collection
            return retval.Quote;
        }


        public async Task<Dictionary<string,Quote>> GetQuotes(List<string> symbols)
        {
            symbols = RemoveIgnoredSymbols(symbols);

            Subscriptions list = subscriptions.Add(symbols);
            if (dxWindow != null) dxWindow.WriteStatus(subscriptions.GetStatusText());

            foreach (string symbol in symbols)
            {
                dxStream.Subscribe(symbol, SubscriptionType.All);
            }
            await WaitForJobComplete();

            subscriptions.Remove(symbols);

            Dictionary<string,Quote> retval = new Dictionary<string,Quote>();
            foreach (KeyValuePair<string,Subscription> item in list)
            {
                retval.Add(item.Key, item.Value.Quote);
            }
            return retval;
        }

        List<string> RemoveIgnoredSymbols(List<string> symbols)
        {
            List<string> retval = new List<string>();
            foreach (string s in symbols)
            {
                if (!ignoreList.Contains(s)) retval.Add(s);
            }
            return retval;
        }
        public async Task<bool> WaitForJobComplete()
        {
            int abortCount = 50;
            while (!subscriptions.IsComplete())
            {
                Debug.WriteLine($"waiting for data .... ");
                Debug.WriteLine(subscriptions.GetStatusText());
                await Task.Delay(TimeSpan.FromSeconds(0.2), CancellationToken.None);
                abortCount--;
                if (abortCount == 0) return false;
            }
            return true;
        }

        public async Task<Candles> GetTimeSeries(string symbol, TimeSeriesType type, DateTime startTime)
        {
            if (ignoreList.Contains(symbol)) return null;

            Subscription tmpSub = subscriptions.Add(symbol, SubscriptionType.TimeSeries);
            if (dxWindow != null) dxWindow.WriteStatus(subscriptions.GetStatusText());

            dxStream.Subscribe(symbol, SubscriptionType.TimeSeries, type, startTime);
            await WaitForJobComplete();

            subscriptions.Remove(symbol);
            if (lastTSChannel > 0) dxStream.CloseChannel(lastTSChannel);  // remove doesn't seem to work

            return tmpSub.Candles;
        }

        public async Task<Subscriptions> GetTimeSeries(List<string> symbols, TimeSeriesType type, DateTime startTime)
        {
            symbols = RemoveIgnoredSymbols(symbols);

            Subscriptions list = subscriptions.Add(symbols, SubscriptionType.TimeSeries);
            if (dxWindow != null) dxWindow.WriteStatus(subscriptions.GetStatusText());

            dxStream.Subscribe(symbols, SubscriptionType.TimeSeries, type, startTime);
            await WaitForJobComplete();

            subscriptions.Remove(symbols);
            if (lastTSChannel > 0) dxStream.CloseChannel(lastTSChannel);

            return list;
        }

        private void DxMessageReceivedHandler(object sender, List<DxMessageReceivedEventArgs> list, int channel)
        {
            //Debug.WriteLine("DX Event received: ");
            string symbol;

            if (list == null)
            {
                Debug.Write("x");
            }
            else
            {
                try
                {
                    // check type of first one
                    if (list[0].GetType() == typeof(DxCandleMessageEventArgs))
                    {
                        // deal with the candles differently
                        List<DateTime> times = list.Select(x => ((DxCandleMessageEventArgs)x).Time).ToList();
                        MessageWindow(String.Format($"\nCandle: mintime: {times.Min()}  maxtime: {times.Max()}"));

                        lastTSChannel = channel;
                        Dictionary<string,string> symbolsProcessed = new Dictionary<string,string>();

                        // process message list
                        foreach (DxMessageReceivedEventArgs e in list)
                        {
                            // ignore instances without a price
                            DxCandleMessageEventArgs candleData = (DxCandleMessageEventArgs)e;
                            if (!double.IsNaN(candleData.Price))
                            {
                                //parse symbol
                                int i = candleData.Symbol.IndexOf("{");
                                symbol = candleData.Symbol.Substring(0, i);
                                if (!symbolsProcessed.ContainsKey(symbol)) symbolsProcessed.Add(symbol, candleData.Symbol);

                                if (subscriptions.ContainsKey(symbol))
                                {
                                    Subscription sub = subscriptions[symbol];
                                    if (sub != null)
                                    {
                                        Candle candle = new Candle()
                                        {
                                            Day = candleData.Time,
                                            Price = Convert.ToDecimal(candleData.Price),
                                            IV = Convert.ToDecimal(candleData.IV)
                                        };
                                        if (!sub.Candles.ContainsKey(candle.Day)) sub.Candles.Add(candle.Day, candle);
                                    }
                                }
                            }
                        }

                        foreach(KeyValuePair<string,string> item in symbolsProcessed)
                        {
                            dxStream.Unsubscribe(item.Value, SubscriptionType.TimeSeries, channel);

                            if (subscriptions.ContainsKey(item.Key))
                            {
                                Subscription sub = subscriptions[item.Key];
                                if (sub != null)
                                {
                                    sub.Status &= ~SubscriptionType.TimeSeries;
                                    if (sub.Candles.Count > 0)
                                    {
                                        dxHandlerEvent?.Invoke(this, DxHandlerEventType.TimeSeries, null);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // anything other than candles
                        foreach (DxMessageReceivedEventArgs e in list)
                        {
                            symbol = e.Symbol;
                            Quote curQuote = null;
                            switch (e.Type)
                            {
                                case DxMessageType.Heartbeat:
                                    MessageWindow("\nHeartbeat");
                                    break;
                                case DxMessageType.Trade:
                                    DxTradeMessageEventArgs args = (DxTradeMessageEventArgs)e;

                                    DateTime local = TimeZoneInfo.ConvertTimeFromUtc(args.Time, TimeZoneInfo.Local);
                                    MessageWindow(String.Format($"\nTrade: {args.Symbol}   price: {args.Price:N2}   @ {local:hh:mm:ss}"));

                                    if (subscriptions.Subscribed(args.Symbol))
                                    {
                                        curQuote = ManageSubscription(args.Symbol, SubscriptionType.Trade);

                                        curQuote.LastPrice = Convert.ToDecimal(args.Price);
                                        curQuote.Change = Convert.ToDecimal(args.Change);
                                        curQuote.LastTrade = args.Time;
                                        curQuote.Volume = args.Volume;
                                    }
                                    break;
                                case DxMessageType.Quote:
                                    DxQuoteMessageEventArgs qArgs = (DxQuoteMessageEventArgs)e;
                                    MessageWindow(String.Format($"\nQuote: {qArgs.Symbol}   askPrice: {qArgs.AskPrice:N2}   bidPrice: {qArgs.BidPrice:N2} "));

                                    if (subscriptions.Subscribed(qArgs.Symbol))
                                    {
                                        curQuote = ManageSubscription(qArgs.Symbol, SubscriptionType.Quote);

                                        curQuote.AskPrice = Convert.ToDecimal(qArgs.AskPrice);
                                        curQuote.BidPrice = Convert.ToDecimal(qArgs.BidPrice);
                                        curQuote.Price = (curQuote.AskPrice + curQuote.BidPrice) / 2;
                                        curQuote.Spread = Convert.ToDecimal(qArgs.AskPrice - qArgs.BidPrice);
                                    }
                                    break;
                                case DxMessageType.Greeks:
                                    DxGreeksMessageEventArgs gArgs = (DxGreeksMessageEventArgs)e;
                                    MessageWindow(String.Format($"\nGreeks: {gArgs.Symbol}   price: {gArgs.Price:N2}   IV: {gArgs.IV:P2}   Delta: {gArgs.Delta:P2} "));

                                    if (subscriptions.Subscribed(gArgs.Symbol))
                                    {
                                        curQuote = ManageSubscription(gArgs.Symbol, SubscriptionType.Greek);
                                        curQuote.Delta = gArgs.Delta;
                                        curQuote.IV = gArgs.IV;
                                        curQuote.GreekPrice = gArgs.Price;
                                    }

                                    // remove fringe options
                                    if ((curQuote != null) && (curQuote.Delta < 0.10 || curQuote.Delta > 0.9))
                                        dxStream.Unsubscribe(gArgs.Symbol, SubscriptionType.All);
                                    break;
                                case DxMessageType.Summary:
                                    DxSummaryMessageEventArgs sArgs = (DxSummaryMessageEventArgs)e;

                                    MessageWindow(String.Format($"\nSummary: {sArgs.Symbol}   PrevDayClosePrice: {sArgs.PrevDayClosePrice}   openInterest: {sArgs.OpenInterest} "));
                                    break;
                                case DxMessageType.Profile:
                                    DxProfileMessageEventArgs pArgs = (DxProfileMessageEventArgs)e;
                                    MessageWindow(String.Format($"\nProfile: {pArgs.Symbol}   description: {pArgs.Description} "));

                                    if (subscriptions.Subscribed(pArgs.Symbol))
                                    {
                                        curQuote = ManageSubscription(pArgs.Symbol, SubscriptionType.Profile);

                                        curQuote = subscriptions[pArgs.Symbol].Quote;
                                        curQuote.Description = pArgs.Description;
                                    }
                                    break;
                                case DxMessageType.TheoPrice:
                                    DxTheoPriceMessageEventArgs tpArgs = (DxTheoPriceMessageEventArgs)e;

                                    MessageWindow(String.Format($"\nTheoPrice: {tpArgs.Symbol}   price: {tpArgs.Price:N2}  int: {tpArgs.Interest:P2}  div: {tpArgs.Dividend}"));
                                    break;
                                default:
                                    MessageWindow("\n" + e.Message);
                                    //tbDump.Text += e.DebugText;
                                    break;
                            }

                            if (subscriptions.Subscribed(symbol))
                            {
                                dxHandlerEvent?.Invoke(this, DxHandlerEventType.QuoteTouched, curQuote);

                                if (subscriptions[symbol].IsComplete)
                                {
                                    dxHandlerEvent?.Invoke(this, DxHandlerEventType.SymbolComplete, curQuote);
                                }
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Write("stop1 :" + e.Message);
                }
            }

            try
            {
                if (dxWindow != null) dxWindow.WriteStatus(subscriptions.GetStatusText());

                if (subscriptions.IsComplete())
                {
                    dxHandlerEvent?.Invoke(this, DxHandlerEventType.AllComplete, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating status: {ex.Message}");
            }
        } //return


        private Quote ManageSubscription(string symbol, SubscriptionType type)
        {
            if (!subscriptions.ContainsKey(symbol)) return null;
            Subscription sub = subscriptions[symbol];
            sub.Status &= ~type;
            if (!sub.Stream) dxStream.Unsubscribe(symbol, type);

            return sub.Quote;
        }

        public string GetCandleCode(DxHandler.TimeSeriesType type)
        {
            string retval = "";
            switch (type)
            {
                default:
                case DxHandler.TimeSeriesType.Day:
                    retval = "{=d}";
                    break;
                case DxHandler.TimeSeriesType.Hour:
                    retval = "{=h}";
                    break;
                case DxHandler.TimeSeriesType.Minute:
                    retval = "{=m}";
                    break;
            }
            return retval;
        }
    }

    public delegate void DxHandlerEvent(object sender, DxHandlerEventType e, Quote quote);

    public enum DxHandlerEventType
    {
        None,
        SymbolComplete,
        AllComplete,
        QuoteTouched,
        TimeSeries
    }


    public class DxStatusParams
    {
        public int Count { get; set; } = 0;
        public int RemainingTrade { get; set; } = 0;
        public int RemainingQuote { get; set; } = 0;
        public int RemainingProfile { get; set; } = 0;
        public int RemainingGreek { get; set; } = 0;
        public int RemainingTimeSeries { get; set; } = 0;
    }




}
