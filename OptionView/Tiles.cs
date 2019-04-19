using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;





namespace OptionView
{
    public class Tiles
    {

        public static void CreateTile(Window window, Canvas canvas, bool green, int ID, string symbol, int left, int top, string details1, string details2, string dte)
        {
            //<ContentControl Canvas.Top = "10" Canvas.Left = "10" Template = "{StaticResource DesignerItemTemplate}" >
            //  <Canvas Style = "{DynamicResource TileCanvas}" >
            //     < TextBlock Text = "Symbol" Style = "{DynamicResource SymbolHeader}" />
            //     < TextBlock Text = "Text" Canvas.Top = "19" Style = "{DynamicResource SymbolDetails}" />
            //     < TextBlock Text = "Text2" Canvas.Top = "38" Style = "{DynamicResource SymbolDetails}" />
            //  </ Canvas >
            //</ ContentControl > 

            ContentControl cc = new ContentControl();
            if (green)
                cc.Template = (ControlTemplate)window.Resources["GreenTileTemplate"];
            else
                cc.Template = (ControlTemplate)window.Resources["RedTileTemplate"];


            Canvas tileCanvas = new Canvas();
            tileCanvas.Style = (Style)window.Resources["TileCanvas"];
            cc.Content = tileCanvas;


            Rectangle rect = new Rectangle()
            {
                Name = "SelectedFrame",
                Stroke = Brushes.Gray,
                Visibility = Visibility.Hidden,
                StrokeThickness= 1,
                StrokeDashArray = new DoubleCollection() { 2 },
                IsHitTestVisible = false,
                Margin = new Thickness(-10,-10,0,0),
                Width = 150,
                Height = 100
            };
            tileCanvas.Children.Add(rect);


            TextBlock txtSymbol = new TextBlock()
            {
                Text = symbol,
                Style = (Style)window.Resources["SymbolHeader"]
            };
            tileCanvas.Children.Add(txtSymbol);

            TextBlock txtDetail1 = new TextBlock()
            {
                Text = details1,
                Style = (Style)window.Resources["SymbolDetails"],
            };
            Canvas.SetTop(txtDetail1, 18);
            tileCanvas.Children.Add(txtDetail1);

            TextBlock txtDetail2 = new TextBlock()
            {
                Text = details2,
                Style = (Style)window.Resources["SymbolDetails"]
            };
            Canvas.SetTop(txtDetail2, 32);
            tileCanvas.Children.Add(txtDetail2);

            TextBlock txtDTE = new TextBlock()
            {
                Text = dte + " days",
                Style = (Style)window.Resources["DaysTilExpiration"]
            };
            Canvas.SetTop(txtDTE, 68);
            txtDTE.HorizontalAlignment = HorizontalAlignment.Right;
            tileCanvas.Children.Add(txtDTE);


            Canvas.SetLeft(cc, left);
            Canvas.SetTop(cc, top);

            cc.Tag = ID;
            canvas.Children.Add(cc);
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


