using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Stack_Solver.Helpers.Behaviors;

/// <summary>
/// Attached behavior that forwards mouse wheel events to the nearest scrollable parent <see cref="ScrollViewer"/>.
/// Use this when nested controls (DataGrid, NumberBox, etc.) consume wheel events and prevent parent scrolling.
/// </summary>
public static class ParentScrollBehavior
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(ParentScrollBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if ((bool)e.NewValue)
        {
            element.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel), handledEventsToo: true);
        }
        else
        {
            element.RemoveHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel));
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindScrollableParent(sender as DependencyObject) is { } scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private static ScrollViewer? FindScrollableParent(DependencyObject? element)
    {
        var parent = element is null ? null : VisualTreeHelper.GetParent(element);

        while (parent is not null)
        {
            if (parent is ScrollViewer sv && CanScroll(sv))
                return sv;

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static bool CanScroll(ScrollViewer sv) =>
        sv.ScrollableHeight > 0 ||
        (sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled &&
         sv.ComputedVerticalScrollBarVisibility == Visibility.Visible);
}
