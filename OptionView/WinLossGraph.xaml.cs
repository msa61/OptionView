using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for WinLossGraph.xaml
    /// </summary>
    public partial class WinLossGraph : UserControl
    {
        public double CtlWidth { get; set; } = 80;
        public double CtlHeight { get; set; } = 10;
        private double scale;
        private decimal minVal;

        public WinLossGraph(List<decimal> values)
        {
            InitializeComponent();
            InitializeScale(values);


            Canvas c = new Canvas()
            {
                Height = CtlHeight,
                Width = CtlWidth,
                Margin = new Thickness(0, 0, 10, 0)
            };
            ((Grid)(this.Content)).Children.Add(c);

            if (values.Count > 0)
            {
                double curX = 0;
                double xInc = CtlWidth / values.Count;

                foreach (decimal v in values)
                {
                    Rectangle r = new Rectangle()
                    {
                        Height = Math.Abs(Scale(v)),
                        Width = xInc * 0.9,
                        Fill = (v < 0) ? Brushes.Red : Brushes.Green,
                        ToolTip = v.ToString("C2")
                    };
                    Canvas.SetLeft(r, curX);
                    Canvas.SetTop(r, CtlHeight/2 + ((v < 0) ? 0 : - Scale(v)));
                    c.Children.Add(r);

                    curX += xInc;
                }
            }
        }

        private bool InitializeScale(List<decimal> vals)
        {
            minVal = vals.Min<decimal>();
            decimal maxVal = vals.Max<decimal>();

            if (minVal == maxVal)
            {
                scale = 0;
                return false;
            }
            if (Math.Abs(minVal) > maxVal) maxVal = Math.Abs(minVal);
            scale = CtlHeight / Convert.ToDouble(maxVal * 2);
            return true;
        }

        private double Scale(decimal y)
        {
            return (Convert.ToDouble(y) * scale);
        }


    }
}
