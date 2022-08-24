using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using System.Data;
using Microsoft.Win32;
using System.Web;


namespace OptionView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Portfolio portfolio;
        public Accounts accounts { get; set; }
        private int selectedTag = 0;
        private int combineRequestTag = 0;
        private bool detailsDirty = false;
        private bool uiDirty = false;




        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            accounts = new Accounts();

            UpdateHoldingsTiles();
            UpdateResultsGrid();
            UpdateTodosGrid();
            UpdateFooter();

            LoadDynamicComboBoxes();
            RestorePreviousSession();
        }

        private void InitializeApp()
        {
            this.Title += " - " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
        }

        string up = HttpUtility.HtmlDecode("&#x2BC5;");
        string down = HttpUtility.HtmlDecode("&#x2BC6;");

        private void UpdateFooter()
        {
            /*
                <StackPanel Orientation="Horizontal" >
                    <Label Content="Account" Width="80" Style="{StaticResource OverviewText}"/>
                    <Label Content="Individual" Width="100"  Style="{StaticResource OverviewText}"/>
                    <StackPanel Orientation="Horizontal" Width="100">
                        <Label Content="A"  Padding="0,4,0,4"/>
                        <Label Content="⏶" Foreground="Red"  Padding="0,4,0,4"/>
                        <Label Content="C" Foreground="Green" Padding="0,4,0,4"/>
                    </StackPanel>

                    <Label Content="Roth" Width="100"  Style="{StaticResource OverviewText}"/>
                </StackPanel>                
                <Border BorderThickness="0,0,0,1" BorderBrush="DarkGray" />

                <Setter Property="FontFamily" Value="Trebuchet MS" />
                <Setter Property="FontSize" Value="9" />
                <Setter Property="Foreground" Value="White" />
                <Setter Property="Padding" Value="1"/>


                <Label Content="VIX:" Width="100" FontSize="16" FontFamily="Trebuchet MS" Padding="2"/>
            */

            StackPanel[] sp = new StackPanel[] { new StackPanel(), new StackPanel(), new StackPanel() };
            for (int i = 0; i < 3; i++)
            {
                Label lb = OverviewLabel(100);

                switch (i)
                {
                    case 0:
                        lb.Content = "Account";
                        break;
                    case 1:
                        lb.Content = "Net Liq";
                        break;
                    case 2:
                        lb.Content = "Utilization";
                        break;
                }

                sp[i].Orientation = Orientation.Horizontal;
                sp[i].Children.Add(lb);
            }

            decimal combinedChange = 0;
            decimal combinedNetLiq = 0;
            decimal combinedBP = 0;
            foreach (Account a in accounts)
            {
                if (a.Active)
                {
                    decimal change = 0;
                    if (portfolio != null)
                    {
                        change = GetAccountChangeSincePrevious(a.ID);
                        combinedChange += change;
                    }

                    Label lb = OverviewLabel(100);
                    lb.Content = a.Name;
                    sp[0].Children.Add(lb);

                    StackPanel subpanel = new StackPanel() { Width = 100, Orientation = Orientation.Horizontal, Margin = new Thickness(0) };

                    TWBalance bal = TastyWorks.Balances(a.ID);
                    combinedNetLiq += bal.NetLiq;
                    combinedBP += bal.OptionBuyingPower;

                    lb = OverviewLabel(0);
                    lb.Content = bal.NetLiq.ToString("C0");

                    subpanel.Children.Add(lb);
                    lb = OverviewLabel(0);
                    lb.Content = (change > 0) ? up : down;
                    lb.Foreground = (change > 0) ? Brushes.Lime : Brushes.Red;
                    subpanel.Children.Add(lb);
                    lb = OverviewLabel(0);
                    lb.Content = change.ToString("#,###");
                    lb.Foreground = (change > 0) ? Brushes.Lime : Brushes.Red;
                    subpanel.Children.Add(lb);

                    sp[1].Children.Add(subpanel);

                    lb = OverviewLabel(100);
                    lb.Content = bal.CommittedPercentage.ToString("P1");
                    sp[2].Children.Add(lb);
                }
            }
            AppendTotals(sp, combinedChange, combinedNetLiq, combinedBP);
            
            Border b = new Border()
            {
                BorderThickness = new Thickness(0,0,0,1),
                BorderBrush = Brushes.DarkGray
            };

            OverviewPanel.Children.Add(sp[0]);
            OverviewPanel.Children.Add(b);
            OverviewPanel.Children.Add(sp[1]);
            OverviewPanel.Children.Add(sp[2]);

            // display VIX
            Decimal vix = Quotes.Get("^VIX");
            string vixText = String.Format("VIX: {0} - ", vix);
            if (vix <= 15) vixText += "25%";
            else if (vix <= 20) vixText += "30%";
            else if (vix <= 30) vixText += "35%";
            else if (vix <= 40) vixText += "40%";
            else vixText += "50%";
            vixText += " allocation";

            Label lb2 = OverviewLabel(0);
            lb2.FontSize = 16;
            lb2.Content = vixText;
            MetricsPanel.Children.Add(lb2);

        }
        private Label OverviewLabel(int width)
        {
            Label lb = new Label()
                {
                    FontSize = 11,
                    FontFamily = new FontFamily("Trebuchet MS"),
                    Foreground = Brushes.White,
                    Padding = new Thickness(2)
                };
            if (width > 0) lb.Width = width;
            return lb;
        }

        private decimal GetAccountChangeSincePrevious(string accountNumber)
        {
            decimal retval = 0;

            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;
                if (grp.Account == accountNumber)
                {
                    retval += grp.ChangeFromPreviousClose;
                    Debug.WriteLine("Symbol: {0}    {1}", grp.Symbol, grp.ChangeFromPreviousClose.ToString());
                }

            }

            Debug.WriteLine("Total: {0}", retval);
            return retval;
        }

        private void AppendTotals(StackPanel[] sp, decimal change, decimal netLiq, decimal bp)
        {
            Label lb = OverviewLabel(100);
            lb.Content = "Combined";
            sp[0].Children.Add(lb);

            StackPanel subpanel = new StackPanel() { Width = 100, Orientation = Orientation.Horizontal, Margin = new Thickness(0) };

            lb = OverviewLabel(0);
            lb.Content = netLiq.ToString("C0");

            subpanel.Children.Add(lb);
            lb = OverviewLabel(0);
            lb.Content = (change > 0) ? up : down;
            lb.Foreground = (change > 0) ? Brushes.Lime : Brushes.Red;
            subpanel.Children.Add(lb);
            lb = OverviewLabel(0);
            lb.Content = change.ToString("#,###");
            lb.Foreground = (change > 0) ? Brushes.Lime : Brushes.Red;
            subpanel.Children.Add(lb);

            sp[1].Children.Add(subpanel);

            lb = OverviewLabel(100);
            decimal committedPercentage = (netLiq - bp) / netLiq;
            lb.Content = committedPercentage.ToString("P1");
            sp[2].Children.Add(lb);

        }

        private void UpdateHoldingsTiles()
        {
            if (MainCanvas.Children.Count > 0) MainCanvas.Children.Clear();

            portfolio = new Portfolio();
            portfolio.GetCurrentHoldings(accounts);
            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;

                // massage cost to incude per lot value as well
                string cost = grp.Cost.ToString("C0") + grp.GetPerLotCost();

                Tiles.CreateTile(this, MainCanvas, Tiles.TileSize.Regular, (grp.CurrentValue + grp.Cost), grp.GroupID, grp.Symbol, grp.UnderlyingPrice.ToString("C2"), grp.AccountName, grp.X, grp.Y, grp.Strategy, cost, (grp.CurrentValue != 0) ? (grp.CurrentValue + grp.Cost).ToString("C0") : "",
                    (grp.EarliestExpiration == DateTime.MaxValue) ? "" : (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString(), 
                    grp.HasInTheMoneyPositions(), (grp.ActionDate > DateTime.MinValue), !grp.OrderActive, (grp.Cost > 0) ? "Prem" : "Cost", null, grp.ChangeFromPreviousClose.ToString("+#;-#"), 1.0);
            }
        }


        private void LoadDynamicComboBoxes()
        {
            // load account numbers
            foreach (Account a in accounts)
            {
                cbAccount.Items.Add(a.Name);
                cbAnalysisAccount.Items.Add(a.Name);
            }

            // load analysis views defined in code
            foreach (AnalysisViews v in viewList)
            {
                cbAnalysisView.Items.Add(v.Name);
            }
            cbAnalysisView.SelectedIndex = 0;
        }


        private void RestorePreviousSession()
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

            string[] filters = Config.GetProp("Filters").Split('|');
            if (filters.Length > 4)
            {
                Int32 fIdx = 0;
                //account
                Int32.TryParse(filters[0], out fIdx);
                cbAccount.SelectedIndex = fIdx;
                //date
                Int32.TryParse(filters[1], out fIdx);
                cbDateFilter.SelectedIndex = fIdx;

                if (filters[2] == "1") chkEarningsFilter.IsChecked = true;
                if (filters[3] == "1") chkNeutralFilter.IsChecked = true;
                if (filters[4] == "1") chkRiskFilter.IsChecked = true;
                if (filters[4] == "-1") chkRiskFilter.IsChecked = null;
            }

            string grouping = Config.GetProp("Grouping");
            Int32 idx = 0;
            Int32.TryParse(grouping, out idx);
            cbGrouping1.SelectedIndex = idx;

            string[] analysisView = Config.GetProp("AnalysisView").Split('|');
            if (analysisView.Length > 2)
            {
                chkOutliers.IsChecked = (analysisView[2] == "True");

                Int32 fIdx = 0;
                //account
                Int32.TryParse(analysisView[0], out fIdx);
                cbAnalysisView.SelectedIndex = fIdx;

                Int32.TryParse(analysisView[1], out fIdx);
                cbAnalysisAccount.SelectedIndex = fIdx;
            }

            // do last to avoid extraineous events
            string tab = Config.GetProp("Tab");
            if (tab.Length > 0)
            {
                MainTab.SelectedIndex = Convert.ToInt32(tab);
            }
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

            string filters = cbAccount.SelectedIndex.ToString() + "|";
            filters += cbDateFilter.SelectedIndex.ToString() + "|";
            filters += (chkEarningsFilter.IsChecked.HasValue && chkEarningsFilter.IsChecked.Value ? "1|" : "0|")
                       + (chkNeutralFilter.IsChecked.HasValue && chkNeutralFilter.IsChecked.Value ? "1|" : "0|");

            if (chkRiskFilter.IsChecked.HasValue)
                filters += (chkRiskFilter.IsChecked.Value ? "1" : "0");
            else
                filters += "-1";
            Config.SetProp("Filters", filters);

            Config.SetProp("Grouping", cbGrouping1.SelectedIndex.ToString());

            // save analysis view state
            Config.SetProp("AnalysisView", cbAnalysisView.SelectedIndex.ToString() + "|" + cbAnalysisAccount.SelectedIndex.ToString() + "|" + chkOutliers.IsChecked.ToString());

            if ((selectedTag != 0) && detailsDirty) SaveTransactionGroupDetails(selectedTag);

            foreach (ContentControl cc in MainCanvas.Children)
            {
                Tiles.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }



            App.CloseConnection();
        }

        private void TileTooltip(object sender, ToolTipEventArgs e)
        {
            if (sender.GetType() == typeof(Grid))
            {
                Grid grid = (Grid)sender;
                ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(grid);

                int grp = Convert.ToInt32(cc.Tag);
                if (grp != 0)
                {
                    string tt = portfolio[grp].GetHistory();
                    decimal price = portfolio[grp].TargetClosePrice();
                    if (price != 0)
                    {
                        string suf = "\nClose Price = ";
                        suf += Math.Abs(price).ToString("f2");
                        suf += (price < 0) ? " (Debit)" : " (Credit)";
                        tt += suf;
                    }
                    grid.ToolTip = tt;
                }
            }
        }


        private void TileDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Debug.WriteLine("Drag complete");
            if (sender.GetType() == typeof(MoveTile))
            {
                MoveTile tile = (MoveTile)sender;
                ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(tile.Parent);

                Tiles.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }
        }


        AdornerLayer adornerLayer = null;
        TileAdorner tileAdorner = null;
        private void TileMouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("TileMouseDown");
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
                    if ((cc != null) && (cc.Tag.GetType() == typeof(int))) tag = (int)cc.Tag;

                    if (tag > 0)
                    {
                        Debug.WriteLine("Group selected: " + tag.ToString());
                        if (selectedTag != 0 && detailsDirty)
                        {
                            Debug.WriteLine("Previous group {0} was dirty", selectedTag);
                            SaveTransactionGroupDetails(selectedTag);
                        }

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

                        detailsDirty = false;  //datepicker gets unavoidably dirty while initializing


                        string details = "";
                        foreach(KeyValuePair<string, Position> item in grp.Holdings)
                        {
                            Position p = item.Value;
                            if (p.Type == "Stock")
                                details += String.Format("{0,2} {1}", p.Quantity, p.Type) + System.Environment.NewLine;
                            else
                                details += String.Format("{0,2} {1} {2} {3:MMMd}", p.Quantity, p.Type.Substring(0, 1), p.Strike, p.ExpDate) + System.Environment.NewLine;
                        }
                        SetTextBox(txtDetails, details, true);

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
            Debug.WriteLine("CanvasMouseDown: " + selectedTag + ": " + detailsDirty.ToString());
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
            SetTextBox(txtDetails, "", false);
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

            grp.Update();
            portfolio.GetCurrentHoldings(accounts);  //refresh
            if (uiDirty)
            {
                grp = portfolio[tag];
                Tiles.UpdateTile(tag, MainCanvas, grp.Strategy, (grp.ActionDate > DateTime.MinValue));  // only fields that can be manually changed
            }

            detailsDirty = false;
            uiDirty = false;
        }

        private void FieldEntryEvent(object sender, KeyEventArgs e)
        {
            detailsDirty = true;

            if (((Control)sender).Name == "txtStrategy") uiDirty = true;
        }
        private void DateAction_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            detailsDirty = true;
            uiDirty = true;
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


        private void SyncButton(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("SyncButton...");

            Cursor prev = this.Cursor;
            this.Cursor = Cursors.Wait;

            DataLoader.Load(accounts);
            UpdateHoldingsTiles();
            UpdateResultsGrid();
            UpdateTodosGrid();

            if (MainTab.SelectedIndex == 1) UpdateAnalysisView();

            this.Cursor = prev;
        }

        private void ValidateButton(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("ValidateButton...");

            Cursor prev = this.Cursor;
            this.Cursor = Cursors.Wait;

            portfolio = new Portfolio();
            portfolio.GetCurrentHoldings(accounts);
            string response = portfolio.ValidateCurrentHoldings();

            this.Cursor = prev;

            if (response != "LoginFailed")
            {
                if (response.Length == 0) response = "Success!";
                MessageBox.Show(response, "Validate Results");
            }

            // refresh cached value - validate method zeros out holdings
            portfolio.GetCurrentHoldings(accounts);
        }

        private void ConfigButton(object sender, RoutedEventArgs e)
        {
            Window cfg = new Login();
            cfg.Show();
        }


        private void CombineClick(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Combine selected - tile id: " + combineRequestTag.ToString());

            if (TransactionGroup.Combine(selectedTag, combineRequestTag) == 1)
                UpdateHoldingsTiles();
        }

        private void ContextMenuValidationCheck(object sender, ContextMenuEventArgs e)
        {
            if (sender.GetType() == typeof(Grid))
            {
                Grid grid = (Grid)sender;
                var ctrl = VisualTreeHelper.GetParent(grid);
                if (ctrl.GetType() == typeof(ContentControl))
                {
                    ContentControl cc = (ContentControl)ctrl;

                    int tag = 0;
                    if ((cc != null) && (cc.Tag.GetType() == typeof(int))) tag = (int)cc.Tag;

                    if (tag > 0)
                    {
                        Debug.WriteLine("contextmenu tile id: " + tag.ToString());
                        combineRequestTag = tag;
                        // don't bother showing combine menu for any of these reasons
                        if ((combineRequestTag == selectedTag) || (portfolio[selectedTag].Symbol != portfolio[tag].Symbol) || (portfolio[selectedTag].Account != portfolio[tag].Account))
                        {
                            e.Handled = true;
                        }
                    }
                }
            }
        }


        // 
        //
        // code for results tab
        // 
        //

        private void UpdateResultsGrid()
        {
            PortfolioResults results = new PortfolioResults();
            results.GetResults();

            ListCollectionView lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(results);
            lcv.Filter = ResultsFilter;

            resultsGrid.ItemsSource = lcv;
        }

        private void FilterClick(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(resultsGrid.ItemsSource).Refresh();

            UpdateFilterStats();
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (resultsGrid.ItemsSource != null)   // grid not initialized yet
                FilterClick(null, new RoutedEventArgs());  
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

            string dateTag = ((ComboBoxItem)cbDateFilter.SelectedItem).Tag.ToString();
            string accountNumber = "";
            if (cbAccount.SelectedIndex != 0) accountNumber = accounts[cbAccount.SelectedIndex - 1].ID; 

            TransactionGroup t = (TransactionGroup)item;
            if (t != null)
            // If filter is turned on, filter completed items.
            {
                if ((accountNumber.Length > 0) && (t.Account != accountNumber))
                    ret = false;
                else if ((dateTag == "LastYear") && ((t.EndTime < new DateTime(DateTime.Now.Year-1, 1, 1)) || (t.EndTime >= new DateTime(DateTime.Now.Year, 1, 1))) )
                    ret = false;
                else if ((dateTag == "YTD") && (t.EndTime < new DateTime(DateTime.Now.Year, 1, 1)))
                    ret = false;
                else if ((dateTag == "90Days") && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(90)))
                    ret = false;
                else if ((dateTag == "30Days") && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(30)))
                    ret = false;
                else if (this.chkEarningsFilter.IsChecked == true && !t.EarningsTrade)
                    ret = false;
                else if (this.chkEarningsFilter.IsChecked == null && t.EarningsTrade)
                    ret = false;
                else if (this.chkNeutralFilter.IsChecked == true && !t.NeutralStrategy)
                    ret = false;
                else if (this.chkNeutralFilter.IsChecked == null && t.NeutralStrategy)
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
                    lcv.SortDescriptions.Add(new SortDescription("EndTime", ListSortDirection.Ascending));
                }
            }
        }

        // 
        //
        // code for Todo tab
        // 
        //

        private void UpdateTodosGrid()
        {
            PortfolioTodos todos = new PortfolioTodos();
            todos.GetTodos();

            todoGrid.ItemsSource = todos;
        }

        private void ResultsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            resultsGrid.Width = ((Grid)sender).ActualWidth - 156;
        }

        private void ResultsGrid_RowDetailsVisibilityChanged(object sender, DataGridRowDetailsEventArgs e)
        {
            if (((DataGridRowDetailsEventArgs)e).Row.DetailsVisibility == Visibility.Collapsed)
            {
                ((DataGrid)sender).SelectedIndex = -1;
            }
        }


        // 
        //
        // code for Analysis tab
        // 
        //

        private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                if (((TabControl)sender).SelectedIndex == 1) UpdateAnalysisView();
            }
        }

        private class AnalysisViews
        {
            public enum Format
            {
                Dollar,
                Number,
                Percent
            }

            public string Name;
            public string XLabel;
            public string YLabel;
            public string XAxisLabel;
            public string YAxisLabel;
            public Format XFormat;
            public Format YFormat;

            public AnalysisViews(string name, string xAxis, string yAxis, string x, string y, Format xFormat, Format yFormat)
            {
                this.Name = name;
                this.XLabel = x;
                this.YLabel = y;
                this.XAxisLabel = xAxis;
                this.YAxisLabel = yAxis;
                this.XFormat = xFormat;
                this.YFormat = yFormat;
            }
        }

        private AnalysisViews[] viewList = new AnalysisViews[]
        {
            new AnalysisViews("CapReq v Profit", "Profit", "Capital Requirement", "Prof", "CapReq", AnalysisViews.Format.Dollar, AnalysisViews.Format.Dollar),
            new AnalysisViews("CapReq v %Target Profit", "%Target Profit", "Capital Requirement", "%Targ", "CapReq", AnalysisViews.Format.Percent, AnalysisViews.Format.Dollar),
            new AnalysisViews("CapReq v Theta", "Theta", "Capital Requirement", "Theta", "CapReq", AnalysisViews.Format.Number, AnalysisViews.Format.Dollar),
            new AnalysisViews("CapReq v Theta Ratio", "Theta Ratio", "Capital Requirement", "Ratio", "CapReq", AnalysisViews.Format.Percent, AnalysisViews.Format.Dollar),
            new AnalysisViews("CapReq v Delta", "Delta", "Capital Requirement", "Delta", "CapReq", AnalysisViews.Format.Number, AnalysisViews.Format.Dollar)
        };
        

        private void UpdateAnalysisView()
        {
            Debug.Print("UpdateAnalysisView");
            if (AnalysisCanvas.Children.Count > 0) AnalysisCanvas.Children.Clear();

            int viewIndex = cbAnalysisView.SelectedIndex;
            if (viewIndex == -1) return;  // form not initialized yet

            double height = ((Grid)AnalysisCanvas.Parent).ActualHeight;
            double width = ((Grid)AnalysisCanvas.Parent).ActualWidth;
            double margin = 50;

            if (height == 0)
            {
                string scrnProps = Config.GetProp("Screen");
                string[] props = scrnProps.Split('|');
                height = Convert.ToDouble(props[4]) - 147;
                width = Convert.ToDouble(props[3]) - 22;
            }



            height -= (8 + 100 + (2 * margin));   //adjust for borders and tile height
            width -= (200 + 4 + 150 + (2 * margin));  //adjust for panel, borders and tile width

            if (height <= 0) return;
            decimal minX = 100000;
            decimal maxX = 0;
            decimal minY = 100000;
            decimal maxY = 0;
            bool secondTile = false;

            decimal horizontalOrigin = -1;

            //Rectangle rect = new Rectangle();
            //rect.Width = width + 150;
            //rect.Height = height + 100;
            //rect.Stroke = Brushes.LightGray;
            //rect.StrokeThickness = 1;
            //Canvas.SetLeft(rect, margin);
            //Canvas.SetTop(rect, margin);
            //AnalysisCanvas.Children.Add(rect);

            if (viewIndex == 0) secondTile = true;

            //adjust max/mins and origin
            switch (viewIndex)
            {
                case 0:
                case 4:
                    horizontalOrigin = 0;
                    break;
                case 1:
                    horizontalOrigin = 1;
                    maxX = 1;
                    break;
                case 2:
                    horizontalOrigin = 0;
                    maxX = 1;
                    break;
                case 3:
                    horizontalOrigin = 0.005M;  //threshold of good ration 0.5%
                    maxX = 0;
                    break;

                default:
                    return;
            }

            decimal overallXValue = 0;
            decimal overallYValue = 0;

            if (portfolio == null)
                Debug.Print("oops");

            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;

                if (FilterAnalysisTiles(grp, viewIndex))
                {
                    switch (viewIndex)
                    {
                        case 0:
                            grp.AnalysisXValue = grp.CurrentValue + grp.Cost;
                            grp.AnalysisYValue = grp.CapitalRequired;
                            grp.PreviousXValue = grp.PreviousCloseValue + grp.Cost; 
                            grp.PreviousYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;
                            break;
                        case 1:
                            grp.AnalysisXValue = grp.PercentOfTarget();
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;
                            break;
                        case 2:
                            grp.AnalysisXValue = grp.CalculateGroupTheta();
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;
                            break;
                        case 3:
                            grp.AnalysisXValue = grp.CalculateGroupTheta();
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;

                            grp.AnalysisXValue /= (grp.CapitalRequired < 100m) ? 100m : grp.CapitalRequired;
                            break;
                        case 4:
                            grp.AnalysisXValue = grp.CalculateGroupDelta();
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;
                            break;

                        default:
                            return;
                    }

                    if (grp.AnalysisXValue < minX) minX = grp.AnalysisXValue;
                    if (grp.AnalysisXValue > maxX) maxX = grp.AnalysisXValue;
                    if (grp.AnalysisYValue < minY) minY = grp.AnalysisYValue;
                    if (grp.AnalysisYValue > maxY) maxY = grp.AnalysisYValue;

                }
            }
            decimal scaleX = (maxX - minX) / (decimal)width;
            decimal scaleY = (maxY - minY) / (decimal)height;

            // just in case min = max, don't want to crash
            if (scaleX == 0) scaleX = 1;
            if (scaleY == 0) scaleY = 1;

            Debug.WriteLine("Width: {0}  Height: {1}", width, height);
            Debug.WriteLine("MinX: {0}  MaxX: {1}", minX, maxX);
            Debug.WriteLine("MinY: {0}  MaxY: {1}", minY, maxY);
            Debug.WriteLine("ScaleX: {0}  ScaleY: {1}", scaleX, scaleY);

            if (horizontalOrigin != -1)
            {
                Int32 x = Convert.ToInt32((decimal)margin + ((horizontalOrigin - minX) / scaleX) + 5);  //fudge it over 5 since the tiles have zero at left edge

                Line line = new Line();
                line.X1 = x;
                line.Y1 = margin;
                line.X2 = x;
                line.Y2 = height + margin + 80;
                line.Stroke = Brushes.DimGray;
                line.StrokeThickness = 1;
                AnalysisCanvas.Children.Add(line);

                // origin label
                TextBlock text1 = new TextBlock();
                text1.Text = FormatValue(horizontalOrigin, viewList[viewIndex].XFormat);
                text1.Foreground = Brushes.DimGray;
                text1.FontSize = 15;
                text1.TextAlignment = TextAlignment.Center;
                Canvas.SetLeft(text1, x - (text1.Text.Length * 4));
                Canvas.SetTop(text1, height + margin + 85);
                AnalysisCanvas.Children.Add(text1);
            }

            // horizontal axis label
            TextBlock text = new TextBlock();
            text.Text = viewList[viewIndex].XAxisLabel;
            text.Foreground = Brushes.DimGray;
            text.FontSize = 30;
            text.TextAlignment = TextAlignment.Right;
            Canvas.SetLeft(text, width + margin);
            Canvas.SetTop(text, height + margin + 100);
            AnalysisCanvas.Children.Add(text);

            // vertical axis label
            text = new TextBlock();
            text.Text = viewList[viewIndex].YAxisLabel;
            text.Foreground = Brushes.DimGray;
            text.FontSize = 30;
            text.LayoutTransform = new RotateTransform(-90);
            text.TextAlignment = TextAlignment.Right;
            Canvas.SetLeft(text, margin / 2);
            Canvas.SetTop(text, 2 * margin);
            AnalysisCanvas.Children.Add(text);

            // cycle thru the tiles
            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;

                if (FilterAnalysisTiles(grp, viewIndex))
                {
                    //Debug.WriteLine(grp.Symbol);

                    // massage cost to incude per lot value as well
                    string value1 = FormatValue(grp.AnalysisXValue, viewList[viewIndex].XFormat);
                    string value2 = FormatValue(grp.AnalysisYValue, viewList[viewIndex].YFormat);

                    int left = Convert.ToInt32((decimal)margin + (grp.AnalysisXValue - minX) / scaleX);
                    int top = Convert.ToInt32((decimal)margin + (decimal)height - ((grp.AnalysisYValue - minY) / scaleY));

                    //Debug.WriteLine("Value1: {0}  Value2: {1}", grp.AnalysisXValue, grp.AnalysisYValue);
                    //Debug.WriteLine("Left:   {0}  Top:    {1}", left, top);

                    if ((secondTile) && (grp.StartTime.Date < DateTime.Today))
                    {
                        int left2 = Convert.ToInt32((decimal)margin + (grp.PreviousXValue - minX) / scaleX);
                        string value1a = FormatValue(grp.PreviousXValue, viewList[viewIndex].XFormat);
                        Tiles.CreateTile(this, AnalysisCanvas, Tiles.TileSize.Small, grp.PreviousXValue, 0, grp.Symbol, "", grp.AccountName,
                            left2, top, grp.Strategy, value2, value1a,
                            (grp.EarliestExpiration == DateTime.MaxValue) ? "" : (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString(), false, false, false,
                            viewList[viewIndex].YLabel, viewList[viewIndex].XLabel, null, 0.2);
                    }

                    Tiles.CreateTile(this, AnalysisCanvas, Tiles.TileSize.Small, (grp.CurrentValue + grp.Cost), grp.GroupID, grp.Symbol, "", grp.AccountName, 
                        left, top, grp.Strategy, value2, value1,
                        (grp.EarliestExpiration == DateTime.MaxValue) ? "" : (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString(), false, false, false,
                        viewList[viewIndex].YLabel, viewList[viewIndex].XLabel, null, 1.0 );
                }
            }

            lbXVal.Content = "Overall " + viewList[viewIndex].XLabel;
            lbYVal.Content = "Overall " + viewList[viewIndex].YLabel;
            switch (viewIndex)
            {
                case 1:
                    txtXVal.Text = "";
                    txtYVal.Text = "";
                    break;
                case 3:  // ratio
                    txtXVal.Text = FormatValue(overallXValue / overallYValue, viewList[viewIndex].XFormat);
                    txtYVal.Text = FormatValue(overallYValue, viewList[viewIndex].YFormat);
                    break;
                default:
                    txtXVal.Text = FormatValue(overallXValue, viewList[viewIndex].XFormat);
                    txtYVal.Text = FormatValue(overallYValue, viewList[viewIndex].YFormat);
                    break;
            }
        }

        private string FormatValue(decimal value, AnalysisViews.Format f)
        {
            switch (f)
            {
                case AnalysisViews.Format.Dollar:
                    return value.ToString("C0");
                case AnalysisViews.Format.Number:
                    return value.ToString("N2");
                case AnalysisViews.Format.Percent:
                    return value.ToString("P1");
                default:
                    return value.ToString();
            }
        }

        private bool FilterAnalysisTiles(TransactionGroup grp, int view)
        {
            bool retval = false;
            
            string accountNumber = "";
            if (cbAnalysisAccount.SelectedIndex != 0) accountNumber = accounts[cbAnalysisAccount.SelectedIndex - 1].ID; 

            bool outliers = (bool)chkOutliers.IsChecked;

            if ((grp.Account == accountNumber) || (accountNumber.Length == 0))  //account filter
            {
                // outlier filter
                if ((outliers == true) ||   // flag on, show everything
                    (view < 2) ||           // ignore flag for first two view types
                    (((view == 2) || (view == 3)) && (grp.CalculateGroupTheta() > 0))  ||
                    ((view == 4) && (grp.CalculateGroupDelta() < 100))
                   )  
                    retval = true;
            }

            return retval;
        }

        private void CbAnalysis_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnalysisCanvas.Children.Count > 0) UpdateAnalysisView();

            int view = cbAnalysisView.SelectedIndex;
            if (chkOutliers != null) chkOutliers.IsEnabled = (view > 1);
        }

        private void Analysis_Click(object sender, RoutedEventArgs e)
        {
            UpdateAnalysisView();
        }







    }





    // 
    //
    // supporting classes for grouping headers
    // 
    //

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

            if ((mode == "ShortSymbol") || (mode == "Year"))
            {
                //IEnumerable<object> grp = (IEnumerable<object>)value;
                //if (grp == null)
                //    return "";

                return value.ToString();
            }
            else if (mode == "Account")
            {
                foreach (Account a in mw.accounts)
                {
                    if (a.ID == (string)value) return a.Name;
                }
                return "<empty>";
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

    public class PercentToBlank : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() == typeof(decimal))
            {
                decimal v = (decimal)value;
                if (v == 0)
                {
                    return "";
                }
                else
                {
                    return string.Format("{0:P1}", v);
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }



}




