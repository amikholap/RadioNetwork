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
using Network;
using System.Windows.Controls.Primitives;
using RadioNetwork.Controls;
using System.Windows.Threading;
using RadioNetwork.DataContext;



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

        /// <summary>
        /// Show a message box with warning text.
        /// </summary>
        /// <param name="warning"></param>
        public static void Warn(string warning)
        {
            MessageBox.Show(warning, "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            _isTalking = false;

            // Start in client mode
            ModeToggleButton.IsChecked = false;
            SwitchToClientMode();

            // Launch periodic task of updating server list
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Start();
            timer.Tick += this.UpdateAvailableServersTimer_Tick;
        }

        private bool StartClient()
        {
            string callsign = _cdc.Callsign;
            var fr = UInt32.Parse(_cdc.Fr);
            var ft = UInt32.Parse(_cdc.Ft);

            if (AvailableServers.SelectedItem == null)
            {
                // server not chosen
                RadioNetwork.MainWindow.Warn("Выберите, к какой радиосети подключиться.");
                return false;
            }

            var servAddr = ((ServerSummary)AvailableServers.SelectedItem).Addr;

            return Controller.StartClient(callsign, fr, ft, servAddr);
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
        /// Return a NumericalImageTextBox that awaits for input.
        /// Left control is returned first.
        /// </summary>
        /// <returns></returns>
        public NumericalImageTextBox GetCurrentFrequencyInput()
        {
            if (FrTextBox.Text.Length < FrTextBox.MaxLength)
            {
                return FrTextBox;
            }
            if (FtTextBox.Text.Length < FtTextBox.MaxLength)
            {
                return FtTextBox;
            }
            return null;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            Controller.Stop();
            Controller.ShutDown();
        }

        private void PushToTalkToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ImageToggleButton btn = (ImageToggleButton)sender;
            btn.ClickMode = ClickMode.Release;
            Controller.StartTalking();
            _isTalking = true;
        }

        private void PushToTalkToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ImageToggleButton btn = (ImageToggleButton)sender;
            btn.ClickMode = ClickMode.Press;
            Controller.StopTalking();
            _isTalking = false;
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

        /// <summary>
        /// Switch to server mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            SwitchToServerMode();
        }

        /// <summary>
        /// Connect to a server.
        /// Available only in client mode.
        /// 
        /// Return to unchecked state if something went wrong.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PowerToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!StartClient())
            {
                // something went wrong
                ToggleButton b = (ToggleButton)sender;
                b.IsChecked = false;
                return;
            }
        }

        /// <summary>
        /// Disconect from a server.
        /// Available only in client mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PowerToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Controller.Stop();
        }

        private void DigitButton_Click(object sender, RoutedEventArgs e)
        {
            var input = this.GetCurrentFrequencyInput();

            if (input != null)
            {
                char digit = ((Control)sender).Name.Last();
                input.Text += digit;
            }
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _cdc.Fr = "";
            _cdc.Ft = "";       
        }

        /// <summary>
        /// Focus right input when left input is full.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            NumericalImageTextBox tb = (NumericalImageTextBox)sender;
            if (tb.Text.Length >= tb.MaxLength)
            {
                FtTextBox.Focus();
            }
        }

        /// <summary>
        /// Update ItemsSource of available servers grid.
        /// </summary>
        private void UpdateAvailableServersTimer_Tick(object sender, EventArgs e)
        {
            bool wasFocused = AvailableServers.HasFocus();
            var servers = Controller.AvailableServers;
            int si = AvailableServers.SelectedIndex;

            // Update list of servers
            AvailableServers.ItemsSource = servers;
            AvailableServers.UpdateLayout();

            if (si == -1 && AvailableServers.Items.Count > 0)
            {
                // If nothing was selected select the first server
                si = 0;
            }

            // Restore selection
            AvailableServers.SelectedIndex = si;

            // Restore focus
            if (wasFocused)
            {
                var row = (DataGridRow)AvailableServers.ItemContainerGenerator.ContainerFromIndex(si);
                if (row != null)
                {
                    row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }
    }
}