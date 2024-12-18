﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using DxLink;
using System.Windows.Markup;

namespace OptionView
{
    public class TransactionGroup
    {
        public string Symbol { get; set; }
        public string ShortSymbol { get; set; }  // deal with options
        public int GroupID { get; set; }
        public decimal Cost { get; set; }
        public decimal Fees { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Strategy { get; set; }
        public string ExitStrategy { get; set; }
        public DateTime ActionDate { get; set; }
        public string ActionText { get; set; }
        public string Comments { get; set; }
        public decimal CapitalRequired { get; set; }
        public decimal OriginalCapitalRequired { get; set; }
        public decimal Return { get; set; }
        public decimal Net
        {
            get { return Cost - Fees; }
        }
        public decimal AnnualReturn { get; set; }
        public bool EarningsTrade { get; set; }
        public bool NeutralStrategy { get; set; }
        public bool DefinedRisk { get; set; }
        public decimal Risk { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Year { get; set; }
        public DateTime EarliestExpiration { get; set; }
        public string TransactionText { get; set; }
        public string Account { get; set; }
        public string AccountName { get; set; }
        public decimal? CurrentValue { get; set; } = null;  // null when no longer in portfolio
        public decimal PreviousCloseValue { get; set; }
        public decimal ChangeFromPreviousClose { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal UnderlyingPriceChange { get; set; }
        public double ImpliedVolatility { get; set; }
        public double ImpliedVolatilityRank { get; set; }
        public DateTime EarningsDate { get; set; }
        public double DividendYield { get; set; }
        public decimal AnalysisXValue { get; set; }
        public decimal AnalysisYValue { get; set; }
        public bool OrderActive { get; set; }
        public Greek GreekData { get; set; }
        public string StreamingSymbol { get; set; }

        public Positions Holdings { get; set; }
        public Transactions Transactions { get; set; }
        private int shiftAmount = 0;


        public TransactionGroup()
        {
            Symbol = "undef";
            OrderActive = true;
            Initialize();
        }
        public TransactionGroup(string sym)
        {
            Symbol = sym;
            Initialize();
        }

        private void Initialize()
        {
            GroupID = 0;
            Cost = 0;
            X = 13 + shiftAmount;
            Y = 13 + shiftAmount;
            Comments = "";
            Holdings = new Positions();
            Transactions = new Transactions();
            GreekData = new Greek();

            shiftAmount += 15;
        }



        public void Update()
        {
            if (this.GroupID > 0)
            {
                // update group
                string sql = "UPDATE transgroup SET Strategy = @st, ExitStrategy = @ex, ActionDate = @ad, ActionText = @at, Comments = @cm, CapitalRequired = @ca, OriginalCapRequired = @oc, EarningsTrade = @ea, NeutralStrategy = @ns, DefinedRisk = @dr, Risk = @rs WHERE ID=@rw";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("st", this.Strategy);
                cmdUpd.Parameters.AddWithValue("ex", this.ExitStrategy);
                cmdUpd.Parameters.AddWithValue("ad", this.ActionDate);
                cmdUpd.Parameters.AddWithValue("at", this.ActionText);
                cmdUpd.Parameters.AddWithValue("cm", this.Comments);
                cmdUpd.Parameters.AddWithValue("ca", this.CapitalRequired);
                cmdUpd.Parameters.AddWithValue("oc", this.OriginalCapitalRequired);
                cmdUpd.Parameters.AddWithValue("ea", this.EarningsTrade);
                cmdUpd.Parameters.AddWithValue("ns", this.NeutralStrategy);
                cmdUpd.Parameters.AddWithValue("dr", this.DefinedRisk);
                cmdUpd.Parameters.AddWithValue("rs", this.Risk);
                cmdUpd.Parameters.AddWithValue("rw", this.GroupID);
                cmdUpd.ExecuteNonQuery();
            }
        }


        public static int Combine(int destinationGroup, int combineGroup, decimal newOrigCap)
        {
            Debug.WriteLine("Combine {0} into {1}", combineGroup, destinationGroup);

            if ((destinationGroup == 0) || (combineGroup == 0))
            {
                MessageBox.Show("Missing ID(s). Combine failed.", "Combine Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return 0;  // nothing happened
            }


            // move any transaction from group to new group
            string sql = "UPDATE Transactions SET TransGroupID = @new WHERE TransGroupID=@grp";
            SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("new", destinationGroup);
            cmdUpd.Parameters.AddWithValue("grp", combineGroup);
            int retval = cmdUpd.ExecuteNonQuery();

            // no reason to keep the old group around - may change to save but with some comments appended
            sql = "DELETE FROM TransGroup Where ID = @id";
            cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("id", combineGroup);
            retval = cmdUpd.ExecuteNonQuery();

            // move any transaction from group to new group
            sql = "UPDATE TransGroup SET OriginalCapRequired = @cr WHERE ID=@grp";
            cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("cr", newOrigCap);
            cmdUpd.Parameters.AddWithValue("grp", destinationGroup);
            retval = cmdUpd.ExecuteNonQuery();

            return 1; // combine completed
        }

        public void Close()
        {
            string sql = "UPDATE TransGroup SET Open = @op WHERE ID=@row";
            SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
            cmdUpd.Parameters.AddWithValue("op", 0);
            cmdUpd.Parameters.AddWithValue("row", this.GroupID);
            cmdUpd.ExecuteNonQuery();
        }

        public string GetPerLotCost()
        {
            decimal defaultAmount = 0;
            bool onlyStock = true;

            foreach (KeyValuePair<string, Position> item in this.Holdings)
            {
                Position p = item.Value;
                if (defaultAmount == 0)
                {
                    defaultAmount = Math.Abs(p.Quantity);
                }
                else
                {
                    if (Math.Abs(p.Quantity) != defaultAmount) return " *";
                }
                if (p.Type != "Stock") onlyStock = false;
            }

            if (defaultAmount == 0) return " oopsie";
            if ((defaultAmount == 1) || onlyStock) return "";

            return " - " + String.Format("{0:C0}", this.Cost / defaultAmount) + "/lot";
        }

        public bool HasInTheMoneyPositions()
        {
            bool retval = false;
            if (this.UnderlyingPrice == 0) return retval;

            foreach (KeyValuePair<string, Position> item in this.Holdings)
            {
                Position p = item.Value;

                if (((p.Type == "Put") && (this.UnderlyingPrice < p.Strike)) || ((p.Type == "Call") && (this.UnderlyingPrice > p.Strike)))
                {
                    retval = true;
                    break;
                }
            }

            return retval;
        }

        public string GetHistoryText()
        {
            SortedList<DateTime, Positions> data = new SortedList<DateTime, Positions>();

            // step thru open transactions
            string sql = "SELECT datetime(time) AS Time, symbol, type, datetime(expiredate) AS ExpireDate, strike, quantity, amount, TransSubType, UnderlyingPrice FROM transactions";
            sql += " WHERE (transgroupid = @gr) ORDER BY time";
            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            cmd.Parameters.AddWithValue("gr", this.GroupID);

            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DateTime time = ((reader["Time"] != DBNull.Value) ? Convert.ToDateTime(reader["Time"].ToString()) : DateTime.MinValue);
                time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
                if (!data.ContainsKey(time)) data.Add(time, new Positions());

                Positions tr = new Positions();
                string symbol = reader["Symbol"].ToString();
                string type = reader["Type"].ToString();
                DateTime expDate = DateTime.MinValue;
                if (reader["ExpireDate"] != DBNull.Value) expDate = Convert.ToDateTime(reader["ExpireDate"].ToString());
                decimal strike = 0;
                if (reader["Strike"] != DBNull.Value) strike = Convert.ToDecimal(reader["Strike"]);
                decimal quantity = Convert.ToDecimal(reader["Quantity"]);
                decimal amount = Convert.ToDecimal(reader["Amount"]);
                string transType = reader["TransSubType"].ToString();
                decimal underlyingPrice = 0;
                if (reader["UnderlyingPrice"] != DBNull.Value) underlyingPrice = Convert.ToDecimal(reader["UnderlyingPrice"]);

                data[time].Add(symbol, type, expDate, strike, quantity, amount, time, 0, transType, 0, underlyingPrice);
            }

            string returnValue = "";
            foreach (KeyValuePair<DateTime, Positions> key in data)
            {
                Positions positions = key.Value;
                decimal total = SumAmounts(positions);

                string desc = GetDescription(positions);
                if (desc == "Expired")
                {
                    returnValue += String.Format("{0}   {1} {2}\n", key.Key.ToString("d-MMM-yy H:mm:ss"), desc, GetUnderlying(positions));
                }
                else if (desc == "Dividend")
                {
                    returnValue += String.Format("{0}   {1} paid {2:C0}\n", key.Key.ToString("d-MMM-yy H:mm:ss"), desc, total);
                    continue;
                }
                else
                {
                    returnValue += String.Format("{0}   {1} for {2:C0} {3}\n", key.Key.ToString("d-MMM-yy H:mm:ss"), desc, total, GetUnderlying(positions));
                }

                foreach (KeyValuePair<string, Position> pkey in positions)
                {
                    Position pos = pkey.Value;
                    string code = "";
                    switch (pos.TransType)
                    {
                        case "Buy to Open":
                            code = "BTO";
                            break;
                        case "Buy to Close":
                            code = "BTC";
                            break;
                        case "Sell to Open":
                            code = "STO";
                            break;
                        case "Sell to Close":
                            code = "STC";
                            break;
                        case "Expiration":
                            code = "EXP";
                            break;
                        case "Assignment":
                            code = "ASG";
                            break;

                    }
                    if (pos.Type == "Stock")
                    {
                        returnValue += String.Format("   {0,2} {1} {2}\n", pos.Quantity, pos.Type, code);
                    }
                    else
                    {
                        returnValue += String.Format("   {0,2} {1} {2} {3:MMMd} {4}\n", pos.Quantity, pos.Type, pos.Strike, pos.ExpDate, code);
                    }
                }
            }

            return returnValue;
        }

        private decimal SumAmounts(Positions positions)
        {
            decimal returnValue = 0;
            foreach (KeyValuePair<string, Position> key in positions)
            {
                Position pos = key.Value;
                returnValue += pos.Amount;
            }

            return returnValue;
        }

        private string GetDescription(Positions positions)
        {
            string returnValue = "Adjusted";
            int openCount = 0;
            int closeCount = 0;
            bool diffExpire = false;
            bool expiration = false;
            bool assignment = false;
            bool dividend = false;
            DateTime expDate = DateTime.MinValue;

            foreach (KeyValuePair<string, Position> key in positions)
            {
                Position pos = key.Value;
                if (expDate == DateTime.MinValue) expDate = pos.ExpDate;

                if (expDate != pos.ExpDate) diffExpire = true;
                if (pos.TransType.IndexOf("Open") >= 0) openCount++;
                if (pos.TransType.IndexOf("Close") >= 0) closeCount++;
                if (pos.TransType.IndexOf("Expiration") >= 0) expiration = true;
                if (pos.TransType.IndexOf("Assignment") >= 0) assignment = true;
                if (pos.TransType.IndexOf("Dividend") >= 0) dividend = true;
            }

            if (expiration)
                returnValue = "Expired";
            else if (assignment)
                returnValue = "Assigned";
            else if (dividend)
                returnValue = "Dividend";
            else if (closeCount == 0)
                returnValue = "Opened";
            else if (openCount == 0)
                returnValue = "Closed";
            else if (diffExpire)
                returnValue = "Rolled Out";
            else if (!diffExpire)
                returnValue = "Rolled";
            return returnValue;
        }
        private string GetUnderlying(Positions positions)
        {
            decimal underlying = 0;

            foreach (KeyValuePair<string, Position> key in positions)
            {
                Position pos = key.Value;
                underlying = pos.UnderlyingPrice;
                break;
            }
            return (underlying > 0) ? (" @ " + underlying.ToString("C2")) : "";
        }

        // for the purposes of establishing profitability of position
        public Positions GetInitialPositions()
        {
            try
            {
                Positions retlist = new Positions();
                Positions allpos = new Positions();

                // establish connection
                App.OpenConnection();

                // step thru open holdings
                string sql = "SELECT symbol, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(amount) as amount, datetime(Time) AS TransTime, [Open-Close] FROM transactions";
                sql += " WHERE (transgroupid = @gr) and [Open-Close] = 'Open' GROUP BY symbol, type, expiredate, strike, [Open-Close]";
                SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
                cmd.Parameters.AddWithValue("gr", this.GroupID);

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    //decimal strike = reader["strike"].ToString();
                    decimal strike = 0m;
                    if (reader["Strike"] != DBNull.Value) strike = Convert.ToDecimal(reader["Strike"]);
                    decimal amount = 0.0m;
                    if (reader["Amount"] != DBNull.Value) amount = Convert.ToDecimal(reader["Amount"]);
                    DateTime expDate = DateTime.MinValue;
                    if (reader["ExpireDate"] != DBNull.Value) expDate = Convert.ToDateTime(reader["ExpireDate"].ToString());
                    DateTime transTime = DateTime.MinValue;
                    if (reader["TransTime"] != DBNull.Value) transTime = Convert.ToDateTime(reader["TransTime"].ToString());
                    transTime = DateTime.SpecifyKind(transTime, DateTimeKind.Utc);


                    allpos.Add(reader["symbol"].ToString(), reader["type"].ToString(), expDate, strike, 0.0m, amount, transTime, 0, "", 0, 0);
                }

                // find first day of group
                DateTime startDay = DateTime.MaxValue;
                foreach (KeyValuePair<string, Position> item in allpos)
                {
                    if (item.Value.TransTime < startDay) startDay = item.Value.TransTime;
                }
                startDay = new DateTime(startDay.Year, startDay.Month, startDay.Day);

                // remove all transactions that didn't occur on the first day
                foreach (KeyValuePair<string, Position> item in allpos)
                {
                    DateTime day = new DateTime(item.Value.TransTime.Year, item.Value.TransTime.Month, item.Value.TransTime.Day);
                    if (day == startDay)
                        retlist.Add(item.Value);
                }

                return retlist;
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetOpeningPositions: " + ex.Message);
            }

            return null;
        }

        public decimal PercentOfTarget()
        {
            decimal retval = 0;

            Positions positions = GetInitialPositions();

            decimal firstDayAmount = 0;
            foreach (KeyValuePair<string, Position> item in positions)
            {
                firstDayAmount += item.Value.Amount;
            }

            Debug.WriteLine("Open value of group {0}: {1}", this.Symbol, firstDayAmount.ToString("C0"));

            /// this is the accurate value... need to find target and convert to a +/- percentage
            decimal target = ParseTargetValue();
            retval = (this.Cost + (this.CurrentValue ?? 0)) / Math.Abs(target * firstDayAmount);

            Debug.WriteLine("PercentOfTarget: {0}  Current Profit: {1}   Percent: {2}", target.ToString(), (this.Cost + (this.CurrentValue ?? 0)).ToString("C0"), retval.ToString());

            return retval;
        }

        public decimal TargetClosePrice()
        {
            decimal retval = 0;

            Positions positions = this.GetInitialPositions();

            decimal firstDayAmount = 0;
            foreach (KeyValuePair<string, Position> item in positions)
            {
                firstDayAmount += item.Value.Amount;
            }

            Debug.WriteLine("Open value of group {0}: {1}", this.Symbol, firstDayAmount.ToString("C0"));

            /// this is the accurate value... need to find target and convert to a +/- percentage
            decimal target = ParseTargetValue();

            if (this.Holdings.ElementAt(0).Value.Quantity != 0)
            {
                retval = (Math.Abs(target * firstDayAmount) - this.Cost) / Math.Abs(this.Holdings.ElementAt(0).Value.Quantity) / 100;
                Debug.WriteLine("Sell Price: {0}", retval);
            }

            return retval;
        }


        private decimal ParseTargetValue()
        {
            decimal retval = 0;

            Match match = Regex.Match(this.ExitStrategy, "\\d*");
            if (match.Success)
            {
                retval = Convert.ToDecimal(match.Value) / 100;
            }
            return retval;
        }


        public List<decimal> GetStrikes(string typ)
        {
            List<decimal> retval = new List<decimal>();

            foreach (KeyValuePair<string, Position> item in this.Holdings)
            {
                Position p = item.Value;
                if (p.Type == typ) retval.Add(p.Strike);
            }
            return retval.Count > 0 ? retval : null;
        }


        public SortedList<DateTime, List<decimal>> GetOptionStrikeHistory(string typ)
        {
            SortedList<DateTime, List<decimal>> retval = new SortedList<DateTime, List<decimal>>();

            // step thru open transactions
            string sql = "SELECT datetime(time) AS Time, type, datetime(expiredate) AS ExpireDate, strike, quantity, TransSubType FROM transactions";
            sql += " WHERE (transgroupid = @gr) ORDER BY time";
            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            cmd.Parameters.AddWithValue("gr", this.GroupID);

            SQLiteDataReader reader = cmd.ExecuteReader();
            DateTime prevTime = DateTime.MinValue;
            while (reader.Read())
            {
                DateTime time = ((reader["Time"] != DBNull.Value) ? Convert.ToDateTime(reader["Time"].ToString()) : DateTime.MinValue);
                time = DateTime.SpecifyKind(time, DateTimeKind.Utc);

                string type = reader["Type"].ToString();
                decimal strike = 0;
                if (reader["Strike"] != DBNull.Value) strike = Convert.ToDecimal(reader["Strike"]);
                decimal quantity = Convert.ToDecimal(reader["Quantity"]);
                string transType = reader["TransSubType"].ToString();


                if (typ == type)
                {
                    if (!retval.ContainsKey(time))
                    {
                        retval.Add(time, new List<decimal>());
                        if (prevTime > DateTime.MinValue)
                        {
                            // seed the next date with the previous date's positions
                            retval[time] = retval[time].Concat(retval[prevTime]).ToList();
                        }
                        prevTime = time;
                    }
                    if (transType.Contains("Open")) retval[time].Add(strike);
                    if (transType.Contains("Close")) retval[time].Remove(strike);
                    if (transType.Contains("Assignment")) retval[time].Remove(strike);
                }
            }
            return retval;
        }


        public SortedList<DateTime, Positions> GetOptionPositionHistory()
        {
            SortedList<DateTime, Positions> retval = new SortedList<DateTime, Positions>();

            // step thru open transactions
            string sql = "SELECT symbol, TransSubType, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, twSymbol, sum(quantity) AS total, sum(amount) as amount, ";
            sql += "datetime(Time) AS Time FROM transactions WHERE (transgroupid = @gr) GROUP BY symbol, TransSubType, type, expiredate, strike ORDER BY Time";

            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            cmd.Parameters.AddWithValue("gr", this.GroupID);
            SQLiteDataReader reader = cmd.ExecuteReader();
            DateTime prevTime = DateTime.MinValue;
            while (reader.Read())
            {
                DateTime time = ((reader["Time"] != DBNull.Value) ? Convert.ToDateTime(reader["Time"].ToString()) : DateTime.MinValue);
                time = DateTime.SpecifyKind(time, DateTimeKind.Utc);

                string transType = reader["TransSubType"].ToString();
                string type = reader["Type"].ToString();
                decimal strike = 0m;
                if (reader["Strike"] != DBNull.Value) strike = Convert.ToDecimal(reader["Strike"]);
                decimal quantity = 0.0m;
                if (reader["Total"] != DBNull.Value) quantity = Convert.ToDecimal(reader["Total"]);
                decimal amount = 0.0m;
                if (reader["Amount"] != DBNull.Value) amount = Convert.ToDecimal(reader["Amount"]);
                DateTime expDate = DateTime.MinValue;
                if (reader["ExpireDate"] != DBNull.Value) expDate = Convert.ToDateTime(reader["ExpireDate"].ToString());

                if (!retval.ContainsKey(time))
                {
                    if ((time > prevTime) && (prevTime > DateTime.MinValue))
                    {
                        // clean up previous time of empty positions
                        retval[prevTime].PurgeEmptyPositions();
                    }

                    retval.Add(time, new Positions());
                    if (prevTime > DateTime.MinValue)
                    {
                        // seed the next date with the previous date's positions
                        retval[time].Concat(retval[prevTime]);
                    }
                    prevTime = time;
                }

                string symbol = reader["symbol"].ToString();
                string twSymbol = reader["twSymbol"].ToString();
                string streamingSymbol = "undef";
                if (twSymbol != "")
                    streamingSymbol = TastyWorks.GetStreamingSymbol(twSymbol);
                else if (type == "Stock")
                    // legacy condition
                    streamingSymbol = symbol;
                else if ((type == "Put") || (type == "Call"))
                    streamingSymbol = string.Format(".{0}{1:yyMMdd}{2}{3}", symbol, expDate, type.Substring(0, 1), strike);

                retval[time].Add(symbol, type, expDate, strike, quantity, amount, time, 0, "", 0, 0, streamingSymbol);
            }
            retval.ElementAt(retval.Count-1).Value.PurgeEmptyPositions();

            return retval;
        }

        public decimal GetCostAtDate(DateTime date)
        {
            decimal retval = 0;

            // step thru open transactions
            string sql = "SELECT symbol, TransSubType, transgroupid, type, datetime(expiredate) AS ExpireDate, strike, sum(quantity) AS total, sum(amount) as amount, ";
            sql += "datetime(Time) AS Time FROM transactions WHERE (transgroupid = @gr) AND date(Time) <= @dt GROUP BY symbol, TransSubType, type, expiredate, strike ORDER BY Time";

            SQLiteCommand cmd = new SQLiteCommand(sql, App.ConnStr);
            cmd.Parameters.AddWithValue("gr", this.GroupID);
            cmd.Parameters.AddWithValue("dt", date);
            SQLiteDataReader reader = cmd.ExecuteReader();
            DateTime prevTime = DateTime.MinValue;
            while (reader.Read())
            {
                if (reader["Amount"] != DBNull.Value) retval += Convert.ToDecimal(reader["Amount"]);
            }

            return retval;
        }


        private static DateTime GetNextHistoricTime(SortedList<DateTime, Positions> lst, int ndx)
        {
            DateTime retval = DateTime.MinValue;

            if ((ndx + 1) >= lst.Count)
            {
                // at the end of the list
                retval = DateTime.MaxValue;
            }
            else
            {
                retval = lst.ElementAt(ndx + 1).Key.Date;
            }
            return retval;
        }


        public GroupHistory GetHistoryValues()
        {
            GroupHistory retval = new GroupHistory();
            retval.Values = new Dictionary<DateTime, GroupHistoryValue>();

            // retrieve all positions over time grouped by dates of transactions
            SortedList<DateTime, Positions> historicalPositions = this.GetOptionPositionHistory();

            List<string> symbols = new List<string>();
            foreach (KeyValuePair<DateTime, Positions> p in historicalPositions)
            {
                Positions positions = p.Value;
                List<string> allSymbols = allSymbols = positions.Select(x => x.Value.StreamingSymbol).ToList();
                foreach (string s in allSymbols)
                {
                    if (!symbols.Any(x => x.Equals(s))) symbols.Add(s);  // add previously unseen symbols
                }
            }
            // add underlying if not already included
            if (this.StreamingSymbol == null) this.StreamingSymbol = TastyWorks.GetStreamingSymbol(this.Symbol);  // if grp has already been closed, the streamsymbol is null
            if (!symbols.Any(s => s.Equals(this.StreamingSymbol))) symbols.Add(this.StreamingSymbol);

            // get data
            Subscriptions lst = App.DxHandler.GetTimeSeries(symbols, DxHandler.TimeSeriesType.Day, this.StartTime.AddDays(-1).Date).Result;



            if ((lst != null) && (lst.Count > 0))
            {
                int currentPositionIndex = 0;

                DateTime today = DateTime.Today;
                for (DateTime date = this.StartTime.Date; date <= today; date = date.AddDays(1))
                {
                    // step thru each day
                    DateTime nextDate = GetNextHistoricTime(historicalPositions, currentPositionIndex);
                    if (date >= nextDate) currentPositionIndex++;

                    KeyValuePair<DateTime, Positions> node = historicalPositions.ElementAt(currentPositionIndex);
                    DateTime currentTime = node.Key;
                    Positions currentPositions = node.Value;
                    //Debug.WriteLine($"Today: {date}    index: {currentPositionIndex}    current: {currentTime}    next: {nextDate}");


                    // determine aggregate value for current day
                    decimal? currentValue = null;
                    foreach (KeyValuePair<string, Position> p in currentPositions)
                    {
                        Position pos = p.Value;

                        if (!lst[pos.StreamingSymbol].Candles.ContainsKey(date))
                        {
                            currentValue = null;
                            break;
                        }

                        decimal multipler = 1;
                        if (pos.Type != "Stock")
                        {
                            multipler = 100;
                            var x = this.Holdings.Where(y => y.Value.Type == pos.Type);
                            if (x.Count() > 0) multipler = this.Holdings.Where(z => z.Value.Type == pos.Type).Select(z => z.Value.Multiplier).First();
                        }

                        //Debug.WriteLine($"     {pos.StreamingSymbol}  {lst[pos.StreamingSymbol][date].Price}    mult: {multipler}");
                        if (currentValue == null) currentValue = 0;
                        currentValue += pos.Quantity * lst[pos.StreamingSymbol].Candles[date].Price * multipler;
                    }

                    // if value is non-zero, add to return list along with underlying and IV
                    if (currentValue != null)
                    {
                        decimal accumlativeCost = this.GetCostAtDate(date);
                        //Debug.WriteLine($"  date: {date}    value: {currentValue}   cost: {grp.Cost}    new cost: {accumlativeCost}");

                        GroupHistoryValue val = new GroupHistoryValue();
                        val.Time = date;
                        val.Value = (currentValue ?? 0) + accumlativeCost;
                        if ((this.StreamingSymbol != null) && (lst.ContainsKey(this.StreamingSymbol)))
                        {
                            Candles candles = lst[this.StreamingSymbol].Candles;
                            val.Underlying = candles[date].Price;
                            val.IV = candles[date].IV * 100;
                            //Debug.WriteLine($"  underlying: {val.Underlying}   iv: {val.IV}");
                        }

                        retval.Values.Add(date, val);
                    }
                }

            }

            // organize data for option bands of graph
            retval.Calls = this.GetOptionStrikeHistory("Call");
            retval.Puts = this.GetOptionStrikeHistory("Put");

            return retval;
        }

    }

    public class GroupHistoryValue
    {
        public DateTime Time { get; set; }
        public decimal Value { get; set; }
        public decimal Underlying { get; set; }
        public decimal IV { get; set; }
    }

    public class GroupHistory
    {
        public Dictionary<DateTime, GroupHistoryValue> Values { get; set; }
        public SortedList<DateTime, List<decimal>> Puts { get; set; }
        public SortedList<DateTime, List<decimal>> Calls { get; set; }
    }

}
