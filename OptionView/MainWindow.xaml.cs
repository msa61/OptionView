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


namespace OptionView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            //DataLoader.Load("all transactions.csv");
            DataLoader.Load("feb-19.csv");

            InitializeComponent();



            if ((App.ConnStr != null) && App.ConnStr.State == System.Data.ConnectionState.Open) App.ConnStr.Close();
        }

 
    }
}
