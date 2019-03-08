using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;




namespace OptionView
{
    public class Tiles
    {

        public void CreateTile(Window window, Canvas canvas, bool green, string symbol, int left, int top, string details)
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

            TextBlock txtSymbol = new TextBlock()
            {
                Text = symbol,
                Style = (Style)window.Resources["SymbolHeader"]
            };
            tileCanvas.Children.Add(txtSymbol);

            TextBlock txtDetail1 = new TextBlock()
            {
                Text = details,
                Style = (Style)window.Resources["SymbolDetails"],
            };
            Canvas.SetTop(txtDetail1, 14);
            tileCanvas.Children.Add(txtDetail1);

            TextBlock txtDetail2 = new TextBlock()
            {
                Text = "tbd",
                Style = (Style)window.Resources["SymbolDetails"]
            };
            Canvas.SetTop(txtDetail2, 28);
            tileCanvas.Children.Add(txtDetail2);


            Canvas.SetLeft(cc, left);
            Canvas.SetTop(cc, top);

            canvas.Children.Add(cc);
        }
    }
}


