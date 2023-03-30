using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Data.SQLite;
using System.IO;
using System.Windows.Threading;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static SQLiteConnection ConnStr { get; set; } = null;
        private static LoadingWindow loadWindow = null;
        public static GroupWindowHandler GroupWindow = new GroupWindowHandler();
        public static bool OfflineMode { get; set; } = false;
        public static bool DataRefreshMode { get; set; } = false;


        private void OnStartup(object sender, StartupEventArgs e)
        {
            if ((e.Args.Count() > 0) && (e.Args[0].ToLower() == "offline")) OfflineMode = true;
            if ((e.Args.Count() > 0) && (e.Args[0].ToLower() == "refresh")) DataRefreshMode = true;

            InitializeStatusWindow(17);

            MainWindow wnd = new MainWindow();
            CloseStatusWindow();
            wnd.Show();
        }

        public static void UpdateStatusMessage(string txt)
        {
            if (loadWindow.IsActive) loadWindow.Message = txt;
        }

        public static void InitializeStatusWindow(int count)
        {
            if ((loadWindow != null) && (loadWindow.IsActive)) loadWindow.Close();

            loadWindow = new LoadingWindow();
            loadWindow.pbStatus.Maximum = count;
            loadWindow.Show();
        }
        public static void CloseStatusWindow()
        {
            loadWindow.Close();
        }

        public static void UpdateStatusWindowCount(int count)
        {
            loadWindow.pbStatus.Maximum = count;
        }

        public static void OpenConnection()
        {
            try
            {
                if (! File.Exists("transactions.sqlite"))
                {
                    MessageBox.Show("Sqlite database not found", "OpenConnection");
                }
                if (ConnStr == null) ConnStr = new SQLiteConnection("Data Source=transactions.sqlite;Version=3;");
                if (ConnStr.State == System.Data.ConnectionState.Closed) ConnStr.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "OpenConnection");
            }
        }

        public static void CloseConnection()
        {
            if ((ConnStr != null) && ConnStr.State == ConnectionState.Open) ConnStr.Close();
        }

    }
}
