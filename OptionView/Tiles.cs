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

        public static void CreateTile(Window window, Canvas canvas, bool green, int ID, string symbol, string account, int left, int top, string strategy, string cost, string profit, string dte, bool alarm, string altLabel)
        {
            //<ContentControl Canvas.Top = "10" Canvas.Left = "10" Template = "{StaticResource DesignerItemTemplate}" >
            //  <Canvas Style = "{DynamicResource TileCanvas}" >
            //     < TextBlock Text = "Symbol" Style = "{DynamicResource SymbolHeader}" />
            //     < TextBlock Text = "Text" Canvas.Top = "19" Style = "{DynamicResource SymbolDetails}" />
            //     < TextBlock Text = "Text2" Canvas.Top = "38" Style = "{DynamicResource SymbolDetails}" />
            //     < Image Height = "16" Canvas.Top = "68" Width = "16" Source = "Icons/Alarm.ico" >
            //  </ Canvas >
            //</ ContentControl > 

            ContentControl cc = new ContentControl();
            cc.Template = (ControlTemplate)window.Resources["TileTemplate"];


            Canvas tileCanvas = new Canvas()
            {
                Height = 100,
                Width = 150,
                Style = (Style)window.Resources["TileCanvas"]
            };
            cc.Content = tileCanvas;


            Border border = new Border()
            {
                BorderThickness = new Thickness(3),
                Width = 150,
                Height = 100
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
            if (green)
            {
                gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF538C2B"), 0.974));
                gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF45761F"), 0.113));
            }
            else
            {
                gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFAE2C20"), 0.974));
                gradBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF91151D"), 0.113));
            }

            RotateTransform rt = new RotateTransform()
            {
                CenterX = 0.5,
                CenterY = 0.5,
                Angle = 315
            };
            gradBrush.RelativeTransform = rt;
            rect.Fill = gradBrush;

            border.Child = rect;
            tileCanvas.Children.Add(border);


            TextBlock txtSymbol = new TextBlock()
            {
                Text = symbol,
                Style = (Style)window.Resources["SymbolHeader"]
            };
            tileCanvas.Children.Add(txtSymbol);

            TextBlock txtAccount = new TextBlock()
            {
                Text = account,
                Style = (Style)window.Resources["SymbolDetailsRight"]
            };
            Canvas.SetRight(txtAccount, 22);
            txtAccount.HorizontalAlignment = HorizontalAlignment.Right;
            tileCanvas.Children.Add(txtAccount);

            TextBlock txtDetail1 = new TextBlock()
            {
                Text = strategy,
                Style = (Style)window.Resources["SymbolDetails"],
                Tag = "strategy",
            };
            Canvas.SetTop(txtDetail1, 18);
            tileCanvas.Children.Add(txtDetail1);

            TextBlock txtDetail2 = new TextBlock()
            {
                Text = ((altLabel != null) ? altLabel : "Cost") + ": " + cost,
                Style = (Style)window.Resources["SymbolDetails"]
            };
            Canvas.SetTop(txtDetail2, 32);
            tileCanvas.Children.Add(txtDetail2);

            TextBlock txtDetail3 = new TextBlock()
            {
                Text = "P/L: " + profit,
                Style = (Style)window.Resources["SymbolDetails"]
            };
            Canvas.SetTop(txtDetail3, 46);
            tileCanvas.Children.Add(txtDetail3);

            TextBlock txtDTE = new TextBlock()
            {
                Text = (dte.Length > 0) ? dte + " days" : "",
                Style = (Style)window.Resources["SymbolDetailsRight"]
            };
            Canvas.SetTop(txtDTE, 68);
            Canvas.SetRight(txtDTE, 22);
            txtDTE.HorizontalAlignment = HorizontalAlignment.Right;
            tileCanvas.Children.Add(txtDTE);

            Image img = new Image()
            {
                Height = 16,
                Width = 16,
                Source = new BitmapImage(new Uri("pack://application:,,,/icons/alarm.ico")),
                Visibility = alarm ? Visibility.Visible : Visibility.Hidden,
                Tag = "alarmIcon"
            };
            Canvas.SetTop(img, 68);
            tileCanvas.Children.Add(img);

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
                    ((TextBlock)FindChildByTag(children, "strategy")).Text = strategy;
                    ((Image)FindChildByTag(children, "alarmIcon")).Visibility = alarm ? Visibility.Visible : Visibility.Hidden;
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


