using System;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int left = 10;
        private int top = 10;
        private bool nextColor = true;



        public MainWindow()
        {
            //DataLoader.Load("all transactions.csv");
            //DataLoader.Load("feb-19.csv");
            //DataLoader.Load("feb-22.csv");
            //DataLoader.Load("feb-24.csv");
            //DataLoader.Load("mar-5.csv");
            //DataLoader.Load("mar-7.csv");
            //DataLoader.Load("gld.csv");
            //DataLoader.Load("spy.csv");
            //DataLoader.Load("msft.csv");

            //DataLoader.Load("DALcorrection.csv");

            HoldingsHelper.UpdateNewTransactions();


            InitializeComponent();



            App.CloseConnection();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            left += 10;
            top += 10;
            nextColor = !nextColor;
            //CreateTile(nextColor, "NDVA", left, top, "blah");

            Canvas canvas = MainCanvas;
            Tiles t = new Tiles();
            t.CreateTile(this, MainCanvas, nextColor, "NDVA", left, top, "blah");

        }

        //private void CreateTile(bool green, string symbol, int left, int top, string details)
        //{
        //    //<ContentControl Canvas.Top = "10" Canvas.Left = "10" Template = "{StaticResource DesignerItemTemplate}" >
        //    //  <Canvas Style = "{DynamicResource TileCanvas}" >
        //    //     < TextBlock Text = "Symbol" Style = "{DynamicResource SymbolHeader}" />
        //    //     < TextBlock Text = "Text" Canvas.Top = "19" Style = "{DynamicResource SymbolDetails}" />
        //    //     < TextBlock Text = "Text2" Canvas.Top = "38" Style = "{DynamicResource SymbolDetails}" />
        //    //  </ Canvas >
        //    //</ ContentControl > 

        //    ContentControl cc = new ContentControl();
        //    if (green)
        //        cc.Template = (ControlTemplate)this.Resources["GreenTileTemplate"];
        //    else
        //        cc.Template = (ControlTemplate)this.Resources["RedTileTemplate"];


        //    Canvas tileCanvas = new Canvas();
        //    tileCanvas.Style = (Style)this.Resources["TileCanvas"];
        //    cc.Content = tileCanvas;

        //    TextBlock txtSymbol = new TextBlock()
        //    {
        //        Text = symbol,
        //        Style = (Style)this.Resources["SymbolHeader"]
        //    };
        //    tileCanvas.Children.Add(txtSymbol);

        //    TextBlock txtDetail1 = new TextBlock()
        //    {
        //        Text = details,
        //        Style = (Style)this.Resources["SymbolDetails"],
        //    };
        //    Canvas.SetTop(txtDetail1, 14);
        //    tileCanvas.Children.Add(txtDetail1);

        //    TextBlock txtDetail2 = new TextBlock()
        //    {
        //        Text = "tbd",
        //        Style = (Style)this.Resources["SymbolDetails"]
        //    };
        //    Canvas.SetTop(txtDetail2, 28);
        //    tileCanvas.Children.Add(txtDetail2);


        //    Canvas.SetLeft(cc, left);
        //    Canvas.SetTop(cc, top);

        //    MainCanvas.Children.Add(cc);
        //}
    }
}
