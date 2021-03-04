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
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }


        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;

            Config.SetEncryptedProp("Username", txtUser.Text);
            Config.SetEncryptedProp("Password", txtPW.Text);

            TastyWorks.ResetToken();

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            this.Close();
        }
    }
}
