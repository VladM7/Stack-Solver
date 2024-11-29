using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace Stack_Solver
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void palletConfigurationButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void truckConfigurationButton_Click(object sender, RoutedEventArgs e)
        {
            TruckConfigurationWindow truckConfigurationWindow = new TruckConfigurationWindow();
            truckConfigurationWindow.Show();
            this.Close();
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
