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
        public static SQLiteConnection ConnStr = null;
        private static LoadingWindow loadWindow = null;
        public static bool OfflineMode = false;


        private void OnStartup(object sender, StartupEventArgs e)
        {
            if ((e.Args.Count() > 0) && (e.Args[0].ToLower() == "offline")) OfflineMode = true;

            loadWindow = new LoadingWindow();
            loadWindow.Show();

            MainWindow wnd = new MainWindow();
            loadWindow.Close();
            wnd.Show();
        }

        public static void UpdateLoadStatusMessage(string txt)
        {
            if (loadWindow.IsActive) loadWindow.Message = txt;
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
