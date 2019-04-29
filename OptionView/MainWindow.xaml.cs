﻿using System;
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
using System.Globalization;
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

            UpdateHoldingsTiles();
            UpdateResultsGrid();

            ResetScreen();
        }

        private void UpdateHoldingsTiles()
        {
            if (MainCanvas.Children.Count > 0) MainCanvas.Children.Clear();

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


            if (props.Length > 4)
            {
                if (props[0] == "1") this.WindowState = WindowState.Maximized;
                this.Left = Convert.ToDouble(props[1]);
                this.Top = Convert.ToDouble(props[2]);
                this.Width = Convert.ToDouble(props[3]);
                this.Height = Convert.ToDouble(props[4]);
            }

            string tab = Config.GetProp("Tab");
            if (tab.Length > 0)
            {
                MainTab.SelectedIndex = Convert.ToInt32(tab);
            }

            string[] filters = Config.GetProp("Filters").Split('|');
            if (filters.Length > 4)
            {
                if (filters[0] == "1") chkYearToDateFilter.IsChecked = true;
                if (filters[1] == "1") chkLast30DaysFilter.IsChecked = true;
                if (filters[2] == "1") chkEarningsFilter.IsChecked = true;
                if (filters[3] == "1") chkNeutralFilter.IsChecked = true;
                if (filters[4] == "1") chkRiskFilter.IsChecked = true;
                if (filters[4] == "-1") chkRiskFilter.IsChecked = null;
            }

            string grouping = Config.GetProp("Grouping");
            Int32 idx = 0;
            Int32.TryParse(grouping, out idx);
            cbGrouping1.SelectedIndex = idx;


        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //correct style mismatches
            txtSymbol.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));
            DateAction_IsEnabledChanged(dateAction, new DependencyPropertyChangedEventArgs());

        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("window closing");

            string scrnProps = ((this.WindowState == WindowState.Maximized) ? "1|" : "0|") + this.Left.ToString() + "|" + this.Top.ToString() + "|" + this.Width.ToString() + "|" + this.Height.ToString();
            Config.SetProp("Screen", scrnProps);
            Config.SetProp("Tab", MainTab.SelectedIndex.ToString());

            string filters = (chkYearToDateFilter.IsChecked.HasValue && chkYearToDateFilter.IsChecked.Value ? "1|" : "0|")
                            + (chkLast30DaysFilter.IsChecked.HasValue && chkLast30DaysFilter.IsChecked.Value ? "1|" : "0|")
                            + (chkEarningsFilter.IsChecked.HasValue && chkEarningsFilter.IsChecked.Value ? "1|" : "0|")
                            + (chkNeutralFilter.IsChecked.HasValue && chkNeutralFilter.IsChecked.Value ? "1|" : "0|");

            if (chkRiskFilter.IsChecked.HasValue)
                filters += (chkRiskFilter.IsChecked.Value ? "1" : "0");
            else
                filters += "-1";
            Config.SetProp("Filters", filters);

            Config.SetProp("Grouping", cbGrouping1.SelectedIndex.ToString());


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

                        SetTextBlock(txtSymbol, grp.Symbol, true);
                        SetTextBox(txtStrategy, grp.Strategy, true);
                        SetTextBox(txtExit, grp.ExitStrategy, true);
                        SetDatePicker(dateAction, grp.ActionDate, true);
                        SetTextBox(txtComments, grp.Comments, true);
                        SetTextBox(txtCapital, grp.CapitalRequired.ToString("C0"), true);
                        SetCheckBox(chkEarnings, grp.EarningsTrade, true);
                        SetCheckBox(chkNeutral, grp.NeutralStrategy, true);
                        SetCheckBox(chkDefinedRisk, grp.DefinedRisk, true);
                        if (grp.DefinedRisk)
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
        private void SetTextBlock(TextBlock tb, string txt, bool enable)
        {
            tb.Text = txt;
            tb.IsEnabled = enable;
            if (enable)
                tb.Background = Brushes.White;
            else
                tb.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));
        }
        private void SetCheckBox(CheckBox cb, bool val, bool enable)
        {
            cb.IsChecked = val;
            cb.IsEnabled = enable;
        }
        private void SetDatePicker(DatePicker dp, DateTime dt, bool enable)
        {
            if (dt > DateTime.MinValue)
            {
                dp.SelectedDate = dt;
            }
            else
            {
                dp.SelectedDate = null;
            }
            dp.IsEnabled = enable;
            SetDatePickerForeground(dp);

        }
        private void SetDatePickerForeground(DatePicker dp)
        {
            if (dp.SelectedDate.HasValue)
                dp.Foreground = Brushes.Black;
            else
                dp.Foreground = Brushes.White;

        }
        private void DateAction_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = (DatePicker)sender;

            if (dp.IsEnabled)
                dp.Background = Brushes.White;
            else
                dp.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));

            Grid grid = UIHelper.FindChild<Grid>(dp, "PART_DisabledVisual");
            if (grid != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
                {
                    var child = VisualTreeHelper.GetChild(grid, i);
                    if (child.GetType() == typeof(Rectangle))
                    {
                        if (dp.IsEnabled)
                            ((Rectangle)child).Fill = Brushes.White;
                        else
                            ((Rectangle)child).Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));

                        break;
                    }
                }
            }
        }
        private void DateAction_CalendarClosed(object sender, RoutedEventArgs e)
        {
            DatePicker dp = (DatePicker)sender;
            SetDatePickerForeground(dp);

            //clear out selected text
            if (Keyboard.PrimaryDevice != null)
            {
                if (Keyboard.PrimaryDevice.ActiveSource != null)
                {
                    var e1 = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Down) { RoutedEvent = Keyboard.KeyDownEvent };
                    InputManager.Current.ProcessInput(e1);
                }
            }
        }



        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (adornerLayer != null && tileAdorner != null) adornerLayer.Remove(tileAdorner);
            if (selectedTag != 0 && detailsDirty) SaveTransactionGroupDetails(selectedTag);
            selectedTag = 0;

            txtSymbol.Text = "";

            SetTextBlock(txtSymbol, "", false);
            SetTextBox(txtStrategy, "", false);
            SetTextBox(txtExit, "", false);
            SetDatePicker(dateAction, DateTime.MinValue, false);
            SetTextBox(txtComments, "", false);
            SetTextBox(txtCapital, "", false);
            SetCheckBox(chkEarnings, false, false);
            SetCheckBox(chkNeutral, false, false);
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
            grp.ActionDate = (dateAction.SelectedDate.HasValue && (dateAction.Text !="")) ? dateAction.SelectedDate.Value : DateTime.MinValue;
            grp.Comments = txtComments.Text;
            Decimal retval = 0;
            if (Decimal.TryParse(txtCapital.Text.Replace("$", ""), out retval)) grp.CapitalRequired = retval;
            grp.EarningsTrade = chkEarnings.IsChecked.HasValue ? chkEarnings.IsChecked.Value : false;
            grp.NeutralStrategy = chkNeutral.IsChecked.HasValue ? chkNeutral.IsChecked.Value : false;

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
        private void DateAction_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
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
                    if (cb.IsChecked.HasValue ? cb.IsChecked.Value : false)
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
                UpdateResultsGrid();

            }
        }



        private void UpdateResultsGrid()
        {
            PortfolioResults results = new PortfolioResults();
            results.GetResults();

            ListCollectionView lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(results);
            lcv.Filter = ResultsFilter;

            resultsGrid.ItemsSource = lcv;

            UpdateFilterStats();
        }

        private void FilterClick(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(resultsGrid.ItemsSource).Refresh();

            UpdateFilterStats();
        }

        private void UpdateFilterStats()
        {
            ListCollectionView lcv = (ListCollectionView)resultsGrid.ItemsSource;
            decimal profit = 0m;
            decimal fees = 0m;
            foreach (TransactionGroup tg in lcv)
            {
                profit += tg.Cost;
                fees += tg.Fees;
            }

            txtProfit.Text = profit.ToString("C0");
            txtCount.Text = lcv.Count.ToString();
            txtFees.Text = fees.ToString("C0");
            txtNet.Text = (profit - fees).ToString("C");

        }
        private bool ResultsFilter(object item)
        {
            bool ret = false;

            TransactionGroup t = (TransactionGroup)item;
            if (t != null)
            // If filter is turned on, filter completed items.
            {
                if (this.chkYearToDateFilter.IsChecked == true && t.StartTime.Year != DateTime.Now.Year)
                    ret = false;
                else if (this.chkLast90DaysFilter.IsChecked == true && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(90)))
                    ret = false;
                else if (this.chkLast30DaysFilter.IsChecked == true && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(30)))
                    ret = false;
                else if (this.chkEarningsFilter.IsChecked == true && !t.EarningsTrade)
                    ret = false;
                else if (this.chkNeutralFilter.IsChecked == true && !t.NeutralStrategy)
                    ret = false;
                else if (this.chkRiskFilter.IsChecked == true && !t.DefinedRisk)
                    ret = false;
                else if (this.chkRiskFilter.IsChecked == null && t.DefinedRisk)  // null = "not" defined risk
                    ret = false;
                else
                {
                    ret = true;
                }
            }
            return ret;
        }


        private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            if (row != null)
            {
                if (row.DetailsVisibility == Visibility.Visible)
                    row.DetailsVisibility = Visibility.Collapsed;
                else
                    row.DetailsVisibility = Visibility.Visible;
            }

        }

        private void CbGrouping1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            ListCollectionView lcv = (ListCollectionView)resultsGrid.ItemsSource;

            if (lcv != null)
            {
                lcv.GroupDescriptions.Clear();
                lcv.SortDescriptions.Clear();

                string grpName = ((ComboBoxItem)cb.SelectedItem).Tag.ToString();

                if (grpName != "None")
                {
                    lcv.GroupDescriptions.Add(new PropertyGroupDescription(grpName));
                    lcv.SortDescriptions.Add(new SortDescription(grpName, ListSortDirection.Ascending));
                    lcv.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Ascending));
                }
            }
        }

    }


    public class GroupTotalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IEnumerable<object> grp = (IEnumerable<object>)value;
            if (grp == null)
                return "";

            decimal total = 0;
            int count = 0;
            int winners = 0;

            foreach (TransactionGroup tg in grp)
            {
                total += tg.Cost;
                count += 1;
                if (tg.Cost > 0) winners += 1;
            }
            return string.Format("{0:C0}  {1:n1}% winners out of {2:n0} positions", total, (decimal)winners * 100m / (decimal)count, count);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class GroupNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MainWindow mw = (MainWindow)Application.Current.Windows[0];

            string mode = ((ComboBoxItem)mw.cbGrouping1.SelectedItem).Tag.ToString();

            if (mode == "Symbol")
            {
                //IEnumerable<object> grp = (IEnumerable<object>)value;
                //if (grp == null)
                //    return "";

                return value.ToString();
            }
            else if (mode == "EarningsTrade")
            {
                return ((bool)value ? "Earnings" : "Regular");
            }
            else if (mode == "NeutralStrategy")
            {
                return ((bool)value ? "Neutral" : "Biased");
            }

            return "blah";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

}




