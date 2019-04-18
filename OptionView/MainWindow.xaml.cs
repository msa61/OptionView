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
         
            InitializeComponent();
            ResetScreen();

            UpdateHoldingsTiles();
            UpdateResultsGrid();
           
        }

        private void UpdateHoldingsTiles()
        {
            if (MainCanvas.Children.Count > 0)  MainCanvas.Children.Clear();

            portfolio = new Portfolio();
            portfolio.GetCurrentHoldings();
            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;
                Tiles.CreateTile(this, MainCanvas, (grp.Cost > 0), grp.GroupID, grp.Symbol, grp.X, grp.Y, grp.Strategy, grp.Cost.ToString("C"), (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString());
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

            string tab = Config.GetProp("Tab");
            if (tab.Length > 0)
            {
                MainTab.SelectedIndex = Convert.ToInt32(tab);
            }
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("window closing");

            string scrnProps = ((this.WindowState == WindowState.Maximized) ? "1|" : "0|") + this.Left.ToString() + "|" + this.Top.ToString() + "|" + this.Width.ToString() + "|" + this.Height.ToString();
            Config.SetProp("Screen", scrnProps);
            Config.SetProp("Tab", MainTab.SelectedIndex.ToString());


            if ((selectedTag != 0) && detailsDirty) SaveTransactionGroupDetails(selectedTag);

            foreach (ContentControl cc in MainCanvas.Children)
            {
                Tiles.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
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
                        if (selectedTag != 0 && tag != selectedTag && detailsDirty) SaveTransactionGroupDetails(selectedTag);

                        TransactionGroup grp = portfolio[tag];
                        txtSymbol.Text = grp.Symbol;

                        SetTextBox(txtStrategy, grp.Strategy, true);
                        SetTextBox(txtExit, grp.ExitStrategy, true);
                        SetTextBox(txtComments, grp.Comments, true);
                        SetTextBox(txtCapital, grp.CapitalRequired.ToString("C0"), true);
                        SetCheckBox(chkEarnings, grp.EarningsTrade, true);
                        SetCheckBox(chkDefinedRisk, grp.DefinedRisk, true);
                        if (grp.DefinedRisk )
                            SetTextBox(txtRisk, grp.Risk.ToString("C0"), true);
                        else
                            SetTextBox(txtRisk, "", false);

                        SetTextBox(txtStartTime, grp.StartTime.ToShortDateString(), false);
                        SetTextBox(txtEndTime, "", false);
                        if (grp.StartTime != grp.EndTime) SetTextBox(txtEndTime, grp.EndTime.ToShortDateString(), false);

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
        private void SetCheckBox(CheckBox cb, bool val, bool enable)
        {
            cb.IsChecked = val;
            cb.IsEnabled = enable;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (adornerLayer != null && tileAdorner != null) adornerLayer.Remove(tileAdorner);
            if (selectedTag != 0 && detailsDirty) SaveTransactionGroupDetails(selectedTag);
            selectedTag = 0;

            txtSymbol.Text = "";

            SetTextBox(txtStrategy, "", false);
            SetTextBox(txtExit, "", false);
            SetTextBox(txtComments, "", false);
            SetTextBox(txtCapital, "", false);
            SetCheckBox(chkEarnings, false, false);
            SetCheckBox(chkDefinedRisk, false, false);
            SetTextBox(txtRisk, "", false);
            SetTextBox(txtStartTime, "", false);
            SetTextBox(txtEndTime, "", false);
        }


        private void SaveTransactionGroupDetails(int tag)
        {
            Debug.WriteLine("Saving... " + tag.ToString());
            TransactionGroup grp = new TransactionGroup();
            grp.GroupID = tag;
            grp.Strategy = txtStrategy.Text;
            grp.ExitStrategy = txtExit.Text;
            grp.Comments = txtComments.Text;
            Decimal retval = 0;
            if (Decimal.TryParse(txtCapital.Text.Replace("$", ""), out retval)) grp.CapitalRequired = retval;
            grp.EarningsTrade = chkEarnings.IsChecked.HasValue ? chkEarnings.IsChecked.Value : false;

            grp.DefinedRisk = chkDefinedRisk.IsChecked.HasValue ? chkDefinedRisk.IsChecked.Value : false;
            retval = 0;
            if (Decimal.TryParse(txtRisk.Text.Replace("$", ""), out retval)) grp.Risk = retval;

            grp.UpdateTransactionGroup();
            portfolio.GetCurrentHoldings();  //refresh

            detailsDirty = false;
        }

        private void FieldEntryEvent(object sender, KeyEventArgs e)
        {
            detailsDirty = true;
        }
        private void CheckBoxMouseEvent(object sender, MouseButtonEventArgs e)
        {
            detailsDirty = true;

            if (sender.GetType() == typeof(CheckBox))
            {
                CheckBox cb = (CheckBox)sender;
                if (cb.Name == "chkDefinedRisk")
                {
                    if (cb.IsChecked.HasValue ? cb.IsChecked.Value : false )
                    {
                        SetTextBox(txtRisk, "", false);
                    }
                    else
                    {
                        SetTextBox(txtRisk, "$0", true);
                    }
                }
            }
            
        }


        private void LoadButton(object sender, RoutedEventArgs e)
        {
            string filename = "";
            Debug.WriteLine("LoadButton...");
            OpenFileDialog opeFileDialog = new OpenFileDialog();
            if (opeFileDialog.ShowDialog() == true)
                filename = opeFileDialog.FileName;

            if (filename.Length > 0)
            {
                Debug.WriteLine("Load: " + filename);
                DataLoader.Load(filename);
                UpdateHoldingsTiles();
            }
        }



        private void UpdateResultsGrid()
        {



            PortfolioResults results = new PortfolioResults();
            results.GetResults();

            resultsGrid.ItemsSource = results;

        }

    }
}
