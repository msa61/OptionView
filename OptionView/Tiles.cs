using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;




namespace OptionView
{
    public class Tiles
    {
        public enum TileSize { Regular, Small };
        public enum TileColor { Green, Red, Gray, Blue };

        public static void CreateTile(Window window, Canvas canvas, TileSize size, decimal? profit, int ID, string symbol, string price, string account, int left, int top, string strategy, 
            string value1, string value2, string dte, bool itm, bool alarm, bool warning, string altLabel1, string altLabel2, string value2a, double opacity)
        {
            //<ContentControl Canvas.Top = "10" Canvas.Left = "10" Template = "{StaticResource DesignerItemTemplate}" >
            //  <Canvas Style = "{DynamicResource TileCanvas}" >
            //     < TextBlock Text = "Symbol" Style = "{DynamicResource SymbolHeader}" />
            //     < TextBlock Text = "Text" Canvas.Top = "19" Style = "{DynamicResource SymbolDetails}" />
            //     < TextBlock Text = "Text2" Canvas.Top = "38" Style = "{DynamicResource SymbolDetails}" />
            //     < Image Height = "16" Canvas.Top = "68" Width = "16" Source = "Icons/Alarm.ico" >
            //  </ Canvas >
            //</ ContentControl > 

            // version 2

            //     <StackPanel Orientation = "Vertical" Background = "Transparent" Width = "127">
            //        <DockPanel Margin = "0" >
            //           <TextBlock Text = "Acct" Style = "{DynamicResource SymbolDetails}" DockPanel.Dock = "Right" />
            //           <StackPanel DockPanel.Dock = "Top" Background = "Transparent" Orientation = "Horizontal" Margin = "0" >
            //              <TextBlock Text = "Symbol" Style = "{DynamicResource SymbolHeader}" />
            //              <TextBlock Text = "$123.00" Style = "{DynamicResource SymbolPrice}" />
            //           </StackPanel >
            //        </DockPanel >
            //        <TextBlock Text = "Text" Style = "{DynamicResource SymbolDetails}"  />
            //        <TextBlock Text = "Text2" Style = "{DynamicResource SymbolDetails}" />
            //        <DockPanel LastChildFill = "False" >
            //          <TextBlock Text = "Text3" Style = "{DynamicResource SymbolDetails}" DockPanel.Dock = "Left" />
            //          <TextBlock Text = "!" Style = "{DynamicResource SymbolDetails}" DockPanel.Dock = "Right" />
            //         </DockPanel >
            //        
            //        <DockPanel Margin = "0,6,0,0" LastChildFill = "False" >
            //           <TextBlock Text = "66" Style = "{DynamicResource SymbolDetails}" DockPanel.Dock = "Right" VerticalAlignment = "Center" />
            //           <Border DockPanel.Dock = "Left" BorderBrush = "White" BorderThickness = "1" Margin = "0,0,6,0" >
            //              <TextBlock Text = "ITM" Style = "{DynamicResource SymbolITM}" />
            //           </Border >
            //           <Image Source = "Icons/alarm.ico" Height = "16" Width = "16" />
            //         </DockPanel >
            //      </StackPanel >


            TileColor color = SelectColor(profit);

            double height = 100;
            double width = 150;

            if (size == TileSize.Small)
            {
                height = 80;
                width = 115;
            }

            ContentControl cc = new ContentControl();
            cc.Template = (ControlTemplate)window.Resources["TileTemplate"];


            Canvas tileCanvas = new Canvas()
            {
                Height = height,
                Width = width,
                Style = (Style)window.Resources["TileCanvas"]
            };
            cc.Content = tileCanvas;


            Border border = new Border()
            {
                BorderThickness = new Thickness(3),
                Height = height,
                Width = width
            };
            Canvas.SetTop(border, -10);
            Canvas.SetLeft(border, -10);
            Rectangle rect = new Rectangle()
            {
                RadiusX = 5.18,
                RadiusY = 5.18,
                IsHitTestVisible = false,
                Tag = "background"
            };

            rect.Fill = GetGradientBrush(color, opacity);

            border.Child = rect;
            tileCanvas.Children.Add(border);

            //
            // start adding text elements
            //

            StackPanel sp = new StackPanel()
            {
                Width = width - 23,
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent
            };
            tileCanvas.Children.Add(sp);

            DockPanel dp = new DockPanel()
            {
                Margin = new Thickness(0)
            };
            sp.Children.Add(dp);

            TextBlock txtAccount = new TextBlock()
            {
                Text = account,
                Style = (Style)window.Resources["SymbolDetails"]
           
            };
            DockPanel.SetDock(txtAccount, Dock.Right);
            dp.Children.Add(txtAccount);

            StackPanel spInner = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0),
                Background = Brushes.Transparent
            };
            DockPanel.SetDock(spInner, Dock.Top);
            dp.Children.Add(spInner);

            TextBlock txtSymbol = new TextBlock()
            {
                Text = symbol,
                Style = (Style)window.Resources["SymbolHeader"]
            };
            spInner.Children.Add(txtSymbol);

            txtSymbol = new TextBlock()
            {
                Text = price,
                Style = (Style)window.Resources["SymbolPrice"],
                Tag = "price"
            };
            spInner.Children.Add(txtSymbol);


            TextBlock txtDetail1 = new TextBlock()
            {
                Text = strategy,
                Style = (Style)window.Resources["SymbolDetails"],
                Tag = "strategy",
            };
            sp.Children.Add(txtDetail1);

            TextBlock txtDetail2 = new TextBlock()
            {
                Text = ((altLabel1 != null) ? altLabel1 : "Cost") + ": " + value1,
                Style = (Style)window.Resources["SymbolDetails"]
            };
            sp.Children.Add(txtDetail2);

            dp = new DockPanel()  // for second to last row
            {
                LastChildFill = false
            };
            sp.Children.Add(dp);

            TextBlock txtDetail3 = new TextBlock()
            {
                Text = ((altLabel2 != null) ? altLabel2 : "P/L") + ": " + value2,
                Style = (Style)window.Resources["SymbolDetails"],
                Tag = "value"
            };
            DockPanel.SetDock(txtDetail3, Dock.Left);
            dp.Children.Add(txtDetail3);

            if (value2a != null)
            {
                TextBlock txtDetail3a = new TextBlock()
                {
                    Text = value2a,
                    Style = (Style)window.Resources["SymbolChangeInValue"],
                    Tag = "change"
                };
                DockPanel.SetDock(txtDetail3, Dock.Left);
                dp.Children.Add(txtDetail3a);
            }

            // warning symbol for lack of cooresponding order
            if (size == TileSize.Regular)
            {
                TextBlock txtOrder = new TextBlock()
                {
                    Text = warning ? "∆" : "",
                    Style = (Style)window.Resources["SymbolHeader"],
                    Tag = "order"
                };
                DockPanel.SetDock(txtOrder, Dock.Right);
                dp.Children.Add(txtOrder);
            }

            //
            // end of main dockpanel, start footer dockpanel
            if (size == TileSize.Regular)
            {
                dp = new DockPanel()
                {
                    Margin = new Thickness(0, 6, 0, 0),
                    LastChildFill = false
                };
                sp.Children.Add(dp);

                TextBlock txtDTE = new TextBlock()
                {
                    Text = (dte.Length > 0) ? dte + "d" : "",
                    Style = (Style)window.Resources["SymbolDetails"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(txtDTE, Dock.Right);
                dp.Children.Add(txtDTE);

                Border itmBorder = new Border()
                {
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 0),
                    Visibility = itm ? Visibility.Visible : Visibility.Collapsed,
                    Tag = "itmborder"
                };
                DockPanel.SetDock(itmBorder, Dock.Left);
                dp.Children.Add(itmBorder);

                TextBlock txtITM = new TextBlock()
                {
                    Text = "ITM",
                    Style = (Style)window.Resources["SymbolITM"]
                };
                itmBorder.Child = txtITM;

                Image img = new Image()
                {
                    Height = 16,
                    Width = 16,
                    Source = new BitmapImage(new Uri("pack://application:,,,/icons/alarm.ico")),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Visibility = alarm ? Visibility.Visible : Visibility.Collapsed,
                    Tag = "alarmIcon"
                };
                dp.Children.Add(img);
            }

            //
            // end of elements
            //

            Canvas.SetLeft(cc, left);
            Canvas.SetTop(cc, top);

            cc.Tag = ID;
            canvas.Children.Add(cc);
        }

        public static TileColor SelectColor(decimal? value)
        {
            TileColor retval = TileColor.Gray;

            if (value == null) retval = TileColor.Gray;
            else if (value > 0) retval = TileColor.Green;
            else if (value < 0) retval = TileColor.Red;
            else retval = TileColor.Blue;

            return retval;
        }

        public static LinearGradientBrush GetGradientBrush(TileColor color, double opacity)
        {
            LinearGradientBrush gradBrush = new LinearGradientBrush();
            gradBrush.StartPoint = new Point(0.5, 0);
            gradBrush.EndPoint = new Point(0.5, 1);
            switch (color)
            {
                case TileColor.Green:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF66A639"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF45761F"), 0.113));
                    break;
                case TileColor.Red:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFC33F32"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF91151D"), 0.113));
                    break;
                case TileColor.Gray:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF909090"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF707070"), 0.113));
                    break;
                case TileColor.Blue:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF5380C1"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF2056A2"), 0.113));
                    break;
            }

            RotateTransform rt = new RotateTransform()
            {
                CenterX = 0.5,
                CenterY = 0.5,
                Angle = 315
            };
            gradBrush.RelativeTransform = rt;
            gradBrush.Opacity = opacity;
            return gradBrush;
        }


        public static void UpdateTile(int tag, Canvas canvas, string strategy, bool alarm)
        {
            UpdateTile(tag, canvas, null, null, null, null, strategy, alarm);
        }
        
        public static void UpdateTile(int tag, Canvas canvas, decimal? profit, string price, string value, string change, string strategy, bool? alarm = null, bool? order = null, bool? itm = null)
        {

            foreach (ContentControl cc in canvas.Children)
            {
                if ((int)cc.Tag == tag)
                {
                    Debug.WriteLine("UpdateTile found: " + tag.ToString());

                    if (profit != null)
                    { 
                        IEnumerable<Rectangle> rectangles = FindVisualChildren<Rectangle>(cc);
                        foreach (Rectangle rect in rectangles)
                        {
                            if ((string)rect.Tag == "background")
                            {
                                TileColor color = SelectColor(profit);
                                GradientBrush brush = GetGradientBrush(color, 1.0);
                                rect.Fill = brush;
                                break;
                            }
                        }
                    }

                    IEnumerable<TextBlock> textBlocks = FindVisualChildren<TextBlock>(cc);
                    foreach (TextBlock tb in textBlocks)
                    {
                        switch ((string)tb.Tag)
                        {
                            case "price":
                                if (price != null) tb.Text = price;
                                break;
                            case "value":
                                if (value != null) tb.Text = value;
                                break;
                            case "change":
                                if (change != null) tb.Text = change;
                                break;
                            case "strategy":
                                if (strategy != null) tb.Text = strategy;
                                break;
                            case "order":
                                if (order != null) tb.Text = (order ?? false) ? "∆" : "";
                                break;
                        }
                    }

                    if (alarm != null)
                    { 
                        IEnumerable<Image> images = FindVisualChildren<Image>(cc);
                        foreach (Image img in images)
                        {
                            if ((string)img.Tag == "alarmIcon")
                            {
                                img.Visibility = (alarm ?? false) ? Visibility.Visible : Visibility.Collapsed;
                                break;
                            }
                        }
                    }

                    if (itm != null)
                    {
                        IEnumerable<Border> borders = FindVisualChildren<Border>(cc);
                        foreach (Border bord in borders)
                        {
                            if ((string)bord.Tag == "itmborder")
                            {
                                bord.Visibility = (itm ?? false) ? Visibility.Visible : Visibility.Collapsed;
                                break;
                            }
                        }
                    }

                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield return (T)Enumerable.Empty<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject ithChild = VisualTreeHelper.GetChild(depObj, i);
                if (ithChild == null) continue;
                if (ithChild is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
            }
        }

        public static void UpdateTilePosition(string tag, int x, int y)
        {
            try
            {
                // establish connection
                App.OpenConnection();

                // update all of the rows in the chain
                string sql = "UPDATE transgroup SET x = @x, y = @y WHERE ID=@row";
                SQLiteCommand cmdUpd = new SQLiteCommand(sql, App.ConnStr);
                cmdUpd.Parameters.AddWithValue("x", x);
                cmdUpd.Parameters.AddWithValue("y", y);
                cmdUpd.Parameters.AddWithValue("row", tag);
                cmdUpd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateTilePosition: " + ex.Message);
            }
        }




    }
}


