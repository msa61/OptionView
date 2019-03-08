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
            //DataLoader.Load("feb-19.csv");
            //DataLoader.Load("feb-22.csv");
            //DataLoader.Load("feb-24.csv");
            //DataLoader.Load("mar-5.csv");
            //DataLoader.Load("mar-7.csv");
            //DataLoader.Load("gld.csv");
            //DataLoader.Load("spy.csv");
            //DataLoader.Load("msft.csv");

            //DataLoader.Load("DALcorrection.csv");

            HoldingsHelper.UpdateNewTransactions();


            InitializeComponent();



            App.CloseConnection();
        }


    }
}
