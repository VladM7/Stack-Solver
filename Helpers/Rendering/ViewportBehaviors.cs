using System.Windows.Controls;
using System.Windows.Input;

namespace Stack_Solver.Helpers.Rendering
{
    public static class ViewportBehaviors
    {
        public static readonly DependencyProperty ZoomCommandProperty =
        DependencyProperty.RegisterAttached("ZoomCommand", typeof(ICommand), typeof(ViewportBehaviors));

        public static readonly DependencyProperty BeginPanCommandProperty =
            DependencyProperty.RegisterAttached("BeginPanCommand", typeof(ICommand), typeof(ViewportBehaviors));

        public static readonly DependencyProperty PanCommandProperty =
            DependencyProperty.RegisterAttached("PanCommand", typeof(ICommand), typeof(ViewportBehaviors));

        public static void SetZoomCommand(UIElement element, ICommand value) => element.SetValue(ZoomCommandProperty, value);
        public static ICommand GetZoomCommand(UIElement element) => (ICommand)element.GetValue(ZoomCommandProperty);

        public static void SetBeginPanCommand(UIElement element, ICommand value) => element.SetValue(BeginPanCommandProperty, value);
        public static ICommand GetBeginPanCommand(UIElement element) => (ICommand)element.GetValue(BeginPanCommandProperty);

        public static void SetPanCommand(UIElement element, ICommand value) => element.SetValue(PanCommandProperty, value);
        public static ICommand GetPanCommand(UIElement element) => (ICommand)element.GetValue(PanCommandProperty);

        static ViewportBehaviors()
        {
            EventManager.RegisterClassHandler(typeof(Viewport3D), UIElement.MouseWheelEvent, new MouseWheelEventHandler(OnMouseWheel));
            EventManager.RegisterClassHandler(typeof(Viewport3D), UIElement.MouseDownEvent, new MouseButtonEventHandler(OnMouseDown));
            EventManager.RegisterClassHandler(typeof(Viewport3D), UIElement.MouseMoveEvent, new MouseEventHandler(OnMouseMove));
        }

        private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is Viewport3D vp)
                GetZoomCommand(vp)?.Execute(Convert.ToDouble(e.Delta));
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Viewport3D vp && e.LeftButton == MouseButtonState.Pressed)
                GetBeginPanCommand(vp)?.Execute(e.GetPosition(vp));
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Viewport3D vp && e.LeftButton == MouseButtonState.Pressed)
                GetPanCommand(vp)?.Execute(e.GetPosition(vp));
        }

    }
}
