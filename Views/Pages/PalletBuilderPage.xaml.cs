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
            PalletViewPortHost.MouseLeftButtonDown += PalletViewPort_MouseLeftButtonDown;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            await ViewModel.OnNavigatedToAsync();
            if (ViewModel.LayerAnalyzer.ViewportController == null && MainPerspectiveCamera is PerspectiveCamera cam)
                ViewModel.LayerAnalyzer.AttachCamera(cam);
            if (ViewModel.PalletAnalyzer.ViewportController == null && PalletPerspectiveCamera is PerspectiveCamera palletCam)
                ViewModel.PalletAnalyzer.AttachCamera(palletCam);

            ViewModel.PalletAnalyzer.PropertyChanged += PalletAnalyzer_PropertyChanged;
            if (PalletPerspectiveCamera is PerspectiveCamera pc)
                pc.Changed += (_, _) => UpdatePalletDimLabels();
            PalletViewPort.SizeChanged += (_, _) => UpdatePalletDimLabels();
        }

        private void PalletAnalyzer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalletAnalyzerViewModel.PalletDimLabels))
                UpdatePalletDimLabels();
        }

        private void UpdatePalletDimLabels()
        {
            PalletLabelCanvas.Children.Clear();
            if (PalletPerspectiveCamera is not PerspectiveCamera cam) return;

            var labels = ViewModel.PalletAnalyzer.PalletDimLabels;
            if (labels.Count == 0) return;

            double vpW = PalletViewPort.ActualWidth;
            double vpH = PalletViewPort.ActualHeight;

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
            PositionedItem? selected = null;

            HitTestResultBehavior resultCallback(HitTestResult r)
            {
                if (r is RayHitTestResult rayResult && rayResult.ModelHit is GeometryModel3D geo && ViewModel.LayerAnalyzer.TryGetItemFromGeometry(geo, out var item))
                {
                    selected = item;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            }

            VisualTreeHelper.HitTest(MainViewPort, null, resultCallback, hitParams);
            ViewModel.LayerAnalyzer.UpdateSelectedItem(selected);
        }

        private void PalletViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(PalletViewPort);
            var hitParams = new PointHitTestParameters(pos);
            LayerTypeDisplay? found = null;

            HitTestResultBehavior resultCallback(HitTestResult r)
            {
                if (r is RayHitTestResult rayResult
                    && rayResult.ModelHit is GeometryModel3D geo
                    && ViewModel.PalletAnalyzer.TryGetLayerTypeForGeometry(geo, out var layerType))
                {
                    found = layerType;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            }

            VisualTreeHelper.HitTest(PalletViewPort, null, resultCallback, hitParams);
            ViewModel.PalletAnalyzer.SelectedLayerType = found;
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

        private void TopHelpButton_Click(object sender, RoutedEventArgs e)
        {
            helpFlyout.IsOpen = !helpFlyout.IsOpen;
        }
    }
}
