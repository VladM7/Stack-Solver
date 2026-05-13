using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Stack_Solver.Converters
{
    public class BoolToInfoBarSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? InfoBarSeverity.Success : InfoBarSeverity.Error;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
