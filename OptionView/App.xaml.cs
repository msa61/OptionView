using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Data.SQLite;

namespace OptionView
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static SQLiteConnection ConnStr = null;

        public static void OpenConnection()
        {
            if (ConnStr == null) App.ConnStr = new SQLiteConnection("Data Source=transactions.sqlite;Version=3;");
            if (ConnStr.State == System.Data.ConnectionState.Closed) App.ConnStr.Open();
        }

        public static void CloseConnection()
        {
            if ((ConnStr != null) && ConnStr.State == ConnectionState.Open) ConnStr.Close();
        }

    }
}
