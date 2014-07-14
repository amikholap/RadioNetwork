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
            Controller.StartServer(_sdc.Callsign);
        }

        private void SwitchToClientMode()
        {
            Controller.Stop();
            this.DataContext = _cdc;
        }

        private void SwitchToServerMode()
        {
            StartServer();
            this.DataContext = _sdc;
        }


        /// <summary>
        /// Entry point of application code.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            Controller.Init();

            _sdc = new ServerDataContext(Controller.Server);
            _cdc = new ClientDataContext(Controller.Client);

            // Initialize IsTalking state
            PushToTalkButton.ClickMode = ClickMode.Press;
            _isTalking = false;

            // Start in client mode
            ModeToggleButton.IsChecked = false;
            SwitchToClientMode();
        }

        void OnWindowClosing(object sender, CancelEventArgs e)
        {
            Controller.Stop();
            Controller.ShutDown();
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

        /// <summary>
        /// Switch to client mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            SwitchToClientMode();
        }

        private void ModeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            SwitchToServerMode();
        }
    }
}