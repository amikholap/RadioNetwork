﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Logic;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace RadioNetwork
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ClientDataContext _cdc;
        private ServerDataContext _sdc;

        private void StartClient()
        {
            string callsign = _cdc.Callsign;
            int fr = int.Parse(_cdc.Fr);
            int ft = int.Parse(_cdc.Ft);

            Controller.StartClient(callsign, fr, ft);
        }

        private void StartServer()
        {
            Controller.StartServer();
        }

        /// <summary>
        /// Entry point of application code.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            this._sdc = new ServerDataContext();
            this._cdc = new ClientDataContext();
            this.DataContext = _cdc;

            StartClient();
        }

        void OnWindowClosing(object sender, CancelEventArgs e)
        {
            Controller.Stop();
        }

        /// <summary>
        /// Switch to client mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ClientModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClientModeMenuItem.IsChecked = true;
            ServerModeMenuItem.IsChecked = false;

            StartClient();

            ServerLayout.Visibility = System.Windows.Visibility.Collapsed;
            ClientLayout.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// Switch to server mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ServerModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClientModeMenuItem.IsChecked = false;
            ServerModeMenuItem.IsChecked = true;

            StartServer();

            ClientLayout.Visibility = System.Windows.Visibility.Collapsed;
            ServerLayout.Visibility = System.Windows.Visibility.Visible;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            StartClient();
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Controller.Stop();
        }
    }
}