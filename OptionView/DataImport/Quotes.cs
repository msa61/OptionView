using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace OptionView
{
    class Quotes
    {
        public static Decimal Get(string symbol)
        {
            Decimal quote = 0;

            try
            {
                string url = "https://finance.yahoo.com/quote/" + symbol;
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load(url);

                var node = doc.GetElementbyId("quote-header-info");
                string value = node.ChildNodes[2].ChildNodes[0].ChildNodes[0].ChildNodes[0].InnerText;

                quote = Convert.ToDecimal(value);
            }
            catch (Exception e)
            {

            }
            return quote;
        }

    }

}
