using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Bottom,
            Percent
        }


        public GroupGraph(List<GroupHistoryValue> values)
        {
            InitializeComponent();
            if (InitializeScale(values) == false) return;

            CreateCanvas();
            DrawAxis();

            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.IV).ToList(), ScaleType.Percent, Brushes.DarkGray);
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Underlying).ToList(), ScaleType.Right, Brushes.DarkGreen);
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Value).ToList(), ScaleType.Left, Brushes.Blue);

            DrawBands(values.Select(x => x.Time).ToList(), values.Select(x => x.Calls).ToList(), Brushes.Red);
            DrawBands(values.Select(x => x.Time).ToList(), values.Select(x => x.Puts).ToList(), Brushes.Red);
        }


        private void DrawBands(List<DateTime> x, List<List<decimal>> strikes, Brush color)
        {
            bool singleLine = true;
            PointCollection points = new PointCollection();
            for (int i = 0; i < x.Count; i++)
            {
                if (strikes[i] != null) points.Add(new Point(ScaleTime(x[i]), ApplyScale(strikes[i][0], ScaleType.Right)));
            }
            if ((strikes[0] != null) && (strikes[0].Count > 1))
            {
                singleLine = false;
                for (int i = x.Count - 1; i >= 0; i--)
                {
                    points.Add(new Point(ScaleTime(x[i]), ApplyScale(strikes[i][1], ScaleType.Right)));
                }
            }

            Brush lColor = color.Clone();
            lColor.Opacity = 0.15;
            if (points.Count > 1)
            {
                if (singleLine)
                {
                    Polyline polyline = new Polyline()
                    {
                        Points = points,
                        Stroke = lColor,
                        StrokeThickness = 8
                    };
                    c.Children.Add(polyline);
                }
                else
                {
                    Polygon polygon = new Polygon()
                    {
                        Points = points,
                        Fill = lColor
                    };
                    c.Children.Add(polygon);
                }
            }

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
                    double nextY = ApplyScale(y[i], scaleType);
                    Line l = new Line()
                    {
                        X1 = lastX,
                        Y1 = lastY,
                        X2 = nextX,
                        Y2 = nextY,
                        Stroke = color,
                        StrokeThickness = 2
                    };
                    if (scaleType != ScaleType.Left)
                    {
                        l.StrokeDashArray = new DoubleCollection() { 2 };
                        l.StrokeThickness = 1;
                    }
                    c.Children.Add(l);

                    if (scaleType == ScaleType.Left) DrawPip(nextX, nextY, color);

                    lastX = nextX;
                    lastY = nextY;
                }
                else
                {
                    // set first point
                    lastX = ScaleTime(x[0]);
                    lastY = ApplyScale(y[0], scaleType);

                    if (scaleType == ScaleType.Left) DrawPip(lastX, lastY, color);
                }
            }


        }

        private double ApplyScale(decimal v, ScaleType st)
        {
            double retval = 0;
            switch (st)
            {
                case ScaleType.Left:
                    retval = ScaleLeft(v);
                    break;
                case ScaleType.Right:
                    retval = ScaleRight(v);
                    break;
                case ScaleType.Percent:
                    retval = ScalePercent(v);
                    break;
            }
            return retval;
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


        private bool InitializeScale(List<GroupHistoryValue> values)
        {
            if (values.Count < 2)
            {
                scaleLeft = 0;
                scaleRight = 0;
                return false;
            }

            List<decimal> prices = values.Select(x => x.Underlying).ToList();
            foreach (GroupHistoryValue v in values)
            {
                List<decimal> strikes = new List<decimal>();
                if (v.Calls != null) strikes = strikes.Concat(v.Calls).ToList();
                if (v.Puts != null) strikes = strikes.Concat(v.Puts).ToList();
                strikes.RemoveAll(item => item == null);

                prices = prices.Concat(strikes).ToList();
            }

            minLeft = values.Min(x => x.Value);
            maxLeft = values.Max(x => x.Value);
            minRight = prices.Min();  // values.Min(x => x.Underlying);
            maxRight = prices.Max();  // values.Max(x => x.Underlying);
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
        private double ScalePercent(decimal y)
        {
            double val = Convert.ToDouble(y / 100);
            return (CtlHeight - (val * CtlHeight));
        }
        private double ScaleTime(DateTime dt)
        {
            TimeSpan ts = dt - minTime;
            return (ts.TotalMinutes * scaleTime) + margin;
        }

    }
}
