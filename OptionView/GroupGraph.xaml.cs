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
        public double CtlHeight { get; set; } = 160;
        private double margin = 30;
        private double chartWidth;
        private double legendHeight = 60;
        private double chartHeight;
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

        public enum ScaleType
        {
            Left,
            Right,
            Bottom,
            Percent,
            None
        }


        public GroupGraph(GroupHistory hist)
        {
            InitializeComponent();

            chartWidth = CtlWidth - (2 * margin);
            chartHeight = CtlHeight - legendHeight;

            if (InitializeScale(hist) == false) return;

            CreateCanvas();
            DrawAxis();

            List<GroupHistoryValue> values = hist.Values.Select(x => x.Value).ToList();
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.IV).ToList(), ScaleType.Percent, Brushes.DarkGray);
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Underlying).ToList(), ScaleType.Right, Brushes.Blue);
            DrawGraphLine(values.Select(x => x.Time).ToList(), values.Select(x => x.Value).ToList(), ScaleType.Left, Brushes.Transparent); // 'transparent' lets the function figure out the color

            DrawBands(hist.Calls, Brushes.Red);
            DrawBands(hist.Puts, Brushes.Red);

            DrawLegend();
        }

        private void DrawLegend()
        {
            Line l = new Line()
            {
                X1 = margin + 20,
                Y1 = chartHeight + 25,
                X2 = margin + 30,
                Y2 = chartHeight + 25,
                Stroke = Brushes.Green,
                StrokeThickness = 1
            };
            c.Children.Add(l);
            l = new Line()
            {
                X1 = margin + 30,
                Y1 = chartHeight + 25,
                X2 = margin + 40,
                Y2 = chartHeight + 25,
                Stroke = Brushes.Red,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            AddLabel("Group Value", margin + 45, chartHeight + 25, ScaleType.None);

            // price
            l = new Line()
            {
                X1 = margin + 20,
                Y1 = chartHeight + 39,
                X2 = margin + 40,
                Y2 = chartHeight + 39,
                Stroke = Brushes.Blue,
                StrokeDashArray = new DoubleCollection() { 2 },
                StrokeThickness = 1
            };
            c.Children.Add(l);

            AddLabel("Underlying Price", margin + 45, chartHeight + 39, ScaleType.None);

            // iv
            l = new Line()
            {
                X1 = margin + 20,
                Y1 = chartHeight + 53,
                X2 = margin + 40,
                Y2 = chartHeight + 53,
                Stroke = Brushes.DarkGray,
                StrokeDashArray = new DoubleCollection() { 2 },
                StrokeThickness = 1
            };
            c.Children.Add(l);

            AddLabel("Underlying IV", margin + 45, chartHeight + 53, ScaleType.None);
        }

        private void DrawBands(SortedList<DateTime, List<decimal>> strikeList, Brush color)
        {
            for (int i = 0; i < strikeList.Count; i++)
            {
                bool singleLine = true;
                PointCollection points = new PointCollection();

                KeyValuePair<DateTime,List<decimal>> item = strikeList.ElementAt(i);
                DateTime time = item.Key;
                List<decimal> list = item.Value;

                if (list.Count == 0) break;

                points.Add(new Point(ScaleTime((i==0) ? minTime : time), ApplyScale(list.Min(), ScaleType.Right)));
                if (list.Count > 1)
                {
                    singleLine = false;
                    points.Add(new Point(ScaleTime((i == 0) ? minTime : time), ApplyScale(list.Max(), ScaleType.Right)));
                }

                if ((i+1) >= strikeList.Count)
                {
                    time = maxTime;
                }
                else
                {
                    time = strikeList.ElementAt(i + 1).Key;
                }
                points.Add(new Point(ScaleTime(time), ApplyScale(list.Max(), ScaleType.Right)));
                if (list.Count > 1) points.Add(new Point(ScaleTime(time), ApplyScale(list.Min(), ScaleType.Right)));


                Brush lColor = color.Clone();
                lColor.Opacity = 0.15;
                if (points.Count > 1)
                {
                    if ((singleLine) || (points[0].Y == points[1].Y))  // single line or rectangle with no height
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
                    Line l = DrawLineSegment(c, lastX, lastY, nextX, nextY, ApplyScale(0, scaleType), color);
                    if (scaleType != ScaleType.Left)
                    {
                        l.StrokeDashArray = new DoubleCollection() { 2 };
                        l.StrokeThickness = 1;
                    }
                    //if (l != null) c.Children.Add(l);

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

        private Line DrawLineSegment(Canvas c, double lastX, double lastY, double nextX, double nextY, double zeroY, Brush color)
        {
            bool determineColor = (color == Brushes.Transparent);
            bool singleSegment = !(determineColor && (Math.Min(lastY, nextY) < zeroY) && (Math.Max(lastY, nextY) > zeroY));

            Line l = null;
            if (singleSegment)
            {
                if (determineColor)
                {
                    color = (((lastY+nextY)/2) <= zeroY) ? Brushes.Green : Brushes.Red;
                }
                l = new Line()
                {
                    X1 = lastX,
                    Y1 = lastY,
                    X2 = nextX,
                    Y2 = nextY,
                    Stroke = color,
                    StrokeThickness = 2
                };
                c.Children.Add(l);
            }
            else
            {
                double slope = (nextY - lastY) / (nextX - lastX);
                double zeroX = ((zeroY - lastY) / slope) + lastX;

                color = (lastY < zeroY) ? Brushes.Green : Brushes.Red;
                l = new Line()
                {
                    X1 = lastX,
                    Y1 = lastY,
                    X2 = zeroX,
                    Y2 = zeroY,
                    Stroke = color,
                    StrokeThickness = 2
                };
                c.Children.Add(l);

                color = (lastY < zeroY) ? Brushes.Red : Brushes.Green;
                l = new Line()
                {
                    X1 = zeroX,
                    Y1 = zeroY,
                    X2 = nextX,
                    Y2 = nextY,
                    Stroke = color,
                    StrokeThickness = 2
                };
                c.Children.Add(l);
            }


            return l;
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
                Y2 = chartHeight,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            l = new Line()
            {
                X1 = chartWidth + margin,
                Y1 = 0,
                X2 = chartWidth + margin,
                Y2 = chartHeight,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            double yVal = chartHeight;
            if ((minLeft < 0) && (maxLeft > 0)) yVal = ScaleLeft(0);
            else if (maxLeft < 0) yVal = 0;
            l = new Line()
            {
                X1 = margin,
                Y1 = yVal,
                X2 = chartWidth + margin,
                Y2 = yVal,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 1
            };
            c.Children.Add(l);

            AddLabel(maxRight, margin, 0, ScaleType.Left, Brushes.Blue);
            AddLabel(minRight, margin, chartHeight, ScaleType.Left, Brushes.Blue);
            AddLabel(maxLeft, chartWidth + margin, 0, ScaleType.Right, (maxLeft > 0) ? Brushes.Green : Brushes.Red);
            AddLabel(minLeft, chartWidth + margin, chartHeight, ScaleType.Right, (minLeft == 0) ? null : (minLeft > 0) ? Brushes.Green : Brushes.Red);

            AddLabel(minTime.ToString("M/d"), margin, chartHeight + 3, ScaleType.Bottom);
            AddLabel(maxTime.ToString("M/d"), chartWidth + margin, chartHeight + 3, ScaleType.Bottom);

        }

        private void AddLabel(decimal val, double left, double top, ScaleType scaleType, Brush color = null)
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

            AddLabel(txt, left, top, scaleType, color);
        }

        private void AddLabel(string txt, double left, double top, ScaleType scaleType, Brush color = null)
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
                TextAlignment = ((scaleType == ScaleType.Right) || (scaleType == ScaleType.None)) ? TextAlignment.Left : (scaleType == ScaleType.Left) ? TextAlignment.Right : TextAlignment.Center
            };
            if (color != null) tb.Foreground = color;
            if (scaleType == ScaleType.Bottom)
            {
                Canvas.SetTop(tb, top +2);
                Canvas.SetLeft(tb, left - (ft.Width/2));
            }
            else if (scaleType == ScaleType.None)
            {
                Canvas.SetTop(tb, top - ft.Height/2 - 1);
                Canvas.SetLeft(tb, left);
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
                Margin = new Thickness(10, 20, 10, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            ((Grid)(this.Content)).Children.Add(c);
        }


        private bool InitializeScale(GroupHistory hist)
        {
            if (hist == null) return false;

            List<GroupHistoryValue> values = hist.Values.Select(x => x.Value).ToList();
            if (values.Count < 2)
            {
                scaleLeft = 0;
                scaleRight = 0;
                return false;
            }

            List<decimal> prices = values.Select(x => x.Underlying).ToList();
            List<decimal> strikes = new List<decimal>();
            foreach (KeyValuePair<DateTime, List<decimal>> x in hist.Calls)
            {
                strikes = strikes.Concat(x.Value).ToList();
            }
            foreach (KeyValuePair<DateTime, List<decimal>> x in hist.Puts)
            {
                strikes = strikes.Concat(x.Value).ToList();
            }
            prices = prices.Concat(strikes).ToList();

            minLeft = values.Min(x => x.Value);
            maxLeft = values.Max(x => x.Value);
            minRight = prices.Min();  // values.Min(x => x.Underlying);
            maxRight = prices.Max();  // values.Max(x => x.Underlying);
            minTime = values.Min(x => x.Time); 
            maxTime = values.Max(x => x.Time);

            if (minLeft == maxLeft) 
                scaleLeft = 1;
            else
                scaleLeft = chartHeight / Convert.ToDouble(maxLeft - minLeft);

            if (minRight == maxRight)
                scaleRight = 1;
            else
                scaleRight = chartHeight / Convert.ToDouble(maxRight - minRight);

            if (minTime == maxTime)
                scaleTime = 1;
            else
            {
                TimeSpan ts = maxTime - minTime;
                scaleTime = chartWidth / ts.TotalMinutes;
            }

            return true;
        }

        private double ScaleLeft(decimal y)
        {
            double val = Convert.ToDouble(y - minLeft);
            return (chartHeight - (val * scaleLeft));
        }
        private double ScaleRight(decimal y)
        {
            double val = Convert.ToDouble(y - minRight);
            return (chartHeight - (val * scaleRight));
        }
        private double ScalePercent(decimal y)
        {
            double val = Convert.ToDouble(y / 100);
            return (chartHeight - (val * chartHeight));
        }
        private double ScaleTime(DateTime dt)
        {
            TimeSpan ts = dt - minTime;
            if (ts.TotalMinutes < 0) return margin;
            return (ts.TotalMinutes * scaleTime) + margin;
        }

        public static int Weekdays(DateTime dtmStart, DateTime dtmEnd)
        {
            // This function includes the start and end date in the count if they fall on a weekday
            int dowStart = ((int)dtmStart.DayOfWeek == 0 ? 7 : (int)dtmStart.DayOfWeek);
            int dowEnd = ((int)dtmEnd.DayOfWeek == 0 ? 7 : (int)dtmEnd.DayOfWeek);
            TimeSpan tSpan = dtmEnd - dtmStart;
            if (dowStart <= dowEnd)
            {
                return (((tSpan.Days / 7) * 5) + Math.Max((Math.Min((dowEnd + 1), 6) - dowStart), 0));
            }
            return (((tSpan.Days / 7) * 5) + Math.Min((dowEnd + 6) - Math.Min(dowStart, 6), 5));
        }

    }
}
