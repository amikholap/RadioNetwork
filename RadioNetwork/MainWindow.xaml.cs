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

            // Start in client mode
            ModeToggleButton.IsChecked = false;
            SwitchToClientMode();

            // Launch periodic task of updating server list
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Start();
            timer.Tick += this.UpdateAvailableServersTimer_Tick;

            Controller.SpeechRecognized += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TextLog.Text += String.Format("{0}: {1}\n", e.Talker.Callsign, e.Message);
                    TextLog.ScrollToEnd();
                });
            };
        }

        private bool StartClient()
        {
            string callsign = ((ClientDataContext)this.DataContext).Callsign;
            var fr = UInt32.Parse(((ClientDataContext)this.DataContext).Fr);
            var ft = UInt32.Parse(((ClientDataContext)this.DataContext).Ft);

            if (AvailableServers.SelectedItem == null)
            {
                // server not chosen
                RadioNetwork.MainWindow.Warn("Выберите, к какой радиосети подключиться.");
                return false;
            }

            var servAddr = ((ServerSummary)AvailableServers.SelectedItem).Addr;

            bool isStarted = Controller.StartClient(callsign, fr, ft, servAddr);
            this.DataContext = new ClientDataContext(Controller.Client);

            return isStarted;
        }

        private void StartServer()
        {
            Controller.StartServer(((ServerDataContext)this.DataContext).Callsign);
            this.DataContext = new ServerDataContext(Controller.Server);
        }

        private void SwitchToClientMode()
        {
            Controller.Stop();
            this.DataContext = new ClientDataContext(Controller.Client);
        }
        private void SwitchToServerMode()
        {
            Controller.Stop();
            this.DataContext = new ServerDataContext(Controller.Server);
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

        /// <summary>
        /// Global shortcut for space as PushToTalk button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                PushToTalkToggleButton.IsChecked = true;
                e.Handled = true;
            }
        }
        /// <summary>
        /// Global shortcut for space as PushToTalk button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                PushToTalkToggleButton.IsChecked = false;
                e.Handled = true;
            }
        }

        private void PushToTalkToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ImageToggleButton btn = (ImageToggleButton)sender;
            btn.ClickMode = ClickMode.Release;
            Controller.StartTalking();
        }
        private void PushToTalkToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ImageToggleButton btn = (ImageToggleButton)sender;
            btn.ClickMode = ClickMode.Press;
            Controller.StopTalking();
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
            if (this.DataContext is ClientDataContext)
            {
                if (!StartClient())
                {
                    // something went wrong
                    ToggleButton b = (ToggleButton)sender;
                    b.IsChecked = false;
                    return;
                }
            }
            else if (this.DataContext is ServerDataContext)
            {
                StartServer();
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
            FocusFrequencyInput();
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ((ClientDataContext)this.DataContext).Fr = "";
            ((ClientDataContext)this.DataContext).Ft = "";
            FocusFrequencyInput();
        }

        /// <summary>
        /// Focus right input when left one is full.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NumericalImageTextBox tb = (NumericalImageTextBox)sender;
            if (tb.Text.Length >= tb.MaxLength)
            {
                this.FocusFrequencyInput(FtTextBox);
            }
        }
        /// <summary>
        /// Cover case not handled by FrTextBox_TextChanged method.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            NumericalImageTextBox tb = (NumericalImageTextBox)sender;
            if (e.Key != Key.Back && tb.Text.Length == tb.MaxLength)
            {
                this.FocusFrequencyInput(FtTextBox);
            }
        }
        /// <summary>
        /// Focus left input when right one is empty.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FtTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NumericalImageTextBox tb = (NumericalImageTextBox)sender;
            if (tb.Text.Length == 0)
            {
                this.FocusFrequencyInput(FrTextBox);
            }
        }
        /// <summary>
        /// Hanle case not handled by FtTextBox_TextChanged method.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FtTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            NumericalImageTextBox tb = (NumericalImageTextBox)sender;
            if (e.Key == Key.Back && tb.Text.Length == 0)
            {
                this.FocusFrequencyInput(FrTextBox);
            }
        }
        /// <summary>
        /// Focus a frequency display control.
        /// Set caret to the end of text.
        /// </summary>
        /// <param name="input">Input to focus. `null` value tells to use the first incomplete one.</param>
        private void FocusFrequencyInput(NumericalImageTextBox input = null)
        {
            if (input == null)
            {
                input = this.GetCurrentFrequencyInput();
            }
            if (input == null)
            {
                return;
            }
            input.CaretIndex = input.Text.Length;
            input.Focus();
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