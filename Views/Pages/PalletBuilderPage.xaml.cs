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
            MainViewPortHost.MouseLeftButtonDown += MainViewPortHost_MouseLeftButtonDown;
            MainViewPortHost.MouseLeftButtonUp += MainViewPortHost_MouseLeftButtonUp;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            await ViewModel.OnNavigatedToAsync();

            if (ViewModel.Results.ViewportController == null && MainPerspectiveCamera is PerspectiveCamera cam)
                ViewModel.Results.AttachCamera(cam);

            ViewModel.Results.PropertyChanged += Results_PropertyChanged;
            if (MainPerspectiveCamera is PerspectiveCamera pc)
                pc.Changed += (_, _) => UpdateSceneLabels();
            MainViewPort.SizeChanged += (_, _) => UpdateSceneLabels();

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
            if (e.PropertyName == nameof(ResultsViewModel.SceneLabels))
                UpdateSceneLabels();
        }

        private void UpdateSceneLabels()
        {
            PalletLabelCanvas.Children.Clear();
            if (MainPerspectiveCamera is not PerspectiveCamera cam) return;

            var labels = ViewModel.Results.SceneLabels;
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

        // A left-drag rotates the camera; a left-click selects. We only select if the pointer barely
        // moved between press and release, so rotating no longer changes the selection.
        private Point _pointerDownPos;
        private int _pointerDownClickCount;
        private const double ClickMoveThreshold = 5.0;

        private void MainViewPortHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pointerDownPos = e.GetPosition(MainViewPort);
            _pointerDownClickCount = e.ClickCount;
        }

        private void MainViewPortHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MainViewPort);
            var movement = pos - _pointerDownPos;
            if (movement.Length > ClickMoveThreshold) return; // treated as a rotate, not a click

            SelectAtPointer(pos, drill: _pointerDownClickCount == 2);
        }

        private void SelectAtPointer(Point pos, bool drill)
        {
            var hitParams = new PointHitTestParameters(pos);
            var results = ViewModel.Results;

            // Find the first hit geometry that maps to something selectable at this level.
            GeometryModel3D? Hit(Func<GeometryModel3D, bool> maps)
            {
                GeometryModel3D? hit = null;
                HitTestResultBehavior callback(HitTestResult r)
                {
                    if (r is RayHitTestResult ray && ray.ModelHit is GeometryModel3D geo && maps(geo))
                    {
                        hit = geo;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                }
                VisualTreeHelper.HitTest(MainViewPort, null, callback, hitParams);
                return hit;
            }

            switch (results.Level)
            {
                case DrillLevel.Layer:
                {
                    PositionedItem? item = null;
                    Hit(g => results.TryGetItemFromGeometry(g, out item));
                    results.SelectBox(item); // null => deselect
                    break;
                }
                case DrillLevel.Pallet:
                {
                    LayerTypeDisplay? layer = null;
                    Hit(g => results.TryGetLayerTypeForGeometry(g, out layer));
                    results.SelectedLayerType = layer; // null => deselect
                    if (drill && layer != null) results.ViewLayerCommand.Execute(layer);
                    break;
                }
                default: // Solution
                {
                    TemplateAssignmentDisplay? assignment = null;
                    Hit(g => results.TryGetAssignmentForGeometry(g, out assignment));
                    results.SelectedAssignment = assignment; // null => deselect
                    if (drill && assignment != null) results.ViewPalletCommand.Execute(assignment);
                    break;
                }
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

        private void PalletTypesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid { SelectedItem: TemplateAssignmentDisplay assignment }
                && ViewModel.Results.ViewPalletCommand.CanExecute(assignment))
            {
                ViewModel.Results.ViewPalletCommand.Execute(assignment);
            }
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
