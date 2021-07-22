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
        public enum TileColor { Green, Red, Gray };

        public static void CreateTile(Window window, Canvas canvas, TileSize size, decimal profit, int ID, string symbol, string price, string account, int left, int top, string strategy, 
            string value1, string value2, string dte, bool itm, bool alarm, string altLabel1, string altLabel2, double opacity)
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
            //        <TextBlock Text = "Text3" Style = "{DynamicResource SymbolDetails}" />   
            //        <DockPanel Margin = "0,6,0,0" LastChildFill = "False" >
            //           <TextBlock Text = "66" Style = "{DynamicResource SymbolDetails}" DockPanel.Dock = "Right" VerticalAlignment = "Center" />
            //           <Border DockPanel.Dock = "Left" BorderBrush = "White" BorderThickness = "1" Margin = "0,0,6,0" >
            //              <TextBlock Text = "ITM" Style = "{DynamicResource SymbolITM}" />
            //           </Border >
            //           <Image Source = "Icons/alarm.ico" Height = "16" Width = "16" />
            //         </DockPanel >
            //      </StackPanel >


            TileColor color;

            if (value2 == "") color = TileColor.Gray;
            else if (profit > 0) color = TileColor.Green;
            else if (profit < 0) color = TileColor.Red;
            else color = TileColor.Gray;


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
                IsHitTestVisible = false
            };

            LinearGradientBrush gradBrush = new LinearGradientBrush();
            gradBrush.StartPoint = new Point(0.5, 0);
            gradBrush.EndPoint = new Point(0.5, 1);
            switch (color)
            { 
                case TileColor.Green:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF538C2B"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF45761F"), 0.113));
                    break;
                case TileColor.Red:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFAE2C20"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF91151D"), 0.113));
                    break;
                case TileColor.Gray:
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF909090"), 0.974));
                    gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF707070"), 0.113));
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
            rect.Fill = gradBrush;

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
                Style = (Style)window.Resources["SymbolPrice"]
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

            TextBlock txtDetail3 = new TextBlock()
            {
                Text = ((altLabel2 != null) ? altLabel2 : "P/L") + ": " + value2,
                Style = (Style)window.Resources["SymbolDetails"]
            };
            sp.Children.Add(txtDetail3);

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

                if (itm)
                {
                    Border itmBorder = new Border()
                    {
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    DockPanel.SetDock(itmBorder, Dock.Left);
                    dp.Children.Add(itmBorder);

                    TextBlock txtITM = new TextBlock()
                    {
                        Text = "ITM",
                        Style = (Style)window.Resources["SymbolITM"]
                    };
                    itmBorder.Child = txtITM;
                }

                Image img = new Image()
                {
                    Height = 16,
                    Width = 16,
                    Source = new BitmapImage(new Uri("pack://application:,,,/icons/alarm.ico")),
                    Visibility = alarm ? Visibility.Visible : Visibility.Hidden,
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

        public static void UpdateTile(int tag, Canvas canvas, string strategy, bool alarm)
        {
            foreach (ContentControl cc in canvas.Children)
            {
                if ((int)cc.Tag == tag)
                {
                    Debug.WriteLine("UpdateTile found: " + tag.ToString());
                    UIElementCollection children = ((Canvas)cc.Content).Children;
                    StackPanel sp = (StackPanel)children[1];
                    ((TextBlock)FindChildByTag(sp.Children, "strategy")).Text = strategy;

                    DockPanel dp = (DockPanel)sp.Children[4];  //footer dockpanel
                    ((Image)FindChildByTag(dp.Children, "alarmIcon")).Visibility = alarm ? Visibility.Visible : Visibility.Hidden;
                }
            }
        }

        private static UIElement FindChildByTag (UIElementCollection children, string tag)
        {
            foreach (UIElement elem in children)
            {
                string val = Convert.ToString(elem.GetType().GetProperty("Tag", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).GetValue(elem));
                if (val == tag) return elem;
            }
            return null;
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


