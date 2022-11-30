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
        public Decimal Price = 0;
        public string Text = "";
        public string ChangeText = "";

        public static Decimal Get(string symbol)
        {
            Quotes value = GetAll(symbol);
            Decimal retval = value.Price;
            return retval;
        }

        public static Quotes GetAll(string symbol)
        {
            Quotes retval = new Quotes();

            try
            {
                App.UpdateLoadStatusMessage("Retrieving quote");

                string url = "https://finance.yahoo.com/quote/" + symbol;
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load(url);

                var node = doc.GetElementbyId("quote-header-info");
                HtmlNode valueText = node.ChildNodes[2].ChildNodes[0].ChildNodes[0];

                retval.Text = valueText.ChildNodes[0].InnerText;
                retval.Price = Convert.ToDecimal(retval.Text);
                retval.ChangeText = valueText.ChildNodes[1].InnerText;

                retval.Text += " " + retval.ChangeText;
            }
            catch (Exception e)
            {

            }
            return retval;
        }

    }

}
