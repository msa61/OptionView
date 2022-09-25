using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window, INotifyPropertyChanged
    {
        private string statusMessage = "Loading...";

        public LoadingWindow()
        {
            InitializeComponent();
            DataContext = this;
        }
        

        public String Message
        {
            get
            {
                return statusMessage;
            }
            set
            {
                Thread.Sleep(50);
                this.Dispatcher.Invoke(() =>
                {
                    lbStatus.Content = value;
                    tbStatus.Text += value + "\n";
                });

                pbStatus.Value += 1;
                statusMessage = value;
                OnPropertyChanged("statusMessage");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        protected void OnPropertyChanged(String property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }

            Application.Current.Dispatcher.Invoke( DispatcherPriority.Background, new ThreadStart(delegate { }));
        }

    }

}
