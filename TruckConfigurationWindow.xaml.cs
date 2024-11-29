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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace Stack_Solver
{
    public partial class TruckConfigurationWindow : Window
    {
        public TruckConfigurationWindow()
        {
            InitializeComponent();
        }

        private void addItemButton_Click(object sender, RoutedEventArgs e)
        {
            PalletConfigWindow palletConfigWindow = new PalletConfigWindow();
            palletConfigWindow.ShowDialog();
        }
    }
}
