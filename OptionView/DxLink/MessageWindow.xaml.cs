using OptionView;
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

namespace DxLink
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        public MessageWindow()
        {
            InitializeComponent();
        }

        public void WriteMessage(string message)
        {
            //return;
            if (tbMain.Text.Length > 32000) tbMain.Text = tbMain.Text.Substring(0, 32000);
            if ((message == ".") && (tbMain.Text.Substring(0, 1) == "."))
            {
                tbMain.Text = message + tbMain.Text;
            }
            else
            {
                tbMain.Text = message + tbMain.Text;
            }
        }

        public void WriteStatus(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                tbStatus.Text = message;
            });
        }

        public void Reset()
        {
            tbMain.Text = "";
        }

    }
}
