using System;
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
        private bool _isTalking;

        private void StartClient()
        {
            string callsign = _cdc.Callsign;
            var fr = UInt32.Parse(_cdc.Fr);
            var ft = UInt32.Parse(_cdc.Ft);

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

            ClientPushToTalkButton.ClickMode = ClickMode.Press;
            ServerPushToTalkButton.ClickMode = ClickMode.Press;
            _isTalking = false;

            _cdc = new ClientDataContext();
            ClientLayout.DataContext = _cdc;
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

            Controller.Stop();

            if (_cdc == null)
            {
                _cdc = new ClientDataContext();
            }
            ClientLayout.DataContext = _cdc;

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

            _sdc = new ServerDataContext(Controller.Server);
            ServerLayout.DataContext = _sdc;

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

        private void PushToTalkButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            if (_isTalking)
            {
                btn.ClickMode = ClickMode.Press;
                Controller.StopTalking();
                _isTalking = false;
            }
            else
            {
                btn.ClickMode = ClickMode.Release;
                Controller.StartTalking();
                _isTalking = true;
            }
        }
    }
}