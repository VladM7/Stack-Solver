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
    /// <summary>
    /// Interaction logic for AddPalletDialog.xaml
    /// </summary>
    public partial class AddPalletDialog : Window
    {
        public AddPalletDialog()
        {
            InitializeComponent();
        }

        private void addPalletButton_Click(object sender, RoutedEventArgs e)
        {
            successLabel.Content = "yes";
            this.Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            successLabel.Content = "no";
            this.Close();
        }
    }
}
