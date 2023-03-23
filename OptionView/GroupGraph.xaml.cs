using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
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
using static OptionView.GroupGraph;
using static System.Net.Mime.MediaTypeNames;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for GroupGraph.xaml
    /// </summary>
    public partial class GroupGraph : UserControl
    {
        public double CtlWidth { get; set; } = 300;
        public double CtlHeight { get; set; } = 100;
        private double scaleLeft;
        private double scaleRight;
        private double scaleTime;
        private decimal minLeft;
        private decimal maxLeft;
        private decimal minRight;
        private decimal maxRight;
        private DateTime minTime;
        private DateTime maxTime;
        private Canvas c;
        private double margin = 30;

        public enum ScaleType
        {
            Left,
            Right,
            Bottom
        }




        public GroupGraph(List<GroupHistoryValues> values)
        {
            InitializeComponent();
            if (InitializeScale(values) == false) return;

            CreateCanvas();
            DrawAxis();

            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Underlying).ToList(), ScaleType.Right, Brushes.DarkGreen);
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Value).ToList(), ScaleType.Left, Brushes.Blue);
        }

        private void DrawGraphLine(List<DateTime> x, List<decimal> y, ScaleType scaleType, Brush color)
        {
            double lastX = 0;
            double lastY = 0;

            for (int i = 0; i < x.Count; i++)
            {
                if (i > 0)
                {
                    double nextX = ScaleTime(x[i]);
                    double nextY = (scaleType == ScaleType.Right) ? ScaleRight(y[i]) : ScaleLeft(y[i]);
                    Line l = new Line()
                    {
                        X1 = lastX,
                        Y1 = lastY,
                        X2 = nextX,
                        Y2 = nextY,
                        Stroke = color,
                        StrokeThickness = 1
                    };
                    if (scaleType == ScaleType.Right) l.StrokeDashArray = new DoubleCollection() { 2 };
                    c.Children.Add(l);

                    DrawPip(nextX, (scaleType == ScaleType.Right) ? ScaleRight(y[i]) : ScaleLeft(y[i]), color);

                    lastX = nextX;
                    lastY = nextY;
                }
                else
                {
                    // set first point
                    lastX = ScaleTime(x[0]);
                    lastY = (scaleType == ScaleType.Right) ? ScaleRight(y[0]) : ScaleLeft(y[0]);

                    DrawPip(lastX, lastY, color);
                }
            }


        }

        private void DrawPip(double x, double y, Brush color)
        {
            Ellipse e = new Ellipse()
            {
                Width = 4,
                Height = 4,
                Stroke = color,
                StrokeThickness = 4
            };
            Canvas.SetLeft(e, x - 2);
            Canvas.SetTop(e, y - 2);
            c.Children.Add(e);
        }


        private void DrawAxis()
        {
            Line l = new Line()
            {
                X1 = margin,
                Y1 = 0,
                X2 = margin,
                Y2 = CtlHeight,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            l = new Line()
            {
                X1 = CtlWidth - margin,
                Y1 = 0,
                X2 = CtlWidth - margin,
                Y2 = CtlHeight,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            double yVal = CtlHeight;
            if ((minLeft < 0) && (maxLeft > 0)) yVal = ScaleLeft(0);
            else if (maxLeft < 0) yVal = 0;
            l = new Line()
            {
                X1 = margin,
                Y1 = yVal,
                X2 = CtlWidth - margin,
                Y2 = yVal,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            AddLabel(maxLeft, margin, 0, ScaleType.Left);
            AddLabel(minLeft, margin, CtlHeight, ScaleType.Left);
            AddLabel(maxRight, CtlWidth - margin, 0, ScaleType.Right);
            AddLabel(minRight, CtlWidth - margin, CtlHeight, ScaleType.Right);

            AddLabel(minTime.ToString("M/d"), margin, CtlHeight, ScaleType.Bottom);
            AddLabel(maxTime.ToString("M/d"), CtlWidth - margin, CtlHeight, ScaleType.Bottom);

        }

        private void AddLabel(decimal val, double left, double top, ScaleType scaleType)
        {
            string txt;
            if (scaleType == ScaleType.Right)
            {
                if (val > 100)
                    txt = val.ToString("C0");
                else
                    txt = val.ToString("C2");
            }
            else
            {
                txt = val.ToString("C0");
            }

            AddLabel(txt, left, top, scaleType);
        }

        private void AddLabel(string txt, double left, double top, ScaleType scaleType)
        {
            FormattedText ft = new FormattedText(
                txt,
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.Black, 1.0);

            TextBlock tb = new TextBlock()
            {
                Text = txt,
                FontSize = 11,
                Foreground = Brushes.DarkGray,
                TextAlignment = (scaleType == ScaleType.Right) ? TextAlignment.Left : (scaleType == ScaleType.Left) ? TextAlignment.Right : TextAlignment.Center
            };
            if (scaleType == ScaleType.Bottom)
            {
                Canvas.SetTop(tb, top +2);
                Canvas.SetLeft(tb, left - (ft.Width/2));
            }
            else
            {
                Canvas.SetTop(tb, top - (ft.Height / 2) - 2);
                Canvas.SetLeft(tb, (scaleType == ScaleType.Right) ? left + 6 : left - ft.Width - 6);
            }
            c.Children.Add(tb);
        }


        private void CreateCanvas()
        {
            c = new Canvas()
            {
                Height = CtlHeight,
                Width = CtlWidth,
                Margin = new Thickness(10, 20, 10, 20)
            };
            ((Grid)(this.Content)).Children.Add(c);
        }


        private bool InitializeScale(List<GroupHistoryValues> values)
        {
            if (values.Count < 2)
            {
                scaleLeft = 0;
                scaleRight = 0;
                return false;
            }

            minLeft = values.Min(x => x.Value);
            maxLeft = values.Max(x => x.Value);
            minRight = values.Min(x => x.Underlying);
            maxRight = values.Max(x => x.Underlying);
            minTime = values.Min(x => x.Time); 
            maxTime = values.Max(x => x.Time);

            if (minLeft == maxLeft) 
                scaleLeft = 1;
            else
                scaleLeft = CtlHeight / Convert.ToDouble(maxLeft - minLeft);

            if (minRight == maxRight)
                scaleRight = 1;
            else
                scaleRight = CtlHeight / Convert.ToDouble(maxRight - minRight);

            if (minTime == maxTime)
                scaleTime = 1;
            else
            {
                TimeSpan ts = maxTime - minTime;
                scaleTime = (CtlWidth - (2 * margin)) / ts.TotalMinutes;
            }

            return true;
        }

        private double ScaleLeft(decimal y)
        {
            double val = Convert.ToDouble(y - minLeft);
            return (CtlHeight - (val * scaleLeft));
        }
        private double ScaleRight(decimal y)
        {
            double val = Convert.ToDouble(y - minRight);
            return (CtlHeight - (val * scaleRight));
        }
        private double ScaleTime(DateTime dt)
        {
            TimeSpan ts = dt - minTime;
            return (ts.TotalMinutes * scaleTime) + margin;
        }

    }
}
