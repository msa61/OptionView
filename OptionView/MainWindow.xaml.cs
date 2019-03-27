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
using System.ComponentModel;
using System.Diagnostics;


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
        private Portfolio portfolio;



        public MainWindow()
        {
            //DataLoader.Load("all transactions.csv");
            //DataLoader.Load("feb-19.csv");
            //DataLoader.Load("feb-22.csv");
            //DataLoader.Load("feb-24.csv");
            //DataLoader.Load("mar-5.csv");
            //DataLoader.Load("mar-7.csv");
            //DataLoader.Load("mar-13.csv");
            //DataLoader.Load("mar-25.csv");
            //DataLoader.Load("gld.csv");
            //DataLoader.Load("spy.csv");
            //DataLoader.Load("msft.csv");

            //DataLoader.Load("DALcorrection.csv");


            HoldingsHelper.UpdateNewTransactions();

            InitializeComponent();
            ResetScreen();



            portfolio = HoldingsHelper.CurrentHoldings();
            foreach (KeyValuePair<int,Underlying> entry in portfolio)
            {
                Underlying u = entry.Value;
                Tiles.CreateTile(this, MainCanvas, (u.Cost > 0), u.TransactionGroup, u.Symbol, u.X, u.Y, u.Strategy, u.Cost.ToString(), (u.EarliestExpiration - DateTime.Today).TotalDays.ToString());
            }

            App.CloseConnection();
        }

        private void ResetScreen()
        {
            string scrnProps = Config.GetProp("Screen");
            string[] props = scrnProps.Split('|');

            if (props.Length < 5) return;

            if (props[0] == "1") this.WindowState = WindowState.Maximized;
            this.Left = Convert.ToDouble(props[1]);
            this.Top = Convert.ToDouble(props[2]);
            this.Width = Convert.ToDouble(props[3]);
            this.Height = Convert.ToDouble(props[4]);
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("window closing");

            string scrnProps = ((this.WindowState == WindowState.Maximized) ? "1|" : "0|") + this.Left.ToString() + "|" + this.Top.ToString() + "|" + this.Width.ToString() + "|" + this.Height.ToString();
            Config.SetProp("Screen", scrnProps);

            foreach (ContentControl cc in MainCanvas.Children)
            {
                HoldingsHelper.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }
        }


        AdornerLayer adornerLayer = null;
        TileAdorner tileAdorner = null;
        private void TileMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender.GetType() == typeof(Rectangle))
            {
                if (adornerLayer != null && tileAdorner != null) adornerLayer.Remove(tileAdorner);

                Rectangle rect = (Rectangle)sender;
                adornerLayer = AdornerLayer.GetAdornerLayer(rect);
                tileAdorner = new TileAdorner(rect);
                adornerLayer.Add(tileAdorner);


                MoveTile tile = (MoveTile)VisualTreeHelper.GetParent(rect);
                if (tile != null && tile.Parent != null)
                {
                    ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(tile.Parent);
                    int tag = 0;
                    if (cc != null && cc.Tag.GetType() == typeof(int)) tag = (int)cc.Tag;

                    if (tag > 0)
                    {
                        Debug.WriteLine("Group selected: " + tag.ToString());


                    }


                }

            }
        }
 
    }
}
