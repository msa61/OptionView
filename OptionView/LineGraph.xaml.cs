using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for LineGraph.xaml
    /// </summary>
    public partial class LineGraph : UserControl
    {
        public double CtlWidth { get; set; } = 100;
        public double CtlHeight { get; set; } = 10;
        private double scale;
        private decimal minVal;


        public LineGraph(List<decimal> values)
        {
            InitializeComponent();
            InitializeScale(values);


            Canvas c = new Canvas()
            {
                Height = CtlHeight,
                Width = CtlWidth,
                Margin = new Thickness(0, 0, 20, 0)
            };
            ((Grid)(this.Content)).Children.Add(c);

            if (values.Count > 0)
            {
                decimal lastPt = 0;
                double curX = 0;
                double xInc = CtlWidth / (values.Count - 1);

                DrawPip(c, curX, Scale(values[0]), values[0].ToString("C2"));

                foreach (decimal v in values)
                {
                    if (lastPt != 0)
                    {
                        Line l = new Line()
                        {
                            X1 = curX,
                            Y1 = Scale(lastPt),
                            X2 = curX + xInc,
                            Y2 = Scale(v),
                            Stroke = Brushes.White,
                            StrokeThickness = 1
                        };
                        c.Children.Add(l);

                        DrawPip(c, curX + xInc, Scale(v), v.ToString("C2"));

                        curX += xInc;
                        lastPt = v;
                    }
                    else
                    {
                        lastPt = v;
                    }
                }
            }
        }

        private void DrawPip(Canvas c, double x, double y, string toolTip)
        {
            // transparent pip to increase size of tooltip reaction
            Ellipse e = new Ellipse()
            {
                Width = 10,
                Height = 10,
                Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom("#01FFFFFF"),
                StrokeThickness = 10,
                ToolTip = toolTip
            };
            Canvas.SetLeft(e, x - 5);
            Canvas.SetTop(e, y - 5);
            c.Children.Add(e);

            e = new Ellipse()
            {
                Width = 2,
                Height = 2,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
            Canvas.SetLeft(e, x - 1);
            Canvas.SetTop(e, y - 1);
            c.Children.Add(e);
        }

        private bool InitializeScale(List<decimal> vals)
        {
            vals.RemoveAll(i => i == 0);
            if (vals.Count < 2)
            {
                scale = 0;
                return false;
            }
            minVal = vals.Min<decimal>();
            decimal maxVal = vals.Max<decimal>();

            if (minVal == maxVal)
            {
                scale = 0;
                return false;
            }
            scale = CtlHeight / Convert.ToDouble(maxVal - minVal);
            return true;
        }

        private double Scale(decimal y)
        {
            double val = Convert.ToDouble(y - minVal);
            return (CtlHeight - (val * scale));
        }


    }
}
