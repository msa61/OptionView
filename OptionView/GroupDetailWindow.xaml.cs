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

        public void Update (GroupGraph gg, double left = 0, double top = 0)
        {
            GroupGraphHolder.Children.Clear();
            if (gg != null) GroupGraphHolder.Children.Add(gg);

            if (left > 0) this.Left = left;
            if (top> 0) this.Top = top;
        }
        public void Clear ()
        {
            GroupGraphHolder.Children.Clear();
        }

    }
}
