﻿using System;
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
        private static MainWindow mainWindow = null;
        public static GroupWindowHandler GroupWindow = new GroupWindowHandler();
        public static bool OfflineMode { get; set; } = false;
        public static bool DataRefreshMode { get; set; } = false;


        private void OnStartup(object sender, StartupEventArgs e)
        {
            if ((e.Args.Count() > 0) && (e.Args[0].ToLower() == "offline")) OfflineMode = true;
            if ((e.Args.Count() > 0) && (e.Args[0].ToLower() == "refresh")) DataRefreshMode = true;


            mainWindow = new MainWindow();
            InitializeStatusMessagePanel(14);
            mainWindow.Show();
        }

        public static void UpdateStatusMessage(string txt)
        {
            bool onUIThread = ((Dispatcher)mainWindow.Dispatcher).CheckAccess();
            if (onUIThread)
            {
                mainWindow.lbLoadStatus.Content = txt;
                mainWindow.pbLoadStatus.Value += 1;
                //mainWindow.lbLoadStatus.Content = mainWindow.pbLoadStatus.Value.ToString();
            }
            else
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    mainWindow.lbLoadStatus.Content = txt;
                    mainWindow.pbLoadStatus.Value += 1;
                    //mainWindow.lbLoadStatus.Content = mainWindow.pbLoadStatus.Value.ToString();
                });
            }
        }

        public static void InitializeStatusMessagePanel(int count)
        {
            bool onUIThread = ((Dispatcher)mainWindow.Dispatcher).CheckAccess();
            if (onUIThread)
            {
                ShowPanel( count);
            }
            else
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    ShowPanel(count);
                });
            }
        }

        private static void ShowPanel(int count)
        {
            mainWindow.pbLoadStatus.Maximum = count;
            mainWindow.pbLoadStatus.Value = 0;
            mainWindow.LoadStatusPanel.Visibility = Visibility.Visible;
            mainWindow.OverviewPanel.Visibility = Visibility.Collapsed;
            mainWindow.MetricsPanel.Visibility = Visibility.Collapsed;
        }

        public static void HideStatusMessagePanel()
        {
            bool onUIThread = ((Dispatcher)mainWindow.Dispatcher).CheckAccess();
            if (onUIThread)
            {
                HidePanel();
            }
            else
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    HidePanel();
                });
            }
        }

        private static void HidePanel()
        {
            mainWindow.LoadStatusPanel.Visibility = Visibility.Collapsed;
            mainWindow.OverviewPanel.Visibility = Visibility.Visible;
            mainWindow.MetricsPanel.Visibility = Visibility.Visible;
        }

        public static void UpdateStatusMessageCount(int count)
        {
            bool onUIThread = ((Dispatcher)mainWindow.Dispatcher).CheckAccess();
            if (onUIThread)
            {
                mainWindow.pbLoadStatus.Maximum = count;
            }
            else
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    mainWindow.pbLoadStatus.Maximum = count;
                });
            }
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
