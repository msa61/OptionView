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
            ResetScreen();



            Portfolio p = HoldingsHelper.CurrentHoldings();
            foreach (Underlying u in p)
            {
                Tiles.CreateTile(this, MainCanvas, (u.Cost > 0), u.TransactionGroup, u.Symbol, u.X, u.Y, u.Comments, u.Cost.ToString());
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


 
    }
}
