using rendering;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace Stack_Solver
{
    public partial class TruckConfigurationWindow : Window
    {
        private Point previousMousePosition;

        struct palet
        {
            public double length, width, height;
            public double weight, weight_limit, height_limit;
        }

        struct cutie
        {
            public double length, width, height;
            public double weight;
        }

        struct arii
        {
            public double value;
            public int nrb1, nrb2;
        }

        palet p;
        cutie c;
        arii[] aria1 = new arii[105];
        arii[] aria2 = new arii[105];

        public int max_boxes_length;
        public int max_boxes_width1;
        public int max_boxes_width2;

        public double aria_palet;
        public double aria_cutie;

        public int box_type_nr;
        public int nr_levels;
        public double eff_height;

        public double length;
        public double width;

        public double area_occupied;

        public int zPositionOffset = -200;

        bool generationError = false;

        Brush boxBrush = Brushes.SandyBrown;
        Brush palletBrush = Brushes.SteelBlue;
        Brush brush = Brushes.SandyBrown;

        private int cameraType = 0;

        public PerspectiveCamera myPCamera = new PerspectiveCamera();

        private Rendering render = new Rendering();

        private void MainViewPort_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Camera camera;
            if (MainViewPort.Camera.GetType() == typeof(PerspectiveCamera))
            {
                camera = (PerspectiveCamera)MainViewPort.Camera;
            }
            else
            {
                camera = (OrthographicCamera)MainViewPort.Camera;
            }

            ScaleTransform3D? transform = camera.Transform as ScaleTransform3D;

            // If the transform is null, create a new one
            if (transform == null)
            {
                transform = new ScaleTransform3D();
                camera.Transform = transform;
            }

            // Increase or decrease the scale based on the mouse wheel delta
            double delta = e.Delta < 0 ? 1.1 : 0.9;
            transform.ScaleX *= delta;
            transform.ScaleY *= delta;
            transform.ScaleZ *= delta;
        }

        private void readValues()
        {
            if (pL.Text == "" || pW.Text == "" || pH.Text == "" || pWghtlim.Text == "" ||
                bL.Text == "" || bW.Text == "" || bH.Text == "" || bWght.Text == "")
                return;
            p.length = Convert.ToDouble(pL.Text);
            p.width = Convert.ToDouble(pW.Text);
            p.height = Convert.ToDouble(pH.Text);
            p.weight_limit = Convert.ToDouble(pWghtlim.Text);

            c.length = Convert.ToDouble(bL.Text);
            c.width = Convert.ToDouble(bW.Text);
            c.height = Convert.ToDouble(bH.Text);
            c.weight = Convert.ToDouble(bWght.Text);
        }

        private void readBoxes()
        {
            if (bL.Text == "" || bW.Text == "" || bH.Text == "" || bWght.Text == "" || pWghtlim.Text == "")
                return;

            p.weight_limit = Convert.ToDouble(pWghtlim.Text);

            c.length = Convert.ToDouble(bL.Text);
            c.width = Convert.ToDouble(bW.Text);
            c.height = Convert.ToDouble(bH.Text);
            c.weight = Convert.ToDouble(bWght.Text);
        }

        private void readPallets()
        {
            if (pL.Text == "" || pW.Text == "" || pH.Text == "" || pWghtlim.Text == "")
                return;
            p.length = Convert.ToDouble(pL.Text);
            p.width = Convert.ToDouble(pW.Text);
            p.height = Convert.ToDouble(pH.Text);
            p.weight_limit = Convert.ToDouble(pWghtlim.Text);
        }

        double max(double a, double b)
        {
            return Math.Max(a, b);
        }

        double min(double a, double b)
        {
            return Math.Min(a, b);
        }

        private void calcul_arie(double pallet_len, double pallet_width, int var, ref double vmax, ref int nrb, ref arii[] aria)
        {
            aria_palet = pallet_width * pallet_len;
            aria_cutie = c.length * c.width;

            max_boxes_length = (int)(pallet_len / c.length);
            max_boxes_width1 = (int)(pallet_width / c.length);
            max_boxes_width2 = (int)(pallet_width / c.width);

            for (int nr_boxes1 = max_boxes_length; nr_boxes1 >= 0; nr_boxes1--)
            {
                double diff = pallet_len - nr_boxes1 * c.length;
                int nr_boxes2 = (int)(diff / c.width) * max_boxes_width1;
                aria[nr_boxes1].value = aria_cutie * (nr_boxes1 * max_boxes_width2 + nr_boxes2);
                aria[nr_boxes1].nrb1 = nr_boxes1 * max_boxes_width2;
                aria[nr_boxes1].nrb2 = nr_boxes2;
            }

            box_type_nr = -1;
            for (int i = 0; i <= max_boxes_length; i++)
                if (aria[i].value > vmax)
                {
                    vmax = aria[i].value;
                    box_type_nr = aria[i].nrb1 + aria[i].nrb2;
                    nrb = i;
                }
        }

        private void draw3D(arii[] aria, int nrb, double pallet_len, double pallet_width, int levels, double inset, int inset_type, double offset_x, double offset_y)
        {
            Type t = typeof(DirectionalLight);
            for (int i = MainViewPort.Children.Count - 1; i >= 0; i--)
            {
                if (MainViewPort.Children[i].GetType() == t)
                    MainViewPort.Children.RemoveAt(i);
            }
            //MainViewPort.Children.Clear();

            ModelVisual3D Lights = new ModelVisual3D();
            DirectionalLight light = new DirectionalLight();
            light.Color = Colors.Brown;
            light.Direction = new Vector3D(-2, -3, -1);
            Lights.Content = light;

            Model3DGroup triangle = new Model3DGroup();

            int nrboxes1, nrboxes2;
            if (max_boxes_width1 == 0)
                nrboxes2 = 0;
            else
                nrboxes2 = aria[nrb].nrb2 / max_boxes_width1;
            if (max_boxes_width2 == 0)
                nrboxes1 = 0;
            else
                nrboxes1 = aria[nrb].nrb1 / max_boxes_width2;

            double xc = (aria[nrb].nrb1 / max_boxes_width2) * (c.length + 0.5);
            double inset1 = 0, inset2 = 0;
            if (inset_type == 1)
                inset1 = inset;
            else
                inset2 = inset;

            pallet_len += 0.5 * (nrboxes1 + nrboxes2);
            pallet_width += 0.5 * (max_boxes_width1 + max_boxes_width2);

            brush = palletBrush;
            render.generateStack(pallet_len, pallet_width, p.height / 4, 1, 1, 1, -pallet_len / 2, -pallet_width / 2, zPositionOffset + 0.75 * p.height, ref triangle, brush);

            brush = boxBrush;
            render.generateStack(c.length, c.width, c.height, nrboxes1, max_boxes_width2, levels, offset_x - pallet_len / 2, offset_y + inset1 - pallet_width / 2, p.height + zPositionOffset, ref triangle, brush);
            render.generateStack(c.width, c.length, c.height, nrboxes2, max_boxes_width1, levels, offset_x + xc - pallet_len / 2, offset_y + inset2 - pallet_width / 2, p.height + zPositionOffset, ref triangle, brush);
            //MessageBox.Show(c.length.ToString());

            ModelVisual3D Model = new ModelVisual3D();
            Model.Content = triangle;
            this.MainViewPort.Children.Add(Model);
        }

        private void generateDrawing(double pallet_len, double pallet_width, int var, int vmax, int nrb, arii[] aria)
        {
            aria_palet = pallet_width * pallet_len;
            aria_cutie = c.length * c.width;
            area_occupied = vmax;

            max_boxes_length = (int)(pallet_len / c.length);
            max_boxes_width1 = (int)(pallet_width / c.length);
            max_boxes_width2 = (int)(pallet_width / c.width);

            int boxesType1 = aria[nrb].nrb1;
            int boxesType2 = aria[nrb].nrb2;
            box_type_nr = boxesType1 + boxesType2;

            if (p.weight_limit == 0)
                nr_levels = (int)(p.height / c.height);
            else
                nr_levels = (int)min((int)(p.height / c.height), (int)(p.weight_limit / (box_type_nr * c.weight)));
            if (nr_levels == 0)
            {
                resultTextBox.Text = "Error.";
                statusInfoBar.Severity = InfoBarSeverity.Error;
                statusInfoBar.Title = "Error";
                statusInfoBar.Message = "No levels can be placed on the truck.";
                statusInfoBar.IsOpen = true;
                generationError = true;
                return;
            }

            eff_height = nr_levels * c.height;

            if (max_boxes_width1 == 0)
                length = aria[nrb].nrb1 / max_boxes_width2 * c.length;
            else if (max_boxes_width2 == 0)
                length = aria[nrb].nrb2 / max_boxes_width1 * c.width;
            else
                length = aria[nrb].nrb1 / max_boxes_width2 * c.length + aria[nrb].nrb2 / max_boxes_width1 * c.width;
            if (boxesType1 == 0)
                width = max_boxes_width1 * c.length;
            else if (boxesType2 == 0)
                width = max_boxes_width2 * c.width;
            else
                width = max(max_boxes_width1 * c.length, max_boxes_width2 * c.width);

            double offset_length;
            double offset_width;
            if (var == 1)
            {
                offset_length = (p.length - length) / 2;
                offset_width = (p.width - width) / 2;
            }
            else
            {
                offset_length = (p.width - length) / 2;
                offset_width = (p.length - width) / 2;
            }

            int inset = 0, inset_type = 0;
            if (boxesType1 != 0 && boxesType2 != 0)
            {
                if (max_boxes_width1 * c.length > max_boxes_width2 * c.width)
                {
                    inset = (int)(max_boxes_width1 * c.length - max_boxes_width2 * c.width) / 2;
                    inset_type = 1;
                }
                else
                {
                    inset = (int)(max_boxes_width2 * c.width - max_boxes_width1 * c.length) / 2;
                    inset_type = 2;
                }
            }

            int start_x_above = 200;
            int start_y_above = 161;

            int start_x_lateral1 = (int)(230 + max(p.length, p.width));
            int start_x_lateral2 = start_x_lateral1 + (int)(aria[nrb].nrb1 / max_boxes_width2 * c.length);
            int start_y_lateral = 161 + (int)eff_height;

            start_x_above += (int)offset_length;
            start_y_above += (int)offset_width;

            double inv_len = max(pallet_len, pallet_width);
            double inv_wid = min(pallet_len, pallet_width);

            resultRunTextBlock.Text = "Area occupied: " + vmax + "cm³\nUnoccupied space: "
                + (aria_palet - vmax)
                + "cm²\nEfficiency: " + Math.Round((vmax / aria_palet * 100), 2, MidpointRounding.AwayFromZero) + "%\n\nPallet type: " +
                inv_len + "x" + inv_wid +
                "cm²\nBox type: " + c.length + "x" + c.width + "x" + c.height + "cm³ / " + c.weight + "kg\n" +
                "_________________________________________\n\n" +
                "Number of boxes / level: " + box_type_nr
                + "\nNumber of levels: "
                + nr_levels + "\nEffective height: " + Math.Round(eff_height, 2) + "cm\nLoad dimensions: " + Math.Round(length, 2) + "x" + Math.Round(width, 2) + "x"
                + Math.Round((eff_height - p.height), 2)
                + "cm³\nPallet dimensions: " + pallet_len + "x" + pallet_width + "x"
                + Math.Round(eff_height, 2) + "cm³\nOffset (x, y): " + Math.Round(offset_length, 2) + "cm, " + Math.Round(offset_width, 2) +
                "cm\n" +
                "_________________________________________\n\n" +
                "Load weight: " + (nr_levels * box_type_nr * c.weight) + "kg\nTotal weight: "
                + (nr_levels * box_type_nr * c.weight + p.weight)
                + "kg\nWeight / level: " + box_type_nr * c.weight +
                "kg\nTotal number of boxes: " + (nr_levels * box_type_nr) + "\n\n";
            draw3D(aria, nrb, pallet_len, pallet_width, nr_levels, inset, inset_type, offset_length, offset_width);
        }

        public TruckConfigurationWindow(double length, double width, double height, double weight)
        {
            InitializeComponent();
            p.length = length;
            p.width = width;
            p.height = height;
            p.weight = weight;
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void releasesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var destinationurl = "https://github.com/VladM7/Stack-Solver/releases/";
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var destinationurl = "https://github.com/VladM7/Stack-Solver/";
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void keyboardFocusSelectAll(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.Tab))
                ((System.Windows.Controls.TextBox)sender).SelectAll();
        }

        private void pW_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pH_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pHlim_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pWght_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pWghtlim_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void bL_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void bW_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void bH_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void bWght_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pL_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bL.Text = p.length.ToString();
            bW.Text = p.width.ToString();
            bH.Text = p.height.ToString();
            bWght.Text = p.weight.ToString();

            if (MainViewPort.Camera.GetType() == typeof(PerspectiveCamera))
            {
                // Get the scale transform from the Viewport3D's camera
                PerspectiveCamera camera = (PerspectiveCamera)MainViewPort.Camera;
                ScaleTransform3D? transform = camera.Transform as ScaleTransform3D;

                // If the transform is null, create a new one
                if (transform == null)
                {
                    transform = new ScaleTransform3D();
                    camera.Transform = transform;
                }

                double delta = 20;
                transform.ScaleX *= delta;
                transform.ScaleY *= delta;
                transform.ScaleZ *= delta;
            }
            else
            {
                System.Windows.MessageBox.Show("Camera is not PerspectiveCamera");
            }
        }

        private void clearViewport()
        {
            foreach (object i in MainViewPort.Children)
                if (i.GetType() == typeof(ModelVisual3D))
                {
                    ModelVisual3D model = (ModelVisual3D)i;
                    if (model == null)
                    { continue; }
                    if (model != defaultLights)
                    {
                        MainViewPort.Children.Remove(model);
                        break;
                    }
                }
        }

        private void compare_results()
        {
            if (c.height > p.height)
            {
                resultTextBox.Text = "Cargo exceeds height limit!";
                statusInfoBar.Severity = InfoBarSeverity.Error;
                statusInfoBar.Title = "Error";
                statusInfoBar.Message = "Cargo exceeds height limit.";
                statusInfoBar.IsOpen = true;
                generationError = true;
                return;
            }
            if (max(c.length, c.width) > max(p.length, p.width))
            {
                resultTextBox.Text = "Cargo exceeds truck dimensions!";
                statusInfoBar.Severity = InfoBarSeverity.Error;
                statusInfoBar.Title = "Error";
                statusInfoBar.Message = "Cargo exceeds truck dimensions.";
                statusInfoBar.IsOpen = true;
                generationError = true;
                return;
            }
            double areaMax1 = 0, areaMax2 = 0;
            int areaMaxPos1 = 0, areaMaxPos2 = 0;
            calcul_arie(p.length, p.width, 1, ref areaMax1, ref areaMaxPos1, ref aria1);
            calcul_arie(p.width, p.length, 2, ref areaMax2, ref areaMaxPos2, ref aria2);
            if (areaMax1 >= areaMax2)
                generateDrawing(p.length, p.width, 1, (int)areaMax1, areaMaxPos1, aria1);
            else
                generateDrawing(p.width, p.length, 2, (int)areaMax2, areaMaxPos2, aria2);
        }

        private void calculateBtn_Click(object sender, RoutedEventArgs e)
        {
            excelBtn.IsEnabled = true;
            saveImageButton.IsEnabled = true;
            generationError = false;
            clearViewport();
            readValues();
            compare_results();
            excelBtn.IsEnabled = true;
            //readValues();
            //compare_results();
            if (generationError == false)
            {
                statusInfoBar.Severity = InfoBarSeverity.Success;
                statusInfoBar.Title = "Success";
                statusInfoBar.Message = "Generation complete.";
                statusInfoBar.IsOpen = true;
            }
            //ShowAxes();
        }

        private double rotationAngleX = 0, rotationAngleY = 0;

        private void MainViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            previousMousePosition = e.GetPosition(MainViewPort);
            MainViewPort.CaptureMouse();
        }

        private void MainViewPort_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainViewPort.ReleaseMouseCapture();
        }

        private Point3D cameraPosition1 = new Point3D(0, 40, 0);
        private Vector3D vector3DLookDirection1 = new Vector3D(0, -1, 0);
        private Vector3D vector3DUpDirection1 = new Vector3D(0, 0, -1);

        private Point3D cameraPosition2 = new Point3D(11, 10, 9);
        private Vector3D vector3DLookDirection2 = new Vector3D(-12, -11, -10);
        private Vector3D vector3DUpDirection2 = new Vector3D(0, 1, 0);

        private Point3D cameraPosition3 = new Point3D(40, 0, 0);
        private Vector3D vector3DLookDirection3 = new Vector3D(-1, 0, 0);
        private Vector3D vector3DUpDirection3 = new Vector3D(0, 1, 0);

        private Point3D cameraPosition4 = new Point3D(0, 0, 40);
        private Vector3D vector3DLookDirection4 = new Vector3D(0, 0, -1);
        private Vector3D vector3DUpDirection4 = new Vector3D(0, 1, 0);

        private void switchCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (cameraType == 0)
            {
                OrthographicCamera orthographicCamera = new OrthographicCamera();
                RegisterName("threedOrthoCamera", orthographicCamera);
                orthographicCamera.Position = new Point3D(51, 50, 49);
                orthographicCamera.LookDirection = new Vector3D(-12, -11, -10);
                orthographicCamera.FarPlaneDistance = 500;
                orthographicCamera.NearPlaneDistance = -100;
                orthographicCamera.UpDirection = new Vector3D(0, 1, 0);
                orthographicCamera.Width = 450;
                MainViewPort.Camera = orthographicCamera;

                switchCameraButton.Content = "Switch to perspective camera";
                cameraType = 1;
            }
            else
            {
                if (MainViewPort.Camera.GetType() == typeof(OrthographicCamera))
                    UnregisterName("threedOrthoCamera");
                MainViewPort.Camera = threedCamera;

                switchCameraButton.Content = "Switch to orthographic camera";
                cameraType = 0;
            }
            freeCameraRadioButton.IsChecked = true;
        }

        private void freeCameraRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            OrthographicCamera? orthographicCamera = FindName("threedOrthoCamera") as OrthographicCamera;
            threedCamera.Position = cameraPosition2;
            threedCamera.LookDirection = vector3DLookDirection2;
            threedCamera.UpDirection = vector3DUpDirection2;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(51, 50, 49);
            orthographicCamera.LookDirection = vector3DLookDirection2;
            orthographicCamera.UpDirection = vector3DUpDirection2;
        }

        private void topDownViewRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            OrthographicCamera? orthographicCamera = FindName("threedOrthoCamera") as OrthographicCamera;
            threedCamera.Position = cameraPosition1;
            threedCamera.LookDirection = vector3DLookDirection1;
            threedCamera.UpDirection = vector3DUpDirection1;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(0, 60, 0);
            orthographicCamera.LookDirection = vector3DLookDirection1;
            orthographicCamera.UpDirection = vector3DUpDirection1;
        }

        private void sideViewXRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            foreach (object o in MainViewPort.Children)
                if (o is ModelVisual3D)
                {
                    ModelVisual3D? mdv = o as ModelVisual3D;
                    Model3DGroup? mdg = mdv.Content as Model3DGroup;
                    if (mdv == null || mdg == null)
                        continue;
                    RotateTransform3D rotateTransformX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -rotationAngleX));
                    mdg.Transform = rotateTransformX;
                    rotationAngleX = 0;
                    mdg.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0));
                }

            OrthographicCamera? orthographicCamera = FindName("threedOrthoCamera") as OrthographicCamera;
            threedCamera.Position = cameraPosition3;
            threedCamera.LookDirection = vector3DLookDirection3;
            threedCamera.UpDirection = vector3DUpDirection3;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(60, 0, 0);
            orthographicCamera.LookDirection = vector3DLookDirection3;
            orthographicCamera.UpDirection = vector3DUpDirection3;
        }

        private void sideViewYRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            foreach (object o in MainViewPort.Children)
                if (o is ModelVisual3D)
                {
                    ModelVisual3D? mdv = o as ModelVisual3D;
                    Model3DGroup? mdg = mdv.Content as Model3DGroup;
                    if (mdv == null || mdg == null)
                        continue;
                    RotateTransform3D rotateTransformX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -rotationAngleX));
                    mdg.Transform = rotateTransformX;
                    rotationAngleX = 0;
                    mdg.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0));
                }

            OrthographicCamera? orthographicCamera = FindName("threedOrthoCamera") as OrthographicCamera;
            threedCamera.Position = cameraPosition4;
            threedCamera.LookDirection = vector3DLookDirection4;
            threedCamera.UpDirection = vector3DUpDirection4;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(0, 0, 60);
            orthographicCamera.LookDirection = vector3DLookDirection4;
            orthographicCamera.UpDirection = vector3DUpDirection4;
        }

        private void MainViewPort_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentMousePosition = e.GetPosition(MainViewPort);
                double deltaX = currentMousePosition.X - previousMousePosition.X;
                double deltaY = currentMousePosition.Y - previousMousePosition.Y;

                // Calculate rotation angles based on deltaX and deltaY
                double rotationSpeed = 0.5; // Adjust as needed
                double yawAngle = deltaX * rotationSpeed;
                double pitchAngle = deltaY * rotationSpeed;

                foreach (object o in MainViewPort.Children)
                    if (o.GetType() == typeof(ModelVisual3D))
                    {
                        ModelVisual3D? mdv = o as ModelVisual3D;
                        Model3DGroup? mdg = mdv.Content as Model3DGroup;
                        if (mdv == null || mdg == null)
                        {
                            //MessageBox.Show("naspa");
                            continue;
                        }
                        rotationAngleX += deltaX;
                        rotationAngleY -= deltaY;
                        RotateTransform3D rotateTransformX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), rotationAngleX));
                        //RotateTransform3D rotateTransformY = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), rotationAngleY));
                        Transform3DGroup rotateTransformGroup = new Transform3DGroup();
                        rotateTransformGroup.Children.Add(rotateTransformX);
                        //rotateTransformGroup.Children.Add(rotateTransformY);
                        mdg.Transform = rotateTransformGroup;
                    }

                // Remember to update previousMousePosition
                previousMousePosition = currentMousePosition;
            }
        }

        private void palletColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (palletColorComboBox.SelectedIndex == 0)
                palletBrush = Brushes.BurlyWood;
            else if (palletColorComboBox.SelectedIndex == 1)
                palletBrush = Brushes.SteelBlue;
            else if (palletColorComboBox.SelectedIndex == 2)
                palletBrush = Brushes.Gold;
            else if (palletColorComboBox.SelectedIndex == 3)
                palletBrush = Brushes.Black;
            else if (palletColorComboBox.SelectedIndex == 4)
                palletBrush = Brushes.White;
        }

        private void boxColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (boxColorComboBox.SelectedIndex == 0)
                boxBrush = Brushes.SandyBrown;
            else if (boxColorComboBox.SelectedIndex == 1)
                boxBrush = Brushes.Chocolate;
            else if (boxColorComboBox.SelectedIndex == 2)
                boxBrush = Brushes.Sienna;
            else if (boxColorComboBox.SelectedIndex == 3)
                boxBrush = Brushes.Black;
            else if (boxColorComboBox.SelectedIndex == 4)
                boxBrush = Brushes.White;
        }
    }
}
