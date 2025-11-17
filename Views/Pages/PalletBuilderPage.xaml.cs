using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Models;
using Stack_Solver.Models.Layering;
using Stack_Solver.ViewModels.Pages;
using System.Windows.Controls;
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
            MainViewPort.MouseLeftButtonDown += MainViewPort_MouseLeftButtonDown;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (ViewModel.ViewportController == null && MainPerspectiveCamera is PerspectiveCamera cam)
            {
                ViewModel.AttachCamera(cam);
            }
        }

        private void MainViewPort_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MainViewPort);
            var hitParams = new PointHitTestParameters(pos);
            PositionedItem? selected = null;

            HitTestResultCallback resultCallback = r =>
            {
                if (r is RayHitTestResult rayResult)
                {
                    if (rayResult.ModelHit is GeometryModel3D geo)
                    {
                        // Access scene builder mapping via reflection (quick solution) or store in Tag
                        var builderField = typeof(PalletBuilderViewModel).GetField("_sceneBuilder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (builderField?.GetValue(ViewModel) is LayerSceneBuilder builder)
                        {
                            if (builder.TryGetItemForGeometry(geo, out var item))
                            {
                                selected = item;
                                return HitTestResultBehavior.Stop;
                            }
                        }
                    }
                }
                return HitTestResultBehavior.Continue;
            };

            VisualTreeHelper.HitTest(MainViewPort, null, resultCallback, hitParams);
            ViewModel.UpdateSelectedItem(selected);
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
                    await ViewModel.UpdateSkuAsync(sku);
                }
                catch
                {
                }
            }
        }

        private void TopHelpButton_Click(object sender, RoutedEventArgs e)
        {
            helpFlyout.IsOpen = !helpFlyout.IsOpen;
        }
    }
}
