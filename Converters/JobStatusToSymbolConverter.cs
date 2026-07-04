using Stack_Solver.Models.Jobs;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Stack_Solver.Converters
{
    /// <summary>Maps a <see cref="JobStatus"/> to the glyph shown beside it in the Job Manager grid.</summary>
    public class JobStatusToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is JobStatus status
                ? status switch
                {
                    JobStatus.Finished => SymbolRegular.CheckmarkCircle24,
                    JobStatus.Failed => SymbolRegular.DismissCircle24,
                    JobStatus.Canceled => SymbolRegular.ErrorCircle24,
                    JobStatus.Ongoing => SymbolRegular.MoreCircle24,
                    _ => SymbolRegular.MoreCircle24,
                }
                : SymbolRegular.MoreCircle24;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
