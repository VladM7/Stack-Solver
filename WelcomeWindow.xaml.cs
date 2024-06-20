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
            TruckConfigurationWindow truckConfigurationWindow = new TruckConfigurationWindow(120, 80, 14.4, 33);
            truckConfigurationWindow.Show();
            this.Close();
        }
    }
}
