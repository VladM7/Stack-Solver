using Stack_Solver.Helpers.Rendering;
using Stack_Solver.Models.Layering;
using Stack_Solver.Models.Supports;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Services
{
    public interface ILayerVisualizationService
    {
        void HighlightItem(Model3DGroup scene, PositionedItem? item, double palletHeight);
        void Build2DRectangles(Layer layer, SupportSurface pallet, int gridStep, ObservableCollection<Rect> target);
    }

    public class LayerVisualizationService : ILayerVisualizationService
    {
        private Model3DGroup? _selectionHighlight;

        public void HighlightItem(Model3DGroup scene, PositionedItem? item, double palletHeight)
        {
            if (_selectionHighlight != null)
            {
                scene.Children.Remove(_selectionHighlight);
                _selectionHighlight = null;
            }
            if (item == null) return;
            var sku = item.SkuType;
            double boxLength = item.Rotated ? sku.Width : sku.Length;
            double boxWidth = item.Rotated ? sku.Length : sku.Width;
            double boxHeight = sku.Height;
            double inflate = 0.6;
            var origin = new Point3D(item.X - inflate / 2.0, palletHeight + 0.01, item.Y - inflate / 2.0);
            var fillBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0));
            var edgeColor = Colors.Yellow;
            _selectionHighlight = GeometryCreator.CreateBoxWithEdges(origin, boxLength + inflate, boxHeight + inflate, boxWidth + inflate, fillBrush, edgeColor, 0.6);
            scene.Children.Add(_selectionHighlight);
        }

        public void Build2DRectangles(Layer layer, SupportSurface pallet, int gridStep, ObservableCollection<Rect> target)
        {
            LayerGeometryBuilder.Build(layer, pallet, gridStep);
            target.Clear();
            if (layer.Geometry?.ItemRectangles != null)
            {
                double canvasHeight = pallet.Width;
                foreach (var r in layer.Geometry.ItemRectangles)
                {
                    var display = new Rect(r.X, canvasHeight - (r.Y + r.Height), r.Width, r.Height);
                    target.Add(display);
                }
            }
        }
    }
}
