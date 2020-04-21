using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Diagnostics;


namespace OptionView
{

    class SymbolDecoder
    {
        public string Type { get; set; }
        public DateTime Expiration { get; set; }
        public decimal Strike { get; set; }

        // can remove symbolType eventually
        public SymbolDecoder(string symbol, string symbolType)
        {
            try
            {
                string regexPattern = @".+\s?.*\s{2}(\d{2})(\d{2})(\d{2})(.)(\d+)";
                string[] flds = Regex.Split(symbol, regexPattern);
                if (flds.Count() == 7)
                {
                    this.Expiration = new DateTime(Convert.ToInt32(flds[1]) + 2000, Convert.ToInt32(flds[2]), Convert.ToInt32(flds[3]));
                    if (flds[4] == "C")
                    {
                        this.Type = "Call";
                    }
                    else if (flds[4] == "P")
                    {
                        this.Type = "Put";
                    }
                    this.Strike = Convert.ToDecimal(flds[5]);
                    // if equity
                    if (flds[5].Length == 8) this.Strike /= 1000;
                }
                else if (flds.Count() == 1)
                {
                    this.Type = "Stock";
                }

                //// delete from here down after awhile
                string testType = "";
                DateTime testExpiration = DateTime.MinValue;
                decimal testStrike = 0;

                if (symbolType == "Equity Option")
                {
                    //  ROKU  200417P00065000
                    string pattern = @"(.+)\s+(\d{2})(\d{2})(\d{2})(.)(\d{5})(\d+)";
                    string[] subs = Regex.Split(symbol, pattern);
                    if (subs.Count() == 9)
                    {
                        testExpiration = new DateTime(Convert.ToInt32(subs[2]) + 2000, Convert.ToInt32(subs[3]), Convert.ToInt32(subs[4]));
                        if (subs[5] == "C")
                        {
                            testType = "Call";
                        }
                        else if (subs[5] == "P")
                        {
                            testType = "Put";
                        }
                        testStrike = Convert.ToDecimal(subs[6]) + Convert.ToDecimal(subs[7]) / 1000;
                    }
                }
                else if (symbolType == "Future Option")
                {
                    //  ./CLM0 LOM0  200514P15
                    string pattern = @"(.+)\s+(.+)\s+(\d{2})(\d{2})(\d{2})(.)(\d{1,5})";
                    string[] subs = Regex.Split(symbol, pattern);
                    if (subs.Count() == 9)
                    {
                        testExpiration = new DateTime(Convert.ToInt32(subs[3]) + 2000, Convert.ToInt32(subs[4]), Convert.ToInt32(subs[5]));
                        if (subs[6] == "C")
                        {
                            testType = "Call";
                        }
                        else if (subs[6] == "P")
                        {
                            testType = "Put";
                        }
                        testStrike = Convert.ToDecimal(subs[7]);  //todo confirm how they handle non integers
                    }
                }
                else
                {
                    testType = "Stock";
                }


                if ((this.Type != testType) || (this.Expiration != testExpiration) || (this.Strike != testStrike))
                {
                    //error
                    string response = this.Type + " | " + this.Strike.ToString() + " | " + this.Expiration.ToString() + "\n";
                    response += testType + " | " + testStrike.ToString() + " | " + testExpiration.ToString();
                    MessageBox.Show(response, "Decoding test");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR SymbolDecoder (" + symbol + ") : " + ex.Message);
            }
        }
    }
}
