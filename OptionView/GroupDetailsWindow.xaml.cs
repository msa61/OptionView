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
using System.Windows.Shapes;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for GroupWindow.xaml
    /// </summary>
    public partial class GroupDetailsWindow : Window
    {
        public GroupDetailsWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Clear();
            App.GroupWindow.Left = this.Left;
            App.GroupWindow.Top = this.Top;
            App.GroupWindow.Window = null;
        }

        public void Update (GroupGraph gg, List<Detail> prices, List<Detail> details)
        {
            GroupGraphHolder.Children.Clear();
            if (gg != null) GroupGraphHolder.Children.Add(gg);

            SetGrid(priceGrid, prices, priceLabel);
            SetGrid(detailsGrid, details, detailsLabel);
        }
        public void Clear ()
        {
            GroupGraphHolder.Children.Clear();
            DetailTables.Visibility = Visibility.Collapsed;
        }

        private void SetGrid(DataGrid grid, List<Detail> details, Label label)
        {
            if (details == null)
            {
                grid.Visibility = Visibility.Collapsed;
                label.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailTables.Visibility = Visibility.Visible;
                grid.Visibility = Visibility.Visible;
                grid.ItemsSource = details;
                label.Visibility = Visibility.Visible;
            }
        }

    }



    public class Detail
    {
        public string ItemName { get; set; }
        public string Property { get; set; }
    }




}
