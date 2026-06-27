using FluentValidation;
using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.ViewModels.Pages;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Wpf.Ui.Abstractions.Controls;

namespace Stack_Solver.Views.Pages
{
    public partial class PalletBuilderPage : INavigableView<PalletBuilderViewModel>
    {
        public PalletBuilderViewModel ViewModel { get; set; }

        public PalletBuilderPage(PalletBuilderViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
            Loaded += OnLoaded;
            MainViewPortHost.MouseLeftButtonDown += MainViewPort_MouseLeftButtonDown;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            await ViewModel.OnNavigatedToAsync();

            if (ViewModel.Results.ViewportController == null && MainPerspectiveCamera is PerspectiveCamera cam)
                ViewModel.Results.AttachCamera(cam);

            ViewModel.Results.PropertyChanged += Results_PropertyChanged;
            if (MainPerspectiveCamera is PerspectiveCamera pc)
                pc.Changed += (_, _) => UpdatePalletDimLabels();
            MainViewPort.SizeChanged += (_, _) => UpdatePalletDimLabels();

            ConstrainToHostHeight();
        }

        // The page is hosted inside the NavigationView's scroll viewer, which would otherwise
        // let the whole page scroll. Pin the page to the visible viewport height so the results
        // column fills the app height and the setup rail scrolls within itself.
        private ScrollViewer? _hostScrollViewer;

        private void ConstrainToHostHeight()
        {
            _hostScrollViewer = FindAncestor<ScrollViewer>(this);
            if (_hostScrollViewer is null) return;

            _hostScrollViewer.SizeChanged += (_, _) => ApplyHostHeight();
            ApplyHostHeight();
            Dispatcher.BeginInvoke(new Action(ApplyHostHeight), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ApplyHostHeight()
        {
            if (_hostScrollViewer is null) return;
            double h = _hostScrollViewer.ViewportHeight;
            RootGrid.MaxHeight = h > 0 ? h : double.PositiveInfinity;
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(start);
            while (parent is not null)
            {
                if (parent is T match) return match;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private bool _settingsCollapsed;
        private GridLength _expandedRailWidth = new(340);

        private void ToggleSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsCollapsed = !_settingsCollapsed;

            if (_settingsCollapsed)
            {
                // Remember the (possibly user-resized) width, then shrink the column to the strip.
                _expandedRailWidth = SettingsColumn.Width;
                SettingsColumn.MinWidth = 0;
                SettingsColumn.Width = GridLength.Auto;
            }
            else
            {
                SettingsColumn.MinWidth = 240;
                SettingsColumn.Width = _expandedRailWidth;
            }

            SettingsRail.Visibility = _settingsCollapsed ? Visibility.Collapsed : Visibility.Visible;
            RailSplitter.Visibility = _settingsCollapsed ? Visibility.Collapsed : Visibility.Visible;
            ExpandStrip.Visibility = _settingsCollapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Results_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResultsViewModel.PalletDimLabels))
                UpdatePalletDimLabels();
        }

        private void UpdatePalletDimLabels()
        {
            PalletLabelCanvas.Children.Clear();
            if (MainPerspectiveCamera is not PerspectiveCamera cam) return;

            // Dimension labels only make sense for the full pallet stack, not a single layer.
            if (ViewModel.Results.IsLayerLevel) return;

            var labels = ViewModel.Results.PalletDimLabels;
            if (labels.Count == 0) return;

            double vpW = MainViewPort.ActualWidth;
            double vpH = MainViewPort.ActualHeight;

            foreach (var label in labels)
            {
                var pt = ViewportProjection.ProjectToScreen(label.Position, cam, vpW, vpH);
                if (pt is null) continue;

                var tb = new TextBlock
                {
                    Text = label.Text,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontFamily = new FontFamily("Cascadia Code"),
                    FontWeight = FontWeights.SemiBold,
                };
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 2, 5, 2),
                    Child = tb,
                };
                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(border, pt.Value.X - border.DesiredSize.Width / 2);
                Canvas.SetTop(border, pt.Value.Y - border.DesiredSize.Height / 2);
                PalletLabelCanvas.Children.Add(border);
            }
        }

        private void MainViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MainViewPort);
            var hitParams = new PointHitTestParameters(pos);
            var results = ViewModel.Results;

            if (results.IsLayerLevel)
            {
                PositionedItem? selected = null;
                HitTestResultBehavior callback(HitTestResult r)
                {
                    if (r is RayHitTestResult ray && ray.ModelHit is GeometryModel3D geo
                        && results.TryGetItemFromGeometry(geo, out var item))
                    {
                        selected = item;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                }
                VisualTreeHelper.HitTest(MainViewPort, null, callback, hitParams);
                results.SelectBox(selected);
            }
            else
            {
                LayerTypeDisplay? found = null;
                HitTestResultBehavior callback(HitTestResult r)
                {
                    if (r is RayHitTestResult ray && ray.ModelHit is GeometryModel3D geo
                        && results.TryGetLayerTypeForGeometry(geo, out var layerType))
                    {
                        found = layerType;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                }
                VisualTreeHelper.HitTest(MainViewPort, null, callback, hitParams);
                if (found != null)
                    results.SelectedLayerType = found;
            }
        }

        private async void SkuSelectionGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            if (e.Row.Item is SKU sku)
            {
                if (e.EditingElement is TextBox tb && tb.GetBindingExpression(TextBox.TextProperty) is { } be)
                {
                    be.UpdateSource();
                }

                try
                {
                    await ViewModel.Settings.UpdateSkuAsync(sku);
                }
                catch (ValidationException ex)
                {
                    var message = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
                    MessageBox.Show(message, "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SkuCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.NotifySelectionChanged();
        }

        private void LayersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: LayerTypeDisplay layerType }
                && ViewModel.Results.ViewLayerCommand.CanExecute(layerType))
            {
                ViewModel.Results.ViewLayerCommand.Execute(layerType);
            }
        }

        private void TopHelpButton_Click(object sender, RoutedEventArgs e)
        {
            helpFlyout.IsOpen = !helpFlyout.IsOpen;
        }
    }
}
