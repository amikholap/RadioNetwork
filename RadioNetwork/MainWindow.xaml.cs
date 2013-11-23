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

        public MainWindow()
        {
            InitializeComponent();

            // this._sdc = new ServerDataContext();
            // Controller.StartServer();

            this._cdc = new ClientDataContext();
            this.DataContext = _cdc;
        }

        void OnWindowClosing(object sender, CancelEventArgs e)
        {
            Controller.Stop();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            string callsign = _cdc.Callsign;
            int fr = int.Parse(_cdc.Fr);
            int ft = int.Parse(_cdc.Ft);

            if (Controller.Mode != ControllerMode.Client)
            {
                Controller.StartClient(callsign, fr, ft);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Controller.Stop();
        }
    }
}