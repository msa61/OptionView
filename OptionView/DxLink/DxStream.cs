using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace DxLink
{
    // handles all aspects of the websocket


    internal class DxStream
    {
        // Define the event.
        public event DxMessageReceivedHandler MessageReceived;
        private string token;
        private string url;
        private string userID = "";
        private int lastChannelRequested = 1;  // main channel, next will increment by 2
        private int lastChannel = -1;
        private ClientWebSocket webSocket = null;
        private TSWebSocketHandler socketHandler = null;
        private DxHandler dxHandler = null;


        public DxStream(string dxAddress, string dxToken, DxHandler handler)
        {
            token = dxToken;
            url = dxAddress;
            dxHandler = handler;

            Setup();  // establishes and opens websocket
            _ = CreateMessageLoop();
            CreateHeartbeatProcess();
        }

        private void Setup()
        {
            webSocket = new ClientWebSocket();
            socketHandler = new TSWebSocketHandler(webSocket);
            Uri serverUri = new Uri(url);

            try
            {
                webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(60);
                webSocket.ConnectAsync(serverUri, CancellationToken.None).Wait();

                if (webSocket.State == WebSocketState.Open)
                {
                    JToken response = SendCommand("setup");
                    response = SendCommand("auth");
                    if (((JObject)response).ContainsKey("userId"))
                    {
                        userID = response["userId"].ToString();
                    }
                    SendCommand("openChannel");
                    SendCommand("feedSetup", lastChannel);
                }
            }
            catch (WebSocketException webEx)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Websocket(setup): Exception occurred: {webEx.Message}");
                Debug.WriteLine($"Websocket(setup): Exception occurred: {webEx.Message}");
            }
            catch (Exception ex)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Websocket(setup): Exception occurred: {ex.Message}");
                Debug.WriteLine($"Websocket(setup): Exception occurred: {ex.Message}");
            }
        }

        private void CreateHeartbeatProcess()
        {
            //Start a separate task to send the heartbeat message every 30 seconds
            CancellationTokenSource heartbeatCancellation = new CancellationTokenSource();
            Task task = Task.Run(async () =>
            {
                while (!heartbeatCancellation.Token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCancellation.Token);

                        string message = GetGenericMessage("heartbeat");

                        await socketHandler.QueueMessageAsync(message);
                        //byte[] heartbeatBuffer = Encoding.UTF8.GetBytes(message);
                        //await webSocket.SendAsync(new ArraySegment<byte>(heartbeatBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        Debug.WriteLine($"Heartbeat sent: " + webSocket.State.ToString());
                    }
                    catch (WebSocketException ex)
                    {
                        Debug.WriteLine("Exception sending heartbeat message: " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception sending heartbeat message: {ex.Message}");
                    }
                }
            });
        }

        private async Task CreateMessageLoop()
        {
            while ((webSocket.State == WebSocketState.Open) || (webSocket.State == WebSocketState.CloseSent))
            {
                //Debug.WriteLine("dxLoop...");
                try
                {
                    WebSocketReceiveResult result = null;
                    byte[] buffer = new byte[2048];
                    string fullResponse = "";
                    do
                    {
                        //Debug.Write(".");
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        string response2 = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        fullResponse += response2;
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.WriteLine("Received close message");
                        break;
                    }

                    //if (fullResponse.Substring(0, 7) != "[{\"data")
                    //{
                    //Debug.WriteLine("DXStream response length: " + fullResponse.Count().ToString());
                    //Debug.WriteLine("DXStream response: " + "\n" + fullResponse + "\n");

                    JObject debug = JsonConvert.DeserializeObject<JObject>(fullResponse);

                    switch (dxHandler.DebugLevel)
                    {
                        case DxHandler.DxDebugLevel.Primary:
                            dxHandler.MessageWindow("\n\t\tReceived: " + DebugParse(fullResponse));
                            break;
                        case DxHandler.DxDebugLevel.Verbose:
                            dxHandler.MessageWindow("\nReceived:\n" + JsonConvert.SerializeObject(debug, Formatting.Indented));
                            //Debug.WriteLine("\nReceived:\n" + JsonConvert.SerializeObject(debug, Formatting.Indented));
                            break;
                    }

                    //}
                    if (fullResponse.Length > 0)
                    {
                        int channel = 0;
                        List<DxMessageReceivedEventArgs> args = ParseMessage(fullResponse, out channel);
                        //Debug.WriteLine("Raising TW Event");
                        if (args != null) MessageReceived?.Invoke(this, args, channel);
                        //Debug.WriteLine("Returned from TW Event");
                    }
                }
                catch (WebSocketException webEx)
                {
                    if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Websocket(web): Exception occurred: {webEx.Message}");
                    Debug.WriteLine($"Websocket(web): Exception occurred: {webEx.Message}");
                }
                catch (Exception ex)
                {
                    if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Websocket: Exception occurred: {ex.Message}");
                    Debug.WriteLine($"Websocket: Exception occurred: {ex.Message}");
                }
                //Debug.WriteLine("dxLoop - end");
            }

        }

        // reopen new channel
        private async Task<int> OpenChannel()
        {
            Debug.WriteLine(webSocket.State);
            lastChannelRequested += 2;
            string message = GetGenericMessage("openChannel", channel: lastChannelRequested);
            await SendMessage(message);

            int abortCount = 50;
            while ((lastChannelRequested != lastChannel) && abortCount > 0) 
            {
                await Task.Delay(TimeSpan.FromSeconds(0.2), CancellationToken.None);
                abortCount--;
            }

            message = GetGenericMessage("feedSetup", channel: lastChannel);
            await SendMessage(message);

            Debug.WriteLine($"last channel: {lastChannel}");
            return lastChannel;
        }

        private List<DxMessageReceivedEventArgs> ParseMessage(string str, out int channel)
        {
            //Debug.WriteLine("ParseMessage");
            channel = 0;

            List<DxMessageReceivedEventArgs> retlist = new List<DxMessageReceivedEventArgs>();

            JToken jsonMessage = JsonConvert.DeserializeObject<JToken>(str);

            if (jsonMessage["type"].ToString() == "CHANNEL_OPENED")
            {
                lastChannel = Convert.ToInt32(jsonMessage["channel"]);
                return null;
            }

            if (jsonMessage["type"].ToString() == "FEED_CONFIG") CacheFormat(jsonMessage);

            // skip processing anything other than data
            if (jsonMessage["type"].ToString() != "FEED_DATA") return null;

            channel = Convert.ToInt32(jsonMessage["channel"]);
            jsonMessage = jsonMessage["data"];

            if ((jsonMessage != null) && (jsonMessage[0].GetType() == typeof(JValue))) jsonMessage = ExpandJson(jsonMessage);

            for (int i = 0; i < jsonMessage.Count(); i++)
            {
                try
                {
                    JToken json = jsonMessage[i];
                    string type = json["eventType"].ToString();

                    switch (type)
                    {
                        case "Trade":
                            DxTradeMessageEventArgs tArgs = new DxTradeMessageEventArgs();
                            tArgs.Type = DxMessageType.Trade;
                            tArgs.Symbol = json["eventSymbol"].ToString();
                            tArgs.Price = Convert.ToDouble(json["price"]);
                            tArgs.Change = Convert.ToDouble(json["change"]);
                            tArgs.Volume = Convert.ToDouble(json["dayVolume"]);
                            Int64 unixTime = Convert.ToInt64(json["time"]);
                            tArgs.Time = DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime;

                            //tArgs.Debug = json;
                            //tArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(tArgs);
                            break;
                        case "Quote":
                            DxQuoteMessageEventArgs qArgs = new DxQuoteMessageEventArgs();
                            qArgs.Type = DxMessageType.Quote;
                            qArgs.Symbol = json["eventSymbol"].ToString();
                            qArgs.AskPrice = Convert.ToDouble(json["askPrice"]);
                            qArgs.BidPrice = Convert.ToDouble(json["bidPrice"]);

                            //qArgs.Debug = json;
                            //qArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(qArgs);
                            break;
                        case "Summary":
                            DxSummaryMessageEventArgs sArgs = new DxSummaryMessageEventArgs();
                            sArgs.Type = DxMessageType.Summary;
                            sArgs.Symbol = json["eventSymbol"].ToString();
                            //sArgs.OpenPrice = Convert.ToDouble(json["dayOpenPrice"]);
                            //sArgs.LowPrice = Convert.ToDouble(json["dayLowPrice"]);
                            //sArgs.HighPrice = Convert.ToDouble(json["dayHighPrice"]);
                            //sArgs.ClosePrice = Convert.ToDouble(json["dayClosePrice"]);
                            sArgs.PrevDayClosePrice = Convert.ToDouble(json["prevDayClosePrice"]);
                            sArgs.OpenInterest = Convert.ToInt32(json["openInterest"]);

                            //sArgs.Debug = json;
                            //sArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(sArgs);
                            break;
                        case "Greeks":
                            DxGreeksMessageEventArgs gArgs = new DxGreeksMessageEventArgs();
                            gArgs.Type = DxMessageType.Greeks;
                            gArgs.Symbol = json["eventSymbol"].ToString();
                            gArgs.Price = Convert.ToDouble(json["price"]);
                            gArgs.IV = Convert.ToDouble(json["volatility"]);
                            gArgs.Delta = Convert.ToDouble(json["delta"]);

                            //gArgs.Debug = json;
                            //gArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(gArgs);
                            break;
                        case "Profile":
                            DxProfileMessageEventArgs pArgs = new DxProfileMessageEventArgs();
                            pArgs.Type = DxMessageType.Profile;
                            pArgs.Symbol = json["eventSymbol"].ToString();
                            pArgs.Description = json["description"].ToString();

                            //pArgs.Debug = json;
                            //pArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(pArgs);
                            break;
                        case "TheoPrice":
                            DxTheoPriceMessageEventArgs tpArgs = new DxTheoPriceMessageEventArgs();
                            tpArgs.Type = DxMessageType.TheoPrice;
                            tpArgs.Symbol = json["eventSymbol"].ToString();
                            Int64 uTime = Convert.ToInt64(json["time"]);
                            tpArgs.Time = DateTimeOffset.FromUnixTimeMilliseconds(uTime).UtcDateTime;
                            tpArgs.Price = Convert.ToDouble(json["price"]);
                            tpArgs.UnderlyingPrice = Convert.ToDouble(json["underlyingPrice"]);
                            tpArgs.Delta = Convert.ToDouble(json["delta"]);
                            tpArgs.Gamma = Convert.ToDouble(json["gamma"]);
                            tpArgs.Dividend = Convert.ToDouble(json["dividend"]);
                            tpArgs.Interest = Convert.ToDouble(json["interest"]);

                            //tpArgs.Debug = json;
                            //tpArgs.DebugText = JsonConvert.SerializeObject(json, Formatting.Indented);

                            retlist.Add(tpArgs);
                            break;

                        case "Candle":
                            DxCandleMessageEventArgs cArgs = new DxCandleMessageEventArgs();
                            cArgs.Type = DxMessageType.Candle;
                            cArgs.Symbol = json["eventSymbol"].ToString();
                            Int64 unixTime2 = Convert.ToInt64(json["time"]);
                            cArgs.Time = DateTimeOffset.FromUnixTimeMilliseconds(unixTime2).UtcDateTime;
                            cArgs.Price = Convert.ToDouble(json["close"]);
                            if (json["impVolatility"].ToString() != "NaN") cArgs.IV = Convert.ToDouble(json["impVolatility"]);

                            retlist.Add(cArgs);
                            break;
                        default:
                            DxMessageReceivedEventArgs args = new DxMessageReceivedEventArgs()
                            {
                                Debug = json,
                                DebugText = JsonConvert.SerializeObject(json, Formatting.Indented)
                            };

                            retlist.Add(args);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Parse: Exception occurred: {ex.Message}");
                    //Debug.Write("stop2: " + ex.Message);
                }
            }

            return retlist;
        }

        Dictionary<string, List<string>> fieldCache = null;
        private void CacheFormat(JToken json)
        {
            if (fieldCache == null) fieldCache = new Dictionary<string, List<string>>();

            json = json["eventFields"];
            if(json == null) return; // sometimes FEED_CONFIG received without any field info
            foreach (JProperty t in json) //for (int i = 0; i < json.Count(); i++)
            {
                try
                {
                    if (fieldCache.ContainsKey(t.Name)) break;
                    List<string> list = new List<string>();
                    JArray arr = (JArray)t.Value;
                    foreach (string s in arr)
                    { 
                        list.Add(s);
                    }
                    fieldCache.Add(t.Name, list);
                }
                catch (Exception ex)
                {
                    if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"Parse: Exception occurred: {ex.Message}");
                    //Debug.Write("stop2: " + ex.Message);
                }
            }
        }

        private JToken ExpandJson(JToken json)
        {
            // fix compact format
            JArray retval = new JArray();

            try
            {
                for (int i = 0; i < json.Count() / 2; i++)
                {
                    JObject newObj = new JObject();
                    string type = json[i].ToString();
                    JToken list = json[i + 1];
                    int ndx = 0;
                    foreach (JValue v in list)
                    {
                        if (ndx >= fieldCache[type].Count)
                        {
                            // more data
                            retval.Add(newObj);
                            newObj = new JObject();
                            ndx = 0;
                        }

                        newObj.Add(new JProperty(fieldCache[type][ndx], v));
                        ndx++;
                    }
                    retval.Add(newObj);
                }
            }
            catch (Exception ex)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"ExpandJson: Exception occurred: {ex.Message}");
                //Debug.Write("stop2: " + ex.Message);
            }

            return retval;
        }

        private string DebugParse(string str)
        {
            string retStr = "<empty>";

            JToken jsonMessage = JsonConvert.DeserializeObject<JToken>(str);

            try
            {
                string type = jsonMessage["type"].ToString();
                retStr = "type: " + type;

                if (type == "AUTH_STATE")
                {
                    retStr += ", state: " + jsonMessage["state"].ToString();

                }
                else
                {
                    jsonMessage = jsonMessage["data"];
                    if (jsonMessage != null)
                    {
                        // in case its compact
                        if (jsonMessage[0].GetType() == typeof(JValue)) jsonMessage = ExpandJson(jsonMessage);
                        for (int i = 0; i < jsonMessage.Count(); i++)
                        {
                            JToken json = jsonMessage[i];
                            type = json["eventType"].ToString();

                            retStr += ", " + json["eventSymbol"].ToString() + "  " + type;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"DebugParse: Exception occurred: {ex.Message}");
                //Debug.Write("stop2: " + ex.Message);
            }

            return retStr;
        }



        //  synchronous outgoing message
        private JToken SendCommand(string type, int channel = 1)
        {
            //Debug.WriteLine("START sent command: " + type);

            string message = GetGenericMessage(type, channel: channel);

            string fullResponse = "";
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

                if (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = null;
                    buffer = new byte[2048];
                    do
                    {
                        result = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).Result;
                        string response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        fullResponse += response;
                        //Debug.WriteLine(response);
                    } while (!result.EndOfMessage);

                    
                    // second message send during "setup" is pulled and ignored
                    if (type == "setup")
                    {
                        do
                        {
                            result = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).Result;
                            string response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            //Debug.WriteLine("ignored: " + response);
                        } while (!result.EndOfMessage);
                    }

                    JObject json = JsonConvert.DeserializeObject<JObject>(fullResponse);

                    if (type == "openChannel")
                    {
                        lastChannel = Convert.ToInt32(json["channel"]);
                    }

                    switch (dxHandler.DebugLevel)
                    {
                        case DxHandler.DxDebugLevel.Primary:
                            dxHandler.MessageWindow("\n\t\tReceived (command): " + DebugParse(fullResponse));
                            break;
                        case DxHandler.DxDebugLevel.Verbose:
                            dxHandler.MessageWindow("\nReceived (command):\n" + JsonConvert.SerializeObject(json, Formatting.Indented));
                            break;
                    }

                }
            }
            catch (WebSocketException webEx)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"SendCommand(web): Exception occurred: {webEx.Message}");
                //Debug.WriteLine($"SendCommand: Exception occurred: {webEx.Message}");
            }
            catch (Exception ex)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"SendCommand: Exception occurred: {ex.Message}");
                //Debug.WriteLine($"SendCommand: Exception occurred: {ex.Message}");
            }


            JToken retval = JsonConvert.DeserializeObject<JToken>(fullResponse);
            //Debug.WriteLine("sent command: " + type);
            //Debug.WriteLine(fullResponse);
            return retval;
        }


        private async Task SendMessage(string message)
        {
            //Debug.WriteLine("SendMessage START: " + message);

            //textBox.Text += "\nsend message: " + message;
            try
            {
                await socketHandler.QueueMessageAsync(message);
                //byte[] buffer = Encoding.UTF8.GetBytes(message);
                //await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException webEx)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"SendMessage: Exception occurred: {webEx.Message}");
                //Debug.WriteLine($"SendMessage: Exception occurred: {webEx.Message}");
            }
            catch (Exception ex)
            {
                if (dxHandler.DebugLevel != DxHandler.DxDebugLevel.None) dxHandler.MessageWindow($"SendMessage: Exception occurred: {ex.Message}");
                //Debug.WriteLine($"SendMessage: Exception occurred: {ex.Message}");
            }

            //Debug.WriteLine("SendMessage END: " + message);
        }



        //
        //
        //   methods for retrieving data
        //
        //


        public void Subscribe(string symbol, SubscriptionType type)
        {
            string message = GetSubscribeMessage("subscribe", symbol, type, 1);
            _ = SendMessage(message);
        }

        public void Subscribe(List<string> symbols, SubscriptionType type)
        {
            foreach (string symbol in symbols)
            {
                Subscribe(symbol, type);
            }
        }

        public void Unsubscribe(string symbol, SubscriptionType type, int channel = 1)
        {
            string message = null;
            if (type == SubscriptionType.TimeSeries)
            {
                message = GetSubscribeMessage("removeTimeSeries", symbol, type, channel);
            }
            else
            {
                message = GetSubscribeMessage("remove", symbol, type, 1);
            }
            _ = SendMessage(message);
        }

        public async void Subscribe(string symbol, SubscriptionType sType, DxHandler.TimeSeriesType tsType, DateTime startTime, int channel = 0)
        {
            Debug.Assert(sType == SubscriptionType.TimeSeries);

            if (channel == 0) channel = await OpenChannel();

            string message = GetTimeMessage("addTimeSeries", symbol, channel, tsType, startTime);
            _ = SendMessage(message);
        }
        public async void Subscribe(List<string> symbols, SubscriptionType sType, DxHandler.TimeSeriesType tsType, DateTime startTime)
        {
            int channel = await OpenChannel();
            Debug.Assert(sType == SubscriptionType.TimeSeries);
            foreach (string symbol in symbols)
            {
                Subscribe(symbol, sType, tsType, startTime, channel);
            }
        }


        public void CloseChannel(int channel = -1)
        {
            string message = GetGenericMessage("closeChannel", channel);
            _ = SendMessage(message);
        }


        public async Task Close()
        {
            if (webSocket.State == WebSocketState.Open)
            {
                string message = GetGenericMessage("closeChannel");
                await SendMessage(message);

                webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
            }
            else
            {
                Debug.WriteLine("Already closed");
            }
        }


        // generic messages
        private string GetGenericMessage(string type, int channel = 1)
        {
            JObject main = new JObject();

            switch (type)
            {
                case "heartbeat":
                    main.Add("type", "KEEPALIVE");
                    main.Add("channel", 0);
                    break;

                case "setup":
                    main.Add("type", "SETUP");
                    main.Add("channel", 0);
                    main.Add("keepaliveTimeout", 60);
                    main.Add("acceptKeepaliveTimeout", 60);
                    main.Add("version", "0.1-js/1.0.0");
                    break;

                case "auth":
                    main.Add("type", "AUTH");
                    main.Add("channel", 0);
                    main.Add("token", token);
                    break;

                case "openChannel":
                    main.Add("type", "CHANNEL_REQUEST");
                    main.Add("channel", channel);
                    main.Add("service", "FEED");

                    JObject parameters = new JObject();
                    parameters.Add("contract", "AUTO");
                    main.Add("parameters", parameters);
                    break;

                case "feedSetup":
                    main.Add("channel", channel);
                    main.Add("type", "FEED_SETUP");
                    main.Add("acceptAggregationPeriod", 1);
                    main.Add("acceptDataFormat", "COMPACT");

                    JObject lst = new JObject();
                    if (channel == 1)
                    {
                        lst.Add("Trade", new JArray(new string[] { "eventType", "eventSymbol", "price", "change", "dayVolume", "time" }));
                        lst.Add("Quote", new JArray(new string[] { "eventType", "eventSymbol", "bidPrice", "askPrice" }));
                        lst.Add("Summary", new JArray(new string[] { "eventType", "eventSymbol", "openInterest", "prevDayClosePrice" }));
                        lst.Add("Greeks", new JArray(new string[] { "eventType", "eventSymbol", "price", "volatility", "delta" }));
                        lst.Add("Profile", new JArray(new string[] { "eventType", "eventSymbol", "description" }));
                    }
                    else
                    {
                        lst.Add("Candle", new JArray(new string[] { "eventType", "eventSymbol", "close", "time", "impVolatility" }));
                    }
                    main.Add("acceptEventFields", lst);

                    break;

                case "closeChannel":
                    main.Add("type", "CHANNEL_CANCEL");
                    main.Add("channel", channel);
                    break;
            }

            OutgoingDebugMessage(main, type);
            return JsonConvert.SerializeObject(main);
        }

        // subscription messages
        private string GetSubscribeMessage(string type, string symbol, SubscriptionType sType, int channel = 1)
        {
            JObject main = new JObject();

            switch (type)
            {
                case "subscribe":
                    main.Add("type", "FEED_SUBSCRIPTION");
                    main.Add("channel", channel);
                    main.Add("add", BuildSymbolList(symbol, sType));
                    break;

                case "remove":
                    main.Add("type", "FEED_SUBSCRIPTION");
                    main.Add("channel", channel);
                    main.Add("remove", BuildSymbolList(symbol, sType));
                    break;

                case "removeTimeSeries":
                    main.Add("type", "FEED_SUBSCRIPTION");
                    main.Add("channel", channel);
                    JArray remList = new JArray();
                    remList.Add(new JObject() { { "symbol", symbol }, { "type", "Candle" } });
                    main.Add("remove", remList);
                    break;
            }

            OutgoingDebugMessage(main, type, symbol + "  " + sType.ToString());
            return JsonConvert.SerializeObject(main);
        }

        // for timeseries messages
        private string GetTimeMessage(string type, string symbol, int channel, DxHandler.TimeSeriesType candleType, DateTime start = default(DateTime))
        {
            JObject main = new JObject();
            string candleCode = dxHandler.GetCandleCode(candleType);
            long fromTime = new DateTimeOffset(start).ToUnixTimeMilliseconds(); 

            switch (type)
            {
                case "addTimeSeries":
                    main.Add("type", "FEED_SUBSCRIPTION");
                    main.Add("channel", channel);

                    JArray addList = new JArray();
                    addList.Add(new JObject() { { "symbol", symbol + candleCode }, { "type", "Candle" }, { "fromTime", fromTime } });
                    main.Add("add", addList);
                    break;
            }

            OutgoingDebugMessage(main, type, symbol + "  " + candleCode);
            return JsonConvert.SerializeObject(main);
        }


        private void OutgoingDebugMessage(JObject json, string msgType, string notes = "")
        {
            switch (dxHandler.DebugLevel)
            {
                case DxHandler.DxDebugLevel.Primary:
                    string msg = "\n\tSent: " + msgType + "   " + notes;
                    dxHandler.MessageWindow(msg);
                    break;
                case DxHandler.DxDebugLevel.Verbose:
                    string debugText = JsonConvert.SerializeObject(json, Formatting.Indented);
                    dxHandler.MessageWindow("\nSent:\n" + debugText);
                    break;
            }
        }


        private JArray BuildSymbolList(string symbol, SubscriptionType? type)
        {
            JArray retlist = new JArray();

            if ((type & SubscriptionType.Trade) == SubscriptionType.Trade)
            {
                AddSub(retlist, "Trade", symbol);
            }
            if ((type & SubscriptionType.Quote) == SubscriptionType.Quote) 
            {
                AddSub(retlist, "Quote", symbol);
            }
            if ((type & SubscriptionType.Summary) == SubscriptionType.Summary)
            {
                AddSub(retlist, "Summary", symbol);  //temp test
            }

            if (symbol.Length < 6)
            {
                // for equities
                if ((type & SubscriptionType.Profile) == SubscriptionType.Profile)
                {
                    AddSub(retlist, "Profile", symbol);
                }
                //if ((type & SubscriptionType.TradeETH) == SubscriptionType.TradeETH)
                //{ 
                //    AddSub(retlist, "TradeETH", symbol);  //temp test
                //}
            }
            else
            {
                // for options
                //if ((type & SubscriptionType.Greek) == SubscriptionType.Greek)
                //{
                //    AddSub(retlist, "Greeks", symbol);
                //}
                //if ((type & SubscriptionType.TheoPrice) == SubscriptionType.TheoPrice)
                //{
                //    AddSub(retlist, "TheoPrice", symbol);  //temp test
                //}
            }
            return retlist;
        }

        private JArray AddSub(JArray list, string type, string sym)
        {
            JObject jObj = new JObject();
            jObj.Add("type", type);
            jObj.Add("symbol", sym);
            list.Add(jObj);
            return list;
        }
    }


    public delegate void DxMessageReceivedHandler(object sender, List<DxMessageReceivedEventArgs> e, int channel);


    public enum DxMessageType
    {
        Unhandled,
        Heartbeat,
        Quote,
        Trade,
        Summary,
        Greeks,
        Profile,
        TheoPrice,
        Candle
    }

    public class DxMessageReceivedEventArgs : EventArgs
    {
        public DxMessageType Type { get; set; }
        public string Symbol { get; set; }
        public string Message { get; set; }
        public string DebugText { get; set; }
        public JToken Debug { get; set; }
    }

    public class DxTradeMessageEventArgs : DxMessageReceivedEventArgs
    {
        public double Price { get; set; }
        public double Change { get; set; }
        public double Volume { get; set; }
        public DateTime Time { get; set; }
    }

    public class DxQuoteMessageEventArgs : DxMessageReceivedEventArgs
    {
        public double AskPrice { get; set; }
        public double BidPrice { get; set; }
    }

    public class DxSummaryMessageEventArgs : DxMessageReceivedEventArgs
    {
        //public double OpenPrice { get; set; }
        //public double LowPrice { get; set; }
        //public double HighPrice { get; set; }
        //public double ClosePrice { get; set; }
        public double PrevDayClosePrice { get; set; }
        public int OpenInterest { get; set; }
    }

    public class DxGreeksMessageEventArgs : DxMessageReceivedEventArgs
    {
        public double Price { get; set; }
        public double IV { get; set; }
        public double Delta { get; set; }
    }

    public class DxProfileMessageEventArgs : DxMessageReceivedEventArgs
    {
        public string Description { get; set; }
    }
    public class DxTheoPriceMessageEventArgs : DxMessageReceivedEventArgs
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public double UnderlyingPrice { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Dividend { get; set; }
        public double Interest { get; set; }
    }

    public class DxCandleMessageEventArgs : DxMessageReceivedEventArgs
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public double IV { get; set; }
    }

    public class DxCandles : List<DxCandleMessageEventArgs>
    {

    }



}
