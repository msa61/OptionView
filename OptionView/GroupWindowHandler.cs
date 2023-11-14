using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptionView
{
    public class GroupWindowHandler
    {
        public GroupDetailsWindow Window { get; set; } = null;
        public double Left { get; set; } = 0;
        public double Top { get; set; } = 0;
        public string Symbol { get; set; } = string.Empty;
        public GroupGraph GraphContents { set; get; } = null;
        public List<Detail> Prices { get; set; } = null;
        public List<Detail> GroupDetails { get; set; } = null;


        public void Open()
        {
            if (Window == null)
            {
                Window = new GroupDetailsWindow();
                Window.Left = Left;
                Window.Top = Top;
            }
            Window.Show();

            Window.Update(Symbol, GraphContents, Prices, GroupDetails);
        }
        public void Update(string symbol = "", GroupGraph gg = null, List<Detail> prices = null, List<Detail> details = null)
        {
            if (symbol != "") Symbol = symbol;
            if (gg != null) GraphContents = gg;
            if (prices != null) Prices = prices;
            if (details != null) GroupDetails = details;
            if ((Window != null) && (Window.IsVisible)) Window.Update(Symbol, GraphContents, Prices, GroupDetails);
        }
        public void Close()
        {
            if (Window != null) Window.Close();
            Window = null;
        }
        public void Clear()
        {
            GraphContents = null;
            Prices = null;
            GroupDetails= null;
            if (Window != null) Window.Clear();
        }
        public bool IsOpen()
        {
            return Window != null && Window.IsVisible;
        }
    }
}
