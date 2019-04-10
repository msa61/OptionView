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
using Microsoft.Win32;


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
        private int selectedTag = 0;
        private bool detailsDirty = false;




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
            DataLoader.Load("apr-9.csv");
            //DataLoader.Load("gld.csv");
            //DataLoader.Load("spy.csv");
            //DataLoader.Load("msft.csv");

            //DataLoader.Load("DALcorrection.csv");


            HoldingsHelper.UpdateNewTransactions();

            InitializeComponent();
            ResetScreen();



            portfolio = HoldingsHelper.CurrentHoldings();
            foreach (KeyValuePair<int, Underlying> entry in portfolio)
            {
                Underlying u = entry.Value;
                Tiles.CreateTile(this, MainCanvas, (u.Cost > 0), u.TransactionGroup, u.Symbol, u.X, u.Y, u.Strategy, u.Cost.ToString("C"), (u.EarliestExpiration - DateTime.Today).TotalDays.ToString());
            }

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


            if ((selectedTag != 0) && detailsDirty) SaveChainDetails(selectedTag);

            foreach (ContentControl cc in MainCanvas.Children)
            {
                HoldingsHelper.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }



            App.CloseConnection();
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
                        if (selectedTag != 0 && tag != selectedTag && detailsDirty) SaveChainDetails(selectedTag);

                        Underlying u = portfolio[tag];
                        txtSymbol.Text = u.Symbol;

                        SetTextBox(txtExit, u.ExitStrategy, true);
                        SetTextBox(txtComments, u.Comments, true);
                        SetTextBox(txtCapital, u.CapitalRequired.ToString("C"), true);
                        SetTextBox(txtStartTime, u.StartTime.ToShortDateString(), false);
                        SetTextBox(txtEndTime, "", false);
                        if (u.StartTime != u.EndTime) SetTextBox(txtEndTime, u.EndTime.ToShortDateString(), false);

                        selectedTag = tag;
                    }


                }

            }
        }

        private void SetTextBox(TextBox tb, string txt, bool enable)
        {
            tb.Text = txt;
            tb.IsEnabled = enable;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (adornerLayer != null && tileAdorner != null) adornerLayer.Remove(tileAdorner);
            if (selectedTag != 0 && detailsDirty) SaveChainDetails(selectedTag);
            selectedTag = 0;

            txtSymbol.Text = "";

            SetTextBox(txtExit, "", false);
            SetTextBox(txtComments, "", false);
            SetTextBox(txtCapital, "", false);
            SetTextBox(txtStartTime, "", false);
            SetTextBox(txtEndTime, "", false);


        }


        private void SaveChainDetails(int tag)
        {
            Debug.WriteLine("Savinging... " + tag.ToString());
            Underlying u = new Underlying();
            u.TransactionGroup = tag;
            u.ExitStrategy = txtExit.Text;
            u.Comments = txtComments.Text;
            u.CapitalRequired = Convert.ToDecimal(txtCapital.Text.Replace("$", ""));


            HoldingsHelper.UpdateTransactionGroup(u);
            //refresh
            portfolio = HoldingsHelper.CurrentHoldings();

            detailsDirty = false;

        }

        private void FieldEntryEvent(object sender, KeyEventArgs e)
        {
            detailsDirty = true;

        }


        private void LoadButton(object sender, RoutedEventArgs e)
        {
            string filename = "";
            Debug.WriteLine("LoadButton...");
            OpenFileDialog opeFileDialog = new OpenFileDialog();
            if (opeFileDialog.ShowDialog() == true)
                filename = opeFileDialog.FileName;

            Debug.WriteLine("Load: " + filename);
        }
    }
}
