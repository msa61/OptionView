﻿using System;
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
    /// Interaction logic for SnapShot.xaml
    /// </summary>
    public partial class SnapShot : UserControl
    {
        public double CtlWidth { get; set; } = 190;
        public double CtlHeight { get; set; } = 90;
        public decimal Price { get; set; }
        public decimal ShortPut { get; set; }
        public decimal LongPut { get; set; }
        public decimal ShortCall { get; set; }
        public decimal LongCall { get; set; }
        public decimal LongStock { get; set; }
        public decimal ShortStock { get; set; }
        public string DeltaText { get; set; } = "";
        public Brush DeltaColor { get; set; }
        

        private double xPt;
        private double scale;
        private decimal minVal;
        private int lineMargin = 8;


        public SnapShot()
        {
            InitializeComponent();
            Loaded += SnapShot_Loaded;
        }

        private void SnapShot_Loaded(object sender, RoutedEventArgs e)
        {
            if (Price > 0) Update();
        }

        public void Clear()
        {
            ((Grid)(this.Content)).Children.Clear();
            this.Price = 0;
            this.ShortCall = 0;
            this.ShortPut = 0;
            this.ShortStock = 0;
            this.LongCall = 0;
            this.LongPut = 0;
            this.LongStock = 0;
            this.DeltaText = "";
            this.DeltaColor = null;
        }

        public void Update()
        { 
            StackPanel sp = (StackPanel)VisualTreeHelper.GetParent(this);
            CtlWidth = sp.ActualWidth - this.Margin.Left - this.Margin.Right;

            ((Grid)(this.Content)).Children.Clear();

            if (!InitializeScale()) return;

            // < Canvas Width = "{Binding Width}" Height = "{Binding Height}" Background = "LightGray" />
            Canvas c = new Canvas()
            {
                Height = CtlHeight,
                Width = CtlWidth,
                Background = this.Background ?? Brushes.Gray
            };
            ((Grid)(this.Content)).Children.Add(c);


            //< Line X1 = "10" Y1 = "50" X2 = "176" Y2 = "50" Stroke = "LightBlue" StrokeThickness = "5" />
            Line l = new Line()
            {
                X1 = lineMargin,
                Y1 = CtlHeight / 2,
                X2 = CtlWidth - lineMargin,
                Y2 = CtlHeight / 2,
                Stroke = Brushes.LightBlue,
                StrokeThickness = 5
            };
            c.Children.Add(l);

            if (DeltaText.Length > 0)
            {
                TextBlock tb = new TextBlock()
                {
                    Text = "Δ: " + DeltaText,
                    FontSize = 14,
                    Foreground = DeltaColor ?? Brushes.LightGray,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Width = 70
                };
                Canvas.SetTop(tb, 10);
                Canvas.SetLeft(tb, (CtlWidth / 2) - 35);
                c.Children.Add(tb);
            }

            bool highlightPrice = false;
            if (((Price < ShortPut) && (ShortPut > 0)) || ((Price > ShortCall) && (ShortCall > 0))) highlightPrice = true;
            //< Ellipse Width = "8" Height = "8" Canvas.Left = "93" Canvas.Top = "46" Stroke = "Blue" StrokeThickness = "2" />
            Ellipse circ = new Ellipse()
            {
                Width = 8,
                Height = 8,
                Stroke = highlightPrice ? Brushes.Red : Brushes.Blue,
                StrokeThickness = 2,
                ToolTip = Price.ToString("C2")
            };
            Canvas.SetTop(circ, (CtlHeight / 2) - 4);
            Canvas.SetLeft(circ, Convert.ToInt32(Scale(Price)));  // current price
            c.Children.Add(circ);


            if (LongPut > 0) AddPointer(c, "P", LongPut, false, false);
            if (ShortPut > 0) AddPointer(c, "P", ShortPut, true, false);

            if (LongCall > 0) AddPointer(c, "C", LongCall, false, (LongCall == LongPut));
            if (ShortCall > 0) AddPointer(c, "C", ShortCall, true, (ShortCall == ShortPut));

            if (LongStock > 0) AddPointer(c, "S", LongStock, false, (LongStock == LongPut) || (LongStock == LongCall));
            if (ShortStock > 0) AddPointer(c, "S", ShortStock, true, (ShortStock == ShortPut) || (ShortStock == ShortCall));
        }

        private void AddPointer(Canvas c, string txt, decimal val, bool isShortSym, bool isOffset)
        {
            double left = Scale(val);

            double offset = 0;
            if (isOffset)
            {
                offset = isShortSym ? 15 : -15;
            }

            ContentControl cc = new ContentControl()
            {
                Content = (Polygon)this.Resources[isShortSym ? "ShortPointer" : "LongPointer"]
            };
            Canvas.SetTop(cc, (this.CtlHeight / 2) + offset);
            Canvas.SetLeft(cc, left);
            c.Children.Add(cc);

            TextBlock t = new TextBlock()
            {
                Text = txt,
                FontSize = 9,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                ToolTip = val.ToString("C2")
            };
            double top = (this.CtlHeight / 2) + (isShortSym ? 4 : -17) + offset;
            Canvas.SetTop(t, top);
            Canvas.SetLeft(t, left - 3);
            c.Children.Add(t);
        }

        private bool InitializeScale()
        {
            List<decimal> vals = new List<decimal>() { Price, ShortPut, LongPut, ShortCall, LongCall, ShortStock, LongStock };
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

            double lineLen = CtlWidth - (2 * lineMargin);
            scale = lineLen * 0.8 / Convert.ToDouble(maxVal - minVal);  // leave 10% on both ends of lines
            xPt = (lineMargin + (0.1 * lineLen));
            return true;
        }

        private double Scale (decimal x)
        {
            double delta = Convert.ToDouble(x - minVal);
            return (delta * scale) + xPt;
        }

    }
}
