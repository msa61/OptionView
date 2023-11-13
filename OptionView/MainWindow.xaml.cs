using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Timers;

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
        private bool todoDirty = false;
        private bool initializingDatePicker = false;
        private Timer refreshTimer = null;


        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            accounts = new Accounts();

            RestorePreviousSession();
        }

        private void InitializeApp()
        {
            this.Title += " - " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // do quick ones before launching thread
            portfolio = new Portfolio(accounts);
            DisplayTilesSafe();
            UpdateResultsGrid();
            UpdateTransactionsGrid();
            UpdateTodosGrid();

            App.InitializeStatusMessagePanel(14);

            // launch the long running tasks
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += InitializeDataAsync;
            worker.RunWorkerCompleted += InitializeDataComplete;

            worker.RunWorkerAsync();

            // setup timer
            refreshTimer = new Timer();
            refreshTimer.Interval = 5 * 60 * 1000;
            refreshTimer.AutoReset= true;
            refreshTimer.Elapsed += TimedRefresh;
            refreshTimer.Start();
        }

        private void InitializeDataAsync(object sender, DoWorkEventArgs e)
        {
            portfolio.GetCurrentData(accounts);
            GetAccountData();
            UpdateFooterSafe();

            LoadDynamicComboBoxesAsync();

            this.Dispatcher.Invoke(() =>
            {
                // notifies ui that progressbar can go away ** before ALL of the async work is done
                AsyncLoadComplete();
            });

            // wait to do this completely in the background
            UpdateScreenerGrid();
        }

        private void InitializeDataComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            refreshActive = false;
        }

        private void AsyncLoadComplete()
        {
            if (App.DataRefreshMode) System.Windows.Application.Current.Shutdown();
            DisplayTilesSafe();
            App.HideStatusMessagePanel();
        }


        private void UpdateFooterSafe()
        {
            if (this.Dispatcher.CheckAccess())
            {
                // same thread
                UpdateFooter();
            }
            else
            {
                // async load
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateFooter();
                });
            }
        }

        private void GetAccountData()
        {
            foreach (Account a in accounts)
            {
                if (a.Active)
                {
                    TWBalance bal = TastyWorks.Balances(a.ID);
                    a.NetLiq = bal.NetLiq;
                    a.OptionBuyingPower = bal.OptionBuyingPower;

                    BalanceHistory.Write(a.Name, a.NetLiq, portfolio.GetAccountCapRequired(a.ID, false));
                }
            }
            BalanceHistory.TimeStamp();
        }


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
            try
            {
                App.UpdateStatusMessage("Updating footer");

                OverviewPanel.Children.Clear();

                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                OverviewLabel(sp, "Account", 80);
                OverviewLabel(sp, "Net Liq", 100);
                OverviewLabel(sp, "Req Cap", 70);
                OverviewLabel(sp, "Allocation", 80);
                OverviewLabel(sp, "Β-Delta", 60);
                OverviewLabel(sp, "Theta", 60);

                OverviewPanel.Children.Add(sp);

                OverviewPanel.Children.Add(new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.DarkGray });

                decimal combinedChange = 0;
                decimal combinedNetLiq = 0;
                decimal combinedBP = 0;
                decimal capReq;
                decimal capReqAll;
                foreach (Account a in accounts)
                {
                    if (a.Active)
                    {
                        sp = new StackPanel { Orientation = Orientation.Horizontal };
                        decimal change = 0;
                        if (portfolio != null)
                        {
                            change = GetAccountChangeSincePrevious(a.ID);
                            combinedChange += change;
                        }

                        Label lb = OverviewLabel(sp, a.Name, 80);

                        // 2nd column
                        combinedNetLiq += a.NetLiq;
                        combinedBP += a.OptionBuyingPower;

                        DecoratedValueLabel(sp, a.NetLiq, change);

                        // 3rd column
                        capReq = portfolio.GetAccountCapRequired(a.ID, false);
                        capReqAll = portfolio.GetAccountCapRequired(a.ID, true);  // includes stock
                        OverviewLabel(sp, capReq.ToString("C0"), 70, toolTip: ("With Stock: " + capReqAll.ToString("C0")));

                        // 4th column
                        decimal acctCommittedPercentage = (a.NetLiq == 0) ? 0 : capReq / a.NetLiq;
                        decimal acctCommittedPercentageAll = (a.NetLiq == 0) ? 0 : capReqAll / a.NetLiq;
                        OverviewLabel(sp, acctCommittedPercentage.ToString("P1"), 80, toolTip: ("With Stock: " + acctCommittedPercentageAll.ToString("P1")));

                        // 5th column
                        OverviewLabel(sp, portfolio.GetWeightedDelta(a.ID).ToString("N0"), 60);

                        // 6th column
                        OverviewLabel(sp, portfolio.GetTheta(a.ID).ToString("C0"), 60);

                        // 7th column
                        LineGraph lg = new LineGraph(BalanceHistory.Get(a.Name));
                        List<decimal> minMax = BalanceHistory.YTDMinMax(a.Name);
                        lg.ToolTip = String.Format("YTD Min: {0:C2}\nYTD Max: {1:C2}", minMax[0], minMax[1]);
                        sp.Children.Add(lg);

                        // 8th column
                        sp.Children.Add(new WinLossGraph(BalanceHistory.GetChange(a.Name)));

                        OverviewPanel.Children.Add(sp);
                    }
                }

                OverviewPanel.Children.Add(new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.DarkGray });

                // totals row
                sp = new StackPanel { Orientation = Orientation.Horizontal };
                OverviewLabel(sp, "Overall", 80);

                // 2nd column
                DecoratedValueLabel(sp, combinedNetLiq, combinedChange);

                // 3rd column
                capReq = portfolio.GetAccountCapRequired();
                capReqAll = portfolio.GetAccountCapRequired(incStock: true);
                OverviewLabel(sp, capReq.ToString("C0"), 70, toolTip: ("With Stock: " + capReqAll.ToString("C0")));

                // 4rd column
                decimal committedPercentage = (combinedNetLiq == 0) ? 0 : capReq / combinedNetLiq;
                decimal CommittedPercentageAll = (combinedNetLiq == 0) ? 0 : capReqAll / combinedNetLiq;
                OverviewLabel(sp, committedPercentage.ToString("P1"), 80, toolTip: ("With Stock: " + CommittedPercentageAll.ToString("P1")));

                // 5th column
                OverviewLabel(sp, portfolio.GetWeightedDelta().ToString("N0"), 60);

                // 6th column
                OverviewLabel(sp, portfolio.GetTheta().ToString("C0"), 60);

                // 7th column
                sp.Children.Add(new LineGraph(BalanceHistory.Get(accounts)));

                // 8th column
                sp.Children.Add(new WinLossGraph(BalanceHistory.GetChange(accounts)));

                OverviewPanel.Children.Add(sp);

                // now work on the right side of the footer
                if (portfolio.SPY != null) DecoratedFooterLabel(SPYFooterText, portfolio.SPY.Price, portfolio.SPY.Change, true);

                if (portfolio.VIX != null)
                {
                    decimal vix = portfolio.VIX.Price;
                    string vixText;
                    if (vix <= 15) vixText = "25%";
                    else if (vix <= 20) vixText = "30%";
                    else if (vix <= 30) vixText = "35%";
                    else if (vix <= 40) vixText = "40%";
                    else vixText = "50%";
                    vixText += " allocation";
                    DecoratedFooterLabel(VIXFooterText, portfolio.VIX.Price, portfolio.VIX.Change, false, vixText);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "UpdateFooter Error");
            }
        }


        private Label OverviewLabel(StackPanel sp, string txt, int width = 0, int fontSize = 11, string toolTip = "")
        {
            Label lb = new Label()
            {
                Content = txt,
                FontSize = fontSize,
                FontFamily = new FontFamily("Trebuchet MS"),
                Foreground = Brushes.White,
                Padding = new Thickness(2),
                VerticalAlignment= VerticalAlignment.Center
            };
            if (width > 0) lb.Width = width;
            if (sp != null) sp.Children.Add(lb);
            if (toolTip.Length > 0) lb.ToolTip= toolTip;
            return lb;
        }

        private void DecoratedValueLabel(StackPanel sp, decimal val, decimal change, int fontSize = 11)
        {
            string up = HttpUtility.HtmlDecode("&#x2BC5;");
            string down = HttpUtility.HtmlDecode("&#x2BC6;");

            StackPanel subpanel = new StackPanel() { Width = 100, Orientation = Orientation.Horizontal, Margin = new Thickness(0) };

            OverviewLabel(subpanel, val.ToString("C0"), 0, fontSize);
            Label lb = OverviewLabel(subpanel, (change > 0) ? up : down);
            lb.Foreground = (change > 0) ? Brushes.PaleGreen : Brushes.LightCoral;
            lb = OverviewLabel(subpanel, change.ToString("#,###"), 0, fontSize);
            lb.Foreground = (change > 0) ? Brushes.PaleGreen : Brushes.LightCoral;

            sp.Children.Add(subpanel);
        }
        private void DecoratedFooterLabel(StackPanel sp, decimal val, decimal change, bool currency, string suffix = "",  int fontSize = 16)
        {
            // need to clear, or cell accumulates
            sp.Children.Clear();

            string up = HttpUtility.HtmlDecode("&#x2BC5;");
            string down = HttpUtility.HtmlDecode("&#x2BC6;");

            StackPanel subpanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0) };

            OverviewLabel(subpanel, val.ToString(currency ? "C2" : "N"), 64, fontSize);
            Label lb = OverviewLabel(subpanel, (change > 0) ? up : down, 16, fontSize - 4);
            lb.Foreground = (change > 0) ? Brushes.PaleGreen : Brushes.LightCoral;
            lb = OverviewLabel(subpanel, Math.Abs(change).ToString(currency ? "C2" : "N"), 0, fontSize - 4);
            lb.Foreground = (change > 0) ? Brushes.PaleGreen : Brushes.LightCoral;
            decimal percent = change / val;
            lb = OverviewLabel(subpanel, "(" + Math.Abs(percent).ToString("P1") + ")", 0, fontSize - 4);
            lb.Foreground = (change > 0) ? Brushes.PaleGreen : Brushes.LightCoral;
            if (suffix.Length > 0) OverviewLabel(subpanel, " ➜ " + suffix, 0, fontSize);

            sp.Children.Add(subpanel);
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

        private bool refreshActive = true;  // defaults to on until screener is finished
        private void OverviewPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!refreshActive) RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            refreshActive = true;
            //App.InitializeStatusMessagePanel(4);

            // launch the long running tasks
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += MinorRefreshAsync;
            worker.RunWorkerCompleted += MinorLoadComplete;

            worker.RunWorkerAsync();
        }

        private void MinorRefreshAsync(object sender, DoWorkEventArgs e)
        {
            this.Dispatcher.Invoke(() => { this.refreshModeSignal.Visibility = Visibility.Visible; });  // for temporary diagnostics

            portfolio.GetCurrentData(accounts);

            this.Dispatcher.Invoke(() => { this.refreshModeSignal.Visibility = Visibility.Collapsed; });
        }
        private void MinorLoadComplete(object sender, RunWorkerCompletedEventArgs e) 
        {
            RefreshTilesSafe();
            UpdateFooterSafe();
            BalanceHistory.WriteGroups(portfolio);
            //App.HideStatusMessagePanel();
            refreshActive = false;
        }

        private void RefreshTiles()
        {
            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;

                Tiles.UpdateTile(grp.GroupID, MainCanvas, (grp.CurrentValue + grp.Cost), grp.UnderlyingPrice, grp.UnderlyingPriceChange,
                    ((grp.CurrentValue ?? 0) != 0) ? "P/L: " + ((decimal)grp.CurrentValue + grp.Cost).ToString("C0") : "",
                    grp.ChangeFromPreviousClose.ToString("+#;-#;nc"),
                    grp.Strategy, (grp.ActionDate > DateTime.MinValue), !grp.OrderActive, grp.HasInTheMoneyPositions());
            }
        }

        private void RefreshTilesSafe()
        {
            if (this.Dispatcher.CheckAccess())
            {
                // same thread
                RefreshTiles();
            }
            else
            {
                // async load
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RefreshTiles();
                });
            }
        }

        private void TimedRefresh(object sender, ElapsedEventArgs e)
        {
            RefreshDisplay();
        }



        private void UpdateHoldingsTiles()
        {
            App.UpdateStatusMessage("Update tiles");

            portfolio = new Portfolio(accounts);
            BalanceHistory.WriteGroups(portfolio); /// TO DO

            DisplayTilesSafe();
        }

        private void DisplayTilesSafe()
        {
            if (this.Dispatcher.CheckAccess())
            {
                // same thread
                DisplayTiles();
            }
            else
            {
                // async load
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DisplayTiles();
                });
            }
        }

        // enable display refresh with in-memory data
        private void DisplayTiles()
        {
            if (MainCanvas.Children.Count > 0) MainCanvas.Children.Clear();
            ClearTransactionGroupDetails();

            foreach (KeyValuePair<int, TransactionGroup> entry in portfolio)
            {
                TransactionGroup grp = entry.Value;

                // massage cost to incude per lot value as well
                string cost = grp.Cost.ToString("C0") + grp.GetPerLotCost();

                Tiles.CreateTile(this, MainCanvas, Tiles.TileSize.Regular, (grp.CurrentValue + grp.Cost), grp.GroupID, grp.Symbol, grp.UnderlyingPrice, grp.UnderlyingPriceChange, grp.AccountName, grp.X, grp.Y, grp.Strategy, cost, ((grp.CurrentValue ?? 0) != 0) ? ((decimal)grp.CurrentValue + grp.Cost).ToString("C0") : "",
                    (grp.EarliestExpiration == DateTime.MaxValue) ? "" : (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString(),
                    grp.HasInTheMoneyPositions(), (grp.ActionDate > DateTime.MinValue), !grp.OrderActive, (grp.Cost > 0) ? "Prem" : "Cost", null, grp.ChangeFromPreviousClose.ToString("+#;-#;nc"), 1.0);
            }
        }

        private void LoadDynamicComboBoxesAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadDynamicComboBoxes();
            });
        }

        private void LoadDynamicComboBoxes()
        {
            // load account numbers
            foreach (Account a in accounts)
            {
                cbAccount.Items.Add(a.Name);
                cbAnalysisAccount.Items.Add(a.Name);
                cbtAccount.Items.Add(a.Name);
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
                //account
                Int32.TryParse(filters[0], out Int32 fIdx);
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
            Int32.TryParse(grouping, out Int32 idx);
            cbResultsGrouping.SelectedIndex = idx;

            string[] analysisView = Config.GetProp("AnalysisView").Split('|');
            if (analysisView.Length > 2)
            {
                chkOutliers.IsChecked = (analysisView[2] == "True");

                //account
                Int32.TryParse(analysisView[0], out Int32 fIdx);
                cbAnalysisView.SelectedIndex = fIdx;

                Int32.TryParse(analysisView[1], out fIdx);
                cbAnalysisAccount.SelectedIndex = fIdx;
            }

            UpdateScreenerFields();

            // do last to avoid extraineous events
            string tab = Config.GetProp("Tab");
            if (tab.Length > 0)
            {
                MainTab.SelectedIndex = Convert.ToInt32(tab);
            }

            // handle group window location
            string[] grpWindow = Config.GetProp("GroupWindow").Split('|');
            if (grpWindow.Length > 1)
            {
                App.GroupWindow.Left = Convert.ToDouble(grpWindow[0]);
                App.GroupWindow.Top = Convert.ToDouble(grpWindow[1]);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //correct style mismatches
            txtSymbol.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));
            txtSymbolTodo.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD));
            DateAction_IsEnabledChanged(dateAction, new DependencyPropertyChangedEventArgs());
            DateAction_IsEnabledChanged(dateActionTodo, new DependencyPropertyChangedEventArgs());
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("window closing");

            string scrnProps = ((this.WindowState == WindowState.Maximized) ? "1|" : "0|") + this.Left.ToString() + "|" + this.Top.ToString() + "|" + this.Width.ToString() + "|" + this.Height.ToString();
            Config.SetProp("Screen", scrnProps);
            string tabNum = MainTab.SelectedIndex.ToString();
            Config.SetProp("Tab", (tabNum == "5") ? "0" : tabNum);

            string filters = cbAccount.SelectedIndex.ToString() + "|";
            filters += cbDateFilter.SelectedIndex.ToString() + "|";
            filters += (chkEarningsFilter.IsChecked.HasValue && chkEarningsFilter.IsChecked.Value ? "1|" : "0|")
                       + (chkNeutralFilter.IsChecked.HasValue && chkNeutralFilter.IsChecked.Value ? "1|" : "0|");

            if (chkRiskFilter.IsChecked.HasValue)
                filters += (chkRiskFilter.IsChecked.Value ? "1" : "0");
            else
                filters += "-1";
            Config.SetProp("Filters", filters);

            Config.SetProp("Grouping", cbResultsGrouping.SelectedIndex.ToString());

            // save analysis view state
            Config.SetProp("AnalysisView", cbAnalysisView.SelectedIndex.ToString() + "|" + cbAnalysisAccount.SelectedIndex.ToString() + "|" + chkOutliers.IsChecked.ToString());

            // save screener filters
            SaveScreenerFields();

            if ((selectedTag != 0) && detailsDirty) SaveTransactionGroupDetails(selectedTag);

            foreach (ContentControl cc in MainCanvas.Children)
            {
                Tiles.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }

            if (todoGrid.SelectedItem != null) UpdateTodoDetails((TransactionGroup)todoGrid.SelectedItem);


            // save group window location
            double left = (App.GroupWindow.Window != null) ? App.GroupWindow.Window.Left : App.GroupWindow.Left;
            double top = (App.GroupWindow.Window != null) ? App.GroupWindow.Window.Top : App.GroupWindow.Top;
            Config.SetProp("GroupWindow", left.ToString() + "|" + top.ToString());
            App.GroupWindow.Close();
            
            App.CloseConnection();
        }

        private void TileTooltip(object sender, ToolTipEventArgs e)
        {
            if (sender.GetType() == typeof(Grid))
            {
                Grid grid = (Grid)sender;
                ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(grid);

                int grp = Convert.ToInt32(cc.Tag);
                if ((portfolio.Count > 0) && (grp != 0))
                {
                    string ttText = portfolio[grp].GetHistory();
                    decimal price = portfolio[grp].TargetClosePrice();
                    if (price != 0)
                    {
                        string suf = "\nClose Price = ";
                        suf += Math.Abs(price).ToString("f2");
                        suf += (price < 0) ? " (Debit)" : " (Credit)";
                        ttText += suf;
                    }

                    StackPanel sp = new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Background = Brushes.Transparent
                    };

                    TextBlock txtBlk = new TextBlock()
                    {
                        Text = ttText,
                        Margin = new Thickness(5,5,5,5)
                    };
                    sp.Children.Add(txtBlk);

                    Line l = new Line()
                    {
                        X1 = 5,
                        Y1 = 5,
                        X2 = 315,
                        Y2 = 5,
                        Stroke = Brushes.DarkGray,
                        StrokeThickness = 1
                    };
                    sp.Children.Add(l);

                    GroupHistory hist = BalanceHistory.GetGroupHistory(portfolio[grp]);
                    sp.Children.Add(new GroupGraph(hist));


                    ToolTip tt = new ToolTip() { HasDropShadow = true };
                    tt.Content = sp;

                    grid.ToolTip = tt;
                }
            }
        }

        private void ToolTipPinButtonClick(object sender, RoutedEventArgs e) 
        {
            if ((App.GroupWindow.Window != null) && (App.GroupWindow.Window.IsVisible))
            {
                App.GroupWindow.Window.Hide();
            }
            else
            {
                App.GroupWindow.Open();
            }
        }

        private void TileDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Debug.WriteLine("Drag complete");
            if (sender.GetType() == typeof(MoveTile))
            {
                MoveTile tile = (MoveTile)sender;
                ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(tile.Parent);

                SnapToGrid(tile);

                Tiles.UpdateTilePosition(cc.Tag.ToString(), (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
            }
        }

        int GRIDSIZE = 10;
        private void SnapToGrid(MoveTile tile)
        {
            ContentControl cc = (ContentControl)VisualTreeHelper.GetParent(tile.Parent);
            //Debug.WriteLine("current position: {0}, {1}", (int)Canvas.GetLeft(cc), (int)Canvas.GetTop(cc));
                
            double xSnap = Canvas.GetLeft(cc) % GRIDSIZE;
            double ySnap = Canvas.GetTop(cc) % GRIDSIZE;

            // If it's less than half the grid size, snap left/up 
            // (by subtracting the remainder), 
            // otherwise move it the remaining distance of the grid size right/down
            // (by adding the remaining distance to the next grid point).
            if (xSnap <= GRIDSIZE / 2.0)
                xSnap *= -1;
            else
                xSnap = GRIDSIZE - xSnap;
            if (ySnap <= GRIDSIZE / 2.0)
                ySnap *= -1;
            else
                ySnap = GRIDSIZE - ySnap;

            xSnap += Canvas.GetLeft(cc);
            ySnap += Canvas.GetTop(cc);

            Canvas.SetLeft(cc, xSnap);
            Canvas.SetTop(cc, ySnap);
        }


        AdornerLayer adornerLayer = null;
        TileAdorner tileAdorner = null;

        private void AddAdorner(Rectangle rect)
        {
            if (adornerLayer != null && tileAdorner != null) adornerLayer.Remove(tileAdorner);

            adornerLayer = AdornerLayer.GetAdornerLayer(rect);
            if (adornerLayer != null)
            {
                tileAdorner = new TileAdorner(rect);
                adornerLayer.Add(tileAdorner);
            }
        }

        private void TileMouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("TileMouseDown");
            if (sender.GetType() == typeof(Rectangle))
            {
                Rectangle rect = (Rectangle)sender;
                AddAdorner(rect);

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
                        SetTextBox(txtOrigCapital, grp.OriginalCapitalRequired.ToString("C0"), false);
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


                        snapShot.Clear();
                        string details = "";
                        foreach(KeyValuePair<string, Position> item in grp.Holdings)
                        {
                            Position p = item.Value;
                            if (p.Type == "Stock")
                            {
                                details += String.Format("{0,2} {1}", p.Quantity, p.Type) + System.Environment.NewLine;

                                if (p.Quantity < 0)
                                    snapShot.ShortStock = - grp.Cost / p.Quantity;
                                else
                                    snapShot.LongStock = - grp.Cost / p.Quantity;
                            }
                            else
                            {
                                details += String.Format("{0,2} {1} {2} {3:MMMd}", p.Quantity, p.Type.Substring(0, 1), p.Strike, p.ExpDate) + System.Environment.NewLine;

                                if (p.Type == "Put")
                                {
                                    if (p.Quantity < 0)
                                        snapShot.ShortPut = p.Strike;
                                    else
                                        snapShot.LongPut = p.Strike;
                                }
                                else if (p.Type == "Call")
                                {
                                    if (p.Quantity < 0)
                                        snapShot.ShortCall = p.Strike;
                                    else
                                        snapShot.LongCall = p.Strike;
                                }
                            }
                        }
                        SetTextBox(txtDetails, details, true);

                        if (grp.UnderlyingPrice > 0)
                        {
                            snapShot.Price = grp.UnderlyingPrice;
                            snapShot.DeltaText = grp.GreekData.Delta.ToString("N2");
                            if (Math.Abs(grp.GreekData.Delta) > 75) snapShot.DeltaColor = Brushes.DarkRed;
                            snapShot.Update();
                        }

                        UpdateGroupDetailWindow(grp);

                    }
                }

            }
        }

        private void UpdateGroupDetailWindow(TransactionGroup grp)
        {
            // header
            App.GroupWindow.Symbol = grp.Symbol;

            if (App.GroupWindow.IsClosed())
            {
                // graph
                GroupHistory hist = BalanceHistory.GetGroupHistory(portfolio[grp.GroupID]);
                App.GroupWindow.GraphContents = new GroupGraph(hist);
            }

            // 2nd section
            App.GroupWindow.Prices = AddHoldingsPrice(grp);

            // 3rd section
            List<Detail> lst = new List<Detail>();
            if ((grp.UnderlyingPrice != 0))
            {
                lst.Add(new Detail { ItemName = "Price", Property = grp.UnderlyingPrice.ToString("C2") });
                lst.Add(new Detail { ItemName = "Price Change", Property = grp.UnderlyingPriceChange.ToString("C2") });
                lst.Add(new Detail { ItemName = "Price Change %", Property = (grp.UnderlyingPriceChange / grp.UnderlyingPrice).ToString("P2") });
            }
            lst.Add(new Detail { ItemName = "IV", Property = grp.ImpliedVolatility.ToString("P1") });
            lst.Add(new Detail { ItemName = "IV Rank", Property = grp.ImpliedVolatilityRank.ToString("P1") });
            string dateText = (grp.EarningsDate == DateTime.MinValue) ? "N/A" : grp.EarningsDate.ToString("d MMM");
            lst.Add(new Detail { ItemName = "Earnings", Property = dateText });
            App.GroupWindow.GroupDetails = lst;

            App.GroupWindow.Update();
        }

        private List<Detail> AddHoldingsPrice(TransactionGroup tg)
        {
            List<Detail> retlist = new List<Detail>();

            decimal total = 0;
            bool missingValue = false;
            foreach (KeyValuePair<string,Position> pos in tg.Holdings)
            {
                Position p = pos.Value;
                string sym = string.Format(".{0}{1:yyMMdd}{2}{3}", p.Symbol, p.ExpDate, p.Type.Substring(0, 1), p.Strike);
                decimal price = p.Market * p.Multiplier * p.Quantity / Math.Abs(p.Quantity);
                total += price;
                retlist.Add(new Detail { ItemName = sym, Property = price.ToString("N0") });
            }
            if ((!missingValue) && (retlist.Count > 1)) retlist.Add(new Detail { ItemName = "Total", Property = total.ToString("N0") });

            return retlist;
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
            initializingDatePicker = true; ;

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

            initializingDatePicker = false; ;
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
            DataPickerEnableHandler(dp);
        }
        private void DataPickerEnableHandler( DatePicker dp)
        {
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
            ClearTransactionGroupDetails();
        }

        private void ClearTransactionGroupDetails()
        {
            Debug.WriteLine("ClearTransactionGroupDetails: " + selectedTag + ": " + detailsDirty.ToString());
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
            SetTextBox(txtOrigCapital, "", false);
            SetCheckBox(chkEarnings, false, false);
            SetCheckBox(chkNeutral, false, false);
            SetCheckBox(chkDefinedRisk, false, false);
            SetTextBox(txtRisk, "", false);
            SetTextBox(txtDetails, "", false);
            SetTextBox(txtStartTime, "", false);
            SetTextBox(txtEndTime, "", false);

            snapShot.Clear();
            App.GroupWindow.Clear();
        }


        private void SaveTransactionGroupDetails(int tag)
        {
            Debug.WriteLine("Saving... " + tag.ToString());
            TransactionGroup grp = portfolio[tag];
            grp.GroupID = tag;
            grp.Strategy = txtStrategy.Text;
            grp.ExitStrategy = txtExit.Text;
            grp.ActionDate = (dateAction.SelectedDate.HasValue && (dateAction.Text !="")) ? dateAction.SelectedDate.Value : DateTime.MinValue;
            grp.Comments = txtComments.Text;
            if (Decimal.TryParse(txtCapital.Text.Replace("$", ""), out Decimal retval)) grp.CapitalRequired = retval;
            grp.EarningsTrade = chkEarnings.IsChecked.HasValue ? chkEarnings.IsChecked.Value : false;
            grp.NeutralStrategy = chkNeutral.IsChecked.HasValue ? chkNeutral.IsChecked.Value : false;

            grp.DefinedRisk = chkDefinedRisk.IsChecked.HasValue ? chkDefinedRisk.IsChecked.Value : false;
            retval = 0;
            if (Decimal.TryParse(txtRisk.Text.Replace("$", ""), out retval)) grp.Risk = retval;

            grp.Update();
            if (uiDirty)
            {
                grp = portfolio[tag];
                Tiles.UpdateTile(tag, MainCanvas, grp.Strategy, (grp.ActionDate > DateTime.MinValue));  // only fields that can be manually changed
            }

            UpdateTodosGrid();

            detailsDirty = false;
            uiDirty = false;
        }

        private void FieldEntryEvent(object sender, KeyEventArgs e)
        {
            detailsDirty = true;

            if ((((Control)sender).Name == "txtStrategy") || (((Control)sender).Name == "dateAction")) uiDirty = true;
        }
        private void DateAction_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (initializingDatePicker) return;
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

            App.InitializeStatusMessagePanel(14);

            this.Cursor = Cursors.Wait;

            // launch the long running tasks
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += SyncWithTastyTradeAsync;
            worker.RunWorkerCompleted += SyncWithTastyTradeComplete;

            worker.RunWorkerAsync();
        }

        private void SyncWithTastyTradeAsync(object sender, DoWorkEventArgs e)
        {
            DataLoader.Load(accounts);
            portfolio = new Portfolio(accounts);
            portfolio.GetCurrentData(accounts);
        }

        private void SyncWithTastyTradeComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            DisplayTilesSafe();
            UpdateResultsGrid();
            UpdateTransactionsGrid();
            UpdateTodosGrid();
            UpdateFooterSafe();

            this.Cursor = null;
            App.HideStatusMessagePanel();
            if (MainTab.SelectedIndex == 1) UpdateAnalysisView();
        }


        private void ValidateButton(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("ValidateButton...");

            Cursor prev = this.Cursor;
            this.Cursor = Cursors.Wait;

            portfolio = new Portfolio(accounts);
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

            decimal newOriginalCapReq = portfolio[selectedTag].OriginalCapitalRequired + portfolio[combineRequestTag].OriginalCapitalRequired;
            if (TransactionGroup.Combine(selectedTag, combineRequestTag, newOriginalCapReq) == 1)
            {
                // combine function doesn't update any in-memory representations, so a reload is required
                App.InitializeStatusMessagePanel(8);

                // launch the long running tasks
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += CombineRefresh;
                worker.RunWorkerCompleted += CombineRefreshComplete;
                worker.RunWorkerAsync();
            }
        }
        private void CombineRefresh(object sender, DoWorkEventArgs e)
        {
            portfolio = new Portfolio(accounts);
            portfolio.GetCurrentData(accounts);
        }
        private void CombineRefreshComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            DisplayTilesSafe();
            App.HideStatusMessagePanel();
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
                        if ((selectedTag == 0) || (combineRequestTag == selectedTag) || (portfolio[selectedTag].Symbol != portfolio[tag].Symbol) || (portfolio[selectedTag].Account != portfolio[tag].Account))
                        {
                            e.Handled = true;
                        }
                    }
                }
            }
        }



        //
        // common for both data grids
        //
        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            resultsGrid.Width = ((Grid)sender).ActualWidth - 156;
            transactionsGrid.Width = ((Grid)sender).ActualWidth - 156;
        }

        private void Grid_RowDetailsVisibilityChanged(object sender, DataGridRowDetailsEventArgs e)
        {
            if (((DataGridRowDetailsEventArgs)e).Row.DetailsVisibility == Visibility.Collapsed)
            {
                ((DataGrid)sender).SelectedIndex = -1;
            }
        }

        private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = (DataGridRow)sender;
            if ((row != null) && (row.DetailsTemplate != null))
            {
                if (row.DetailsVisibility == Visibility.Visible)
                    row.DetailsVisibility = Visibility.Collapsed;
                else
                    row.DetailsVisibility = Visibility.Visible;
            }

        }


        // 
        //
        // code for results tab
        // 
        //

        private void UpdateResultsGrid()
        {
            try
            { 
                App.UpdateStatusMessage("Updating results");

                PortfolioResults results = new PortfolioResults();
                results.GetResults();

                ListCollectionView lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(results);
                lcv.Filter = ResultsFilter;

                resultsGrid.ItemsSource = lcv;

                UpdateFilterStats();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "UpdateResultsGrid Error");
            }

        }

        private void ResultsFilterClick(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(resultsGrid.ItemsSource).Refresh();

            UpdateFilterStats();
        }
        private void ComboBox_ResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (resultsGrid.ItemsSource != null)   // grid not initialized yet
                ResultsFilterClick(null, new RoutedEventArgs());  
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



        private void CbResultsGrouping_SelectionChanged(object sender, SelectionChangedEventArgs e)
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



        private void resultsGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = ItemsControl.ContainerFromElement((DataGrid)sender, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row == null) return;

            if (row.Item.GetType() == typeof(TransactionGroup))
            {
                TransactionGroup tg = (TransactionGroup)row.Item;
                GroupHistory hist = BalanceHistory.GetGroupHistory(tg);
                App.GroupWindow.Clear();
                App.GroupWindow.Update(tg.Symbol, new GroupGraph(hist), null, null);

            }
        }


        // 
        //
        // code for transaction tab
        // 
        //

        private void UpdateTransactionsGrid()
        {
            try
            {
                App.UpdateStatusMessage("Updating transactions grid");

                Transactions results = new Transactions();
                results.GetRecent();

                ListCollectionView lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(results);
                lcv.Filter = TransactionsFilter;

                transactionsGrid.ItemsSource = lcv;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "UpdateTransactionsGrid Error");
            }
        }
        private void ComboBox_TransactionsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (transactionsGrid.ItemsSource != null)   // grid not initialized yet
                CollectionViewSource.GetDefaultView(transactionsGrid.ItemsSource).Refresh();
        }

        private bool TransactionsFilter(object item)
        {
            bool ret = false;

            //string dateTag = ((ComboBoxItem)cbDateFilter.SelectedItem).Tag.ToString();

            string accountNumber = "";
            if (cbtAccount.SelectedIndex != 0) accountNumber = accounts[cbtAccount.SelectedIndex - 1].ID;

            Transaction t = (Transaction)item;
            if (t != null)
            // If filter is turned on, filter completed items.
            {
                if ((accountNumber.Length > 0) && (t.Account != accountNumber))
                    ret = false;
                else if (txtSymbolFilter.Text.Length > 0)
                {
                    if ((t.Symbol != null) && (t.Symbol.Length >= txtSymbolFilter.Text.Length) && (txtSymbolFilter.Text.ToUpper() == t.Symbol.Substring(0, txtSymbolFilter.Text.Length)))
                        ret = true;
                    else
                        ret = false;
                }
                /*
                else if ((dateTag == "LastYear") && ((t.EndTime < new DateTime(DateTime.Now.Year - 1, 1, 1)) || (t.EndTime >= new DateTime(DateTime.Now.Year, 1, 1))))
                    ret = false;
                else if ((dateTag == "YTD") && (t.EndTime < new DateTime(DateTime.Now.Year, 1, 1)))
                    ret = false;
                else if ((dateTag == "90Days") && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(90)))
                    ret = false;
                else if ((dateTag == "30Days") && ((DateTime.Now - t.EndTime) > TimeSpan.FromDays(30)))
                    ret = false;
                    */
                else
                {
                    ret = true;
                }
            }
            return ret;
            
        }
        private void TransactionsGroupClick(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            ListCollectionView lcv = (ListCollectionView)transactionsGrid.ItemsSource;

            if (lcv != null)
            {
                // removes grouping of rows
                lcv.GroupDescriptions.Clear();

                if (cb.IsChecked == true)
                {
                    // adds grouping of rows
                    lcv.GroupDescriptions.Add(new PropertyGroupDescription("GroupID"));
                }
            }
        }
        //lcv.SortDescriptions.Clear();

        //lcv.SortDescriptions.Add(new SortDescription(grpName, ListSortDirection.Ascending));

        private void txtSymbolFilter_KeyUp(object sender, KeyEventArgs e)
        {
            if (transactionsGrid.ItemsSource != null)   // grid not initialized yet
                CollectionViewSource.GetDefaultView(transactionsGrid.ItemsSource).Refresh();
        }



        // 
        //
        // code for screener tab
        // 
        //

        int totalScreenerRows = 0;
        private void UpdateScreenerGrid()
        {
            try
            {
                if (screenerGrid.ItemsSource == null)
                {
                    EquityProfiles eqProfiles = new EquityProfiles(portfolio);
                    totalScreenerRows = eqProfiles.Count;

                    this.Dispatcher.Invoke(() =>
                    {
                        ListCollectionView lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(eqProfiles);
                        lcv.Filter = ScreenerFilter;

                        screenerGrid.ItemsSource = lcv;

                        // restore last sort used
                        string[] screenerSort = Config.GetProp("ScreenerSort").Split('|');
                        if (screenerSort.Length > 1)
                        {
                            lcv.SortDescriptions.Add(new SortDescription(screenerSort[0], (screenerSort[1] == "1") ? ListSortDirection.Ascending : ListSortDirection.Descending));
                        }

                        lbScreenerStatus.Content = String.Format($"{screenerGrid.Items.Count} of {totalScreenerRows} showing");
                        App.HideStatusMessagePanel();
                    });
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "UpdateScreenGrid Error");
            }
        }


        private void txtScreenerFilter_KeyUp(object sender, KeyEventArgs e)
        {
            RefreshScreener();
        }

        private void chkEarningsTrade_Check(object sender, RoutedEventArgs e)
        {
            RefreshScreener();
        }

        private void RefreshScreener()
        {
            if (screenerGrid.ItemsSource != null)   // grid not initialized yet
                CollectionViewSource.GetDefaultView(screenerGrid.ItemsSource).Refresh();

            lbScreenerStatus.Content = String.Format($"{screenerGrid.Items.Count} of {totalScreenerRows} showing");
        }

        private bool ScreenerFilter(object item)
        {
            bool ret = false;

            decimal minPrice = Convert.ToDecimal(SafeValueFromTextBox(txtMinPrice));
            double minVolume = SafeValueFromTextBox(txtMinVolume);
            double minIV = SafeValueFromTextBox(txtMinIV) / 100;
            double minIVR = SafeValueFromTextBox(txtMinIVR) / 100;
            decimal minMarketCap = Convert.ToDecimal(SafeValueFromTextBox(txtMinMarketCap));
            double minDTE = SafeValueFromTextBox(txtMinDTE);
            bool earningsTrade = chkEarningsTrade.IsChecked.Value;

            EquityProfile eq = (EquityProfile)item;

            if (eq != null)
            // If filter is turned on, filter completed items.
            {
                if ((eq.UnderlyingPrice > minPrice)
                    && (eq.OptionVolume > minVolume)
                    && (eq.ImpliedVolatility > minIV)
                    && (eq.ImpliedVolatilityRank > minIVR)
                    && (eq.MarketCap > minMarketCap)
                    && ((eq.DaysUntilEarnings == -1) || (eq.DaysUntilEarnings > minDTE))
                   )
                {
                    ret = true;
                }

                if (earningsTrade)
                {
                    if (eq.EarningsInNextSession != Visibility.Visible)
                        ret = false;
                }
            }
            return ret;

        }

        private double SafeValueFromTextBox(TextBox tb)
        {
            double val = -100000;
            if (tb.Text.Length > 0) Double.TryParse(tb.Text, out val);
            return val;
        }

        private void SaveScreenerConfigButton(object sender, RoutedEventArgs e)
        {
            SaveScreenerFields();
        }

        private void RecallScreenerConfigButton(object sender, RoutedEventArgs e)
        {
            UpdateScreenerFields();

            if (screenerGrid.ItemsSource != null)   // grid not initialized yet
                CollectionViewSource.GetDefaultView(screenerGrid.ItemsSource).Refresh();
        }

        private void UpdateScreenerFields()
        {
            string[] screenerView = Config.GetProp("Screener").Split('|');
            if (screenerView.Length == 6)
            {
                txtMinPrice.Text = screenerView[0];
                txtMinVolume.Text = screenerView[1];
                txtMinIV.Text = screenerView[2];
                txtMinIVR.Text = screenerView[3];
                txtMinMarketCap.Text = screenerView[4];
                txtMinDTE.Text = screenerView[5];
            }
        }

        private void SaveScreenerFields()
        {
            Config.SetProp("Screener", txtMinPrice.Text + "|" + txtMinVolume.Text + "|" + txtMinIV.Text + "|" + txtMinIVR.Text + "|" + txtMinMarketCap.Text + "|" + txtMinDTE.Text);
            if (screenerGrid.ItemsSource != null)
            {
                SortDescription sd = ((ListCollectionView)screenerGrid.ItemsSource).SortDescriptions[0];
                Config.SetProp("ScreenerSort", sd.PropertyName + "|" + ((sd.Direction == ListSortDirection.Ascending) ? "1" : "0"));
            }
        }

        // 
        //
        // code for Todo tab
        // 
        //
        public void UpdateTodosGrid()
        {
            try
            {
                App.UpdateStatusMessage("Updating todos");

                PortfolioTodos todos = new PortfolioTodos();
                todos.GetTodos();

                todoGrid.ItemsSource = todos;

                UpdateTodoIcon(todos);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "UpdateTodosGrid Error");
            }
        }

        private void UpdateTodoIcon(PortfolioTodos todos)
        {
            bool isUrgent = false;
            foreach (TransactionGroup grp in todos)
            {
                if (grp.ActionDate <= DateTime.Today.AddDays(1))
                {
                    isUrgent = true;
                    break;
                }
            }

            if (todos.Count == 0)
            {
                TodoIcon.Visibility = Visibility.Hidden;
            }
            else
            {
                TodoIcon.Visibility = Visibility.Visible;
                TodoCountColor.Fill = isUrgent ? Brushes.Red : Brushes.Green;
                tbTodoCount.Text = todos.Count.ToString();
            }
        }

        private void TodoClearClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            Debug.WriteLine("TodoClearClick - " + ((TransactionGroup)mi.DataContext).Symbol);

            int groupID = ((TransactionGroup)mi.DataContext).GroupID;
            TransactionGroup tg = portfolio[groupID];
            tg.ActionDate = DateTime.MinValue;
            tg.Update();
            UpdateTodosGrid();
            DisplayTilesSafe();  //refresh tiles
        }

        private void TodoContextMenuValidationCheck(object sender, ContextMenuEventArgs e)
        {
            // return e.Handled = true to suppress menu
            if (sender.GetType() == typeof(DataGrid))
            {
                DataGrid grid = (DataGrid)sender;
                if (grid.SelectedItem == null) return;
                TransactionGroup tg = (TransactionGroup)grid.SelectedItem;
                Debug.WriteLine("click: " + tg.Symbol);
            }
        }

        private void UpdateTodoDetails(TransactionGroup grp)
        {
            if (todoDirty)
            {
                bool dateChange = false;
                DateTime previousDate = grp.ActionDate;
                grp.ActionDate = (dateActionTodo.SelectedDate.HasValue && (dateActionTodo.Text != "")) ? dateActionTodo.SelectedDate.Value : DateTime.MinValue;
                dateChange = (previousDate != grp.ActionDate);
                grp.Comments = txtCommentsTodo.Text;
                grp.ActionText = txtAction.Text;

                TransactionGroup tg = portfolio[grp.GroupID];
                tg.ActionDate = (dateActionTodo.SelectedDate.HasValue && (dateActionTodo.Text != "")) ? dateActionTodo.SelectedDate.Value : DateTime.MinValue;
                tg.Comments = txtCommentsTodo.Text;
                tg.ActionText = txtAction.Text;
                tg.Update();

                todoDirty = false;

                // if the date got manually cleared, repaint the tiles and reload todo grid
                if (tg.ActionDate == DateTime.MinValue)
                {
                    UpdateTodosGrid();
                    DisplayTilesSafe();  //refresh tiles
                }


            }
        }

        private void TodoGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0)
            {
                UpdateTodoDetails( ((TransactionGroup)(e.RemovedItems[0])));
            }

            if (todoGrid.SelectedItem == null)
            {
                ClearTodoFields();
            }
            else
            {
                TransactionGroup grp = ((TransactionGroup)(todoGrid.SelectedItem));
                Debug.WriteLine("Selected: " + grp.GroupID.ToString());

                SetTextBlock(txtSymbolTodo, grp.Symbol, true);
                SetDatePicker(dateActionTodo, grp.ActionDate, true);
                SetTextBox(txtCommentsTodo, grp.Comments, true);
                SetTextBox(txtAction, grp.ActionText, true);
            }
        }

        private void ClearTodoFields()
        {
            SetTextBlock(txtSymbolTodo, "", false);
            SetDatePicker(dateActionTodo, DateTime.MinValue, false);
            SetTextBox(txtCommentsTodo, "", false);
            SetTextBox(txtAction, "", false);
        }

        private void TodoFieldEntryEvent(object sender, KeyEventArgs e)
        {
            todoDirty = true;
        }
        private void DateActionTodo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (initializingDatePicker) return;
            todoDirty = true;
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
                Debug.WriteLine("Tab " + ((TabControl)sender).SelectedIndex.ToString());

                TabItem previousTab = null;
                if (e.RemovedItems.Count != 0) previousTab = (TabItem)e.RemovedItems[0];

                switch (((TabControl)sender).SelectedIndex)
                {
                    case 1:
                        UpdateAnalysisView();
                        break;
                    case 5:
                        if (screenerGrid.ItemsSource == null)
                        {
                            //activate status bar, as loading hasn't
                            App.InitializeStatusMessagePanel(4);
                        }
                        break;
                }

                if (previousTab != null)
                {
                    if ((previousTab.Name == "TodoTab") && (todoGrid.SelectedItem != null))
                    {
                        UpdateTodoDetails((TransactionGroup)todoGrid.SelectedItem);
                    }
                    if ((previousTab.Name == "HoldingsTab") && (selectedTag != 0) && detailsDirty)
                    {
                        SaveTransactionGroupDetails(selectedTag);
                    }
                }
            }
        }

        private void MainTab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // cleanup up the datepicker's disable state only works when its on the active tab
            DataPickerEnableHandler(dateAction);
            DataPickerEnableHandler(dateActionTodo);


            // Fix adorner (that is lost with tab changes)
            if (selectedTag != 0)   //skip if nothing is selected
            {
                foreach (ContentControl cc in MainCanvas.Children)
                {
                    if (Convert.ToInt32(cc.Tag) == selectedTag)
                    {
                        // AddAdorner(cc);

                        Grid topGrid = (Grid)VisualTreeHelper.GetChild(cc, 0);
                        Rectangle rect = UIHelper.FindChild<Rectangle>(topGrid, "DragRectangle");
                        AddAdorner(rect);
                    }
                }
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

            decimal horizontalOrigin = -1;

            //Rectangle rect = new Rectangle();
            //rect.Width = width + 150;
            //rect.Height = height + 100;
            //rect.Stroke = Brushes.LightGray;
            //rect.StrokeThickness = 1;
            //Canvas.SetLeft(rect, margin);
            //Canvas.SetTop(rect, margin);
            //AnalysisCanvas.Children.Add(rect);


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
                            grp.AnalysisXValue = (grp.CurrentValue ?? 0) + grp.Cost;
                            grp.AnalysisYValue = grp.CapitalRequired;

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
                            grp.AnalysisXValue = Convert.ToDecimal(grp.GreekData.Theta);
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;
                            break;
                        case 3:
                            grp.AnalysisXValue = Convert.ToDecimal(grp.GreekData.Theta);
                            grp.AnalysisYValue = grp.CapitalRequired;

                            overallXValue += grp.AnalysisXValue;
                            overallYValue += grp.AnalysisYValue;

                            grp.AnalysisXValue /= (grp.CapitalRequired < 100m) ? 100m : grp.CapitalRequired;
                            break;
                        case 4:
                            grp.AnalysisXValue = Convert.ToDecimal(grp.GreekData.Delta);
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

                Line line = new Line()
                {
                    X1 = x,
                    Y1 = margin,
                    X2 = x,
                    Y2 = height + margin + 80,
                    Stroke = Brushes.DimGray,
                    StrokeThickness = 1
                };
                AnalysisCanvas.Children.Add(line);

                // origin label
                TextBlock text1 = new TextBlock()
                {
                    Text = FormatValue(horizontalOrigin, viewList[viewIndex].XFormat),
                    Foreground = Brushes.DimGray,
                    FontSize = 15,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(text1, x - (text1.Text.Length * 4));
                Canvas.SetTop(text1, height + margin + 85);
                AnalysisCanvas.Children.Add(text1);
            }

            // horizontal axis label
            TextBlock text = new TextBlock()
            {
                Text = viewList[viewIndex].XAxisLabel,
                Foreground = Brushes.DimGray,
                FontSize = 30,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(text, width + margin);
            Canvas.SetTop(text, height + margin + 100);
            AnalysisCanvas.Children.Add(text);

            // vertical axis label
            text = new TextBlock()
            {
                Text = viewList[viewIndex].YAxisLabel,
                Foreground = Brushes.DimGray,
                FontSize = 30,
                LayoutTransform = new RotateTransform(-90),
                TextAlignment = TextAlignment.Right
            };
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

                    Tiles.CreateTile(this, AnalysisCanvas, Tiles.TileSize.Small, ((grp.CurrentValue ?? 0) + grp.Cost), grp.GroupID, grp.Symbol, 0, 0, grp.AccountName, 
                        left, top, grp.Strategy, value2, value1,
                        (grp.EarliestExpiration == DateTime.MaxValue) ? "" : (grp.EarliestExpiration - DateTime.Today).TotalDays.ToString(), false, false, false,
                        viewList[viewIndex].YLabel, viewList[viewIndex].XLabel, (viewIndex == 0) ? grp.ChangeFromPreviousClose.ToString("+#;-#") : null, 1.0 );
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
                    if (overallYValue != 0) txtXVal.Text = FormatValue(overallXValue / overallYValue, viewList[viewIndex].XFormat);
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
                    (((view == 2) || (view == 3)) && (grp.GreekData.Theta > 0))  ||
                    ((view == 4) && (grp.GreekData.Delta < 100))
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

            string mode = ((ComboBoxItem)mw.cbResultsGrouping.SelectedItem).Tag.ToString();

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

    public class ExpDateToBlank : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() == typeof(DateTime))
            {
                DateTime t = (DateTime)value;
                if (t == DateTime.MinValue)
                {
                    return "";
                }
                else
                {
                    return string.Format("{0:d MMM}", t);
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }
    public class ValueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.GetType() == typeof(decimal))
            {
                decimal v = (decimal)value;

                if (v > 0.0005M)
                    return Brushes.PaleGreen;
                else if (v < -0.0005M)
                    return Brushes.LightCoral;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}




