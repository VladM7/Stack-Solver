using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using OpenTK;
using static System.Windows.Input.Key;
using static System.Windows.Input.ModifierKeys;
using Microsoft.Office.Interop.Excel;
using Window = System.Windows.Window;
using Point = System.Windows.Point;
using Microsoft.Win32;
using System.Security.Cryptography;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBox = System.Windows.MessageBox;
using System.IO;

namespace Stack_Solver_v3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainViewPort.MouseMove += MainViewPort_MouseMove;
            MainViewPort.MouseLeftButtonDown += MainViewPort_MouseLeftButtonDown;
            MainViewPort.MouseLeftButtonUp += MainViewPort_MouseLeftButtonUp;
            boxColorComboBox.SelectedIndex = 1;
            palletColorComboBox.SelectedIndex = 2;
        }

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

        public int zPositionOffset = 80;

        bool generationError = false;

        Brush boxBrush = Brushes.SandyBrown;
        Brush palletBrush = Brushes.BurlyWood;
        Brush brush = Brushes.SandyBrown;

        private int cameraType = 0;

        public PerspectiveCamera myPCamera = new PerspectiveCamera();

        private Vector3D CalculateTriangleNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            Vector3D v0 = new Vector3D(
                p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3D v1 = new Vector3D(
                p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            return Vector3D.CrossProduct(v0, v1);
        }

        public Model3D geometryCreation(Point3D p0, Point3D p1, Point3D p2)
        {
            GeometryModel3D myGeometryModel = new GeometryModel3D();
            ModelVisual3D myModelVisual3D = new ModelVisual3D();

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();

            Point3DCollection myPositionCollection = new Point3DCollection();

            myPositionCollection.Add(p0);
            myPositionCollection.Add(p1);
            myPositionCollection.Add(p2);

            myMeshGeometry3D.Positions = myPositionCollection;

            Int32Collection myTriangleIndicesCollection = new Int32Collection();

            myTriangleIndicesCollection.Add(0);
            myTriangleIndicesCollection.Add(1);
            myTriangleIndicesCollection.Add(2);
            myMeshGeometry3D.TriangleIndices = myTriangleIndicesCollection;

            Vector3D Normal = CalculateTriangleNormal(p0, p1, p2);
            myMeshGeometry3D.Normals.Add(Normal);
            myMeshGeometry3D.Normals.Add(Normal);
            myMeshGeometry3D.Normals.Add(Normal);

            myGeometryModel.Geometry = myMeshGeometry3D;

            LinearGradientBrush myHorizontalGradient = new LinearGradientBrush();
            myHorizontalGradient.StartPoint = new Point(0, 0.5);
            myHorizontalGradient.EndPoint = new Point(1, 0.5);
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.25));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.75));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.LimeGreen, 1.0));

            //DiffuseMaterial myMaterial = new DiffuseMaterial(myHorizontalGradient);

            DiffuseMaterial myMaterial = new DiffuseMaterial(brush);
            myGeometryModel.Material = myMaterial;

            return myGeometryModel;
        }

        private void genTriangle(double posinit_x, double posinit_y, double posinit_z, double length, double width, double height, Model3DGroup triangle)
        {
            Point3D p0 = new Point3D(posinit_x + width, posinit_y + height, posinit_z + length);
            Point3D p1 = new Point3D(posinit_x, posinit_y + height, posinit_z + length);
            Point3D p2 = new Point3D(posinit_x, posinit_y, posinit_z + length);
            Point3D p3 = new Point3D(posinit_x + width, posinit_y, posinit_z + length);
            Point3D p4 = new Point3D(posinit_x + width, posinit_y + height, posinit_z);
            Point3D p5 = new Point3D(posinit_x, posinit_y + height, posinit_z);
            Point3D p6 = new Point3D(posinit_x, posinit_y, posinit_z);
            Point3D p7 = new Point3D(posinit_x + width, posinit_y, posinit_z);

            //front
            triangle.Children.Add(geometryCreation(p0, p1, p2));
            triangle.Children.Add(geometryCreation(p0, p2, p3));

            //back
            triangle.Children.Add(geometryCreation(p4, p7, p6));
            triangle.Children.Add(geometryCreation(p4, p6, p5));

            //right
            triangle.Children.Add(geometryCreation(p4, p0, p3));
            triangle.Children.Add(geometryCreation(p4, p3, p7));

            //left
            triangle.Children.Add(geometryCreation(p1, p5, p6));
            triangle.Children.Add(geometryCreation(p1, p6, p2));

            //top
            triangle.Children.Add(geometryCreation(p1, p0, p4));
            triangle.Children.Add(geometryCreation(p1, p4, p5));

            //bottom
            triangle.Children.Add(geometryCreation(p2, p6, p7));
            triangle.Children.Add(geometryCreation(p2, p7, p3));
        }

        private void generateStack(double size_x, double size_y, double size_z, int nr_x, int nr_y, int nr_z, double offset_x, double offset_y, double offset_z, ref Model3DGroup triangle)
        {
            //MessageBox.Show(offset_x + "x, " + offset_y + "y");
            for (int i = 0; i < nr_x; i++)
                for (int j = 0; j < nr_y; j++)
                    for (int k = 0; k < nr_z; k++)
                        genTriangle(offset_y + j * (size_y + 0.5), offset_z + k * (size_z + 0.5), offset_x + i * (size_x + 0.5), size_x, size_y, size_z, triangle);

            //genTriangle(0, 0, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);

            //genTriangle(0, 0, 1.02, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//y
            //genTriangle(1.02, 0, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//x
            //genTriangle(0, 1.02, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//z
            //genTriangle(0, 2.04, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);
        }

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

        /*private void Add_Shape(object sender, RoutedEventArgs e)
        {
            //MainViewPort.Children.Clear();
            ModelVisual3D Lights = new ModelVisual3D();
            DirectionalLight light = new DirectionalLight();
            light.Color = Colors.White;
            light.Direction = new Vector3D(-2, -3, -1);
            Lights.Content = light;
            this.MainViewPort.Children.Add(Lights);

            int nr_x = Convert.ToInt32(textBoxXNr.Text);
            int nr_z = Convert.ToInt32(textBoxYNr.Text);
            int nr_y = Convert.ToInt32(textBoxZNr.Text);

            double size_y = Convert.ToDouble(lengthBox.Text);
            double size_x = Convert.ToDouble(widthBox.Text);
            double size_z = Convert.ToDouble(heightBox.Text);

            Model3DGroup triangle = new Model3DGroup();

            for (int i = 0; i < nr_x; i++)
                for (int j = 0; j < nr_y; j++)
                    for (int k = 0; k < nr_z; k++)
                        genTriangle(j * (size_y + 0.1), k * (size_z + 0.1), i * (size_x + 0.1), size_x, size_y, size_z, triangle);

            ModelVisual3D Model = new ModelVisual3D();
            Model.Content = triangle;
            this.MainViewPort.Children.Add(Model);
        }*/

        private void readValues()
        {
            if (pL.Text == "" || pW.Text == "" || pH.Text == "" || pHlim.Text == ""
                || pWght.Text == "" || pWghtlim.Text == "" ||
                bL.Text == "" || bW.Text == "" || bH.Text == "" || bWght.Text == "")
                return;
            p.length = Convert.ToDouble(pL.Text);
            p.width = Convert.ToDouble(pW.Text);
            p.height = Convert.ToDouble(pH.Text);
            p.height_limit = Convert.ToDouble(pHlim.Text);
            p.weight = Convert.ToDouble(pWght.Text);
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
            if (pL.Text == "" || pW.Text == "" || pH.Text == "" || pHlim.Text == ""
                || pWght.Text == "" || pWghtlim.Text == "")
                return;
            p.length = Convert.ToDouble(pL.Text);
            p.width = Convert.ToDouble(pW.Text);
            p.height = Convert.ToDouble(pH.Text);
            p.height_limit = Convert.ToDouble(pHlim.Text);
            p.weight = Convert.ToDouble(pWght.Text);
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
            generateStack(pallet_len, pallet_width, p.height / 4, 1, 1, 1, -pallet_len / 2, -pallet_width / 2, zPositionOffset + 0.75 * p.height, ref triangle);

            generateStack(pallet_len, 10, p.height / 4, 1, 1, 1, -pallet_len / 2, -pallet_width / 2, zPositionOffset, ref triangle);
            generateStack(pallet_len, 10, p.height / 4, 1, 1, 1, -pallet_len / 2, -5, zPositionOffset, ref triangle);
            generateStack(pallet_len, 10, p.height / 4, 1, 1, 1, -pallet_len / 2, pallet_width / 2 - 10, zPositionOffset, ref triangle);

            //front
            generateStack(10, 10, p.height, 1, 1, 1, -pallet_len / 2, -pallet_width / 2, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, -pallet_len / 2, -5, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, -pallet_len / 2, pallet_width / 2 - 10, zPositionOffset, ref triangle);
            //middle
            generateStack(10, 10, p.height, 1, 1, 1, -5, -pallet_width / 2, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, -5, -5, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, -5, pallet_width / 2 - 10, zPositionOffset, ref triangle);
            //back
            generateStack(10, 10, p.height, 1, 1, 1, pallet_len / 2 - 10, -pallet_width / 2, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, pallet_len / 2 - 10, -5, zPositionOffset, ref triangle);
            generateStack(10, 10, p.height, 1, 1, 1, pallet_len / 2 - 10, pallet_width / 2 - 10, zPositionOffset, ref triangle);

            brush = boxBrush;
            generateStack(c.length, c.width, c.height, nrboxes1, max_boxes_width2, levels, offset_x - pallet_len / 2, offset_y + inset1 - pallet_width / 2, p.height + zPositionOffset, ref triangle);
            generateStack(c.width, c.length, c.height, nrboxes2, max_boxes_width1, levels, offset_x + xc - pallet_len / 2, offset_y + inset2 - pallet_width / 2, p.height + zPositionOffset, ref triangle);
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
                nr_levels = (int)((p.height_limit - p.height) / c.height);
            else
                nr_levels = (int)min((int)((p.height_limit - p.height) / c.height), (int)((p.weight_limit - p.weight) / (box_type_nr * c.weight)));
            if (nr_levels == 0)
            {
                resultTextBox.Text = "Error.";
                statusInfoBar.Severity = InfoBarSeverity.Error;
                statusInfoBar.Title = "Error";
                statusInfoBar.Message = "No levels can be placed on the pallet.";
                statusInfoBar.IsOpen = true;
                generationError = true;
                return;
            }

            eff_height = nr_levels * c.height + p.height;

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
                "cm\n" +
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

        private void compare_results()
        {
            if (c.height + p.height > p.height_limit)
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
                resultTextBox.Text = "Cargo exceeds pallet dimensions!";
                statusInfoBar.Severity = InfoBarSeverity.Error;
                statusInfoBar.Title = "Error";
                statusInfoBar.Message = "Cargo exceeds pallet dimensions.";
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

        private void calculateBtn_Click(object sender, RoutedEventArgs e)
        {
            generationError = false;
            clearViewport();
            zPositionOffset = Convert.ToInt16(ZPositionTextBox.Text);
            if (pickMultipleBoxSizes.IsChecked == true)
            {
                if (excelFilePath == null)
                {
                    statusInfoBar.Severity = InfoBarSeverity.Error;
                    statusInfoBar.Title = "Error";
                    statusInfoBar.Message = "Pick an Excel file first.";
                    statusInfoBar.IsOpen = true;
                    generationError = true;
                    return;
                }
                if (runAllCheckbox.IsChecked == true)
                    readExcelFile(0);
                else
                {
                    readPallets();
                    readExcelFile(1);
                }
            }
            else
            {
                if (runAllCheckbox.IsChecked == true)
                {
                    readBoxes();
                    run_all_tests();
                }
                else
                {
                    readValues();
                    compare_results();
                }
            }
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
            ShowAxes();
        }

        private double rotationAngleX = 0, rotationAngleY = 0;

        private void rotationBtn_Click(object sender, RoutedEventArgs e)
        {
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
                    rotationAngleX += 10;
                    RotateTransform3D rotateTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), rotationAngleX));
                    mdg.Transform = rotateTransform;
                }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                MessageBox.Show("Camera is not PerspectiveCamera");
            }
        }

        private void MainViewPort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            previousMousePosition = e.GetPosition(MainViewPort);
            MainViewPort.CaptureMouse();
        }

        private void MainViewPort_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainViewPort.ReleaseMouseCapture();
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

        private void excelBtn_Click(object sender, RoutedEventArgs e)
        {
            insertDataExcel(2);
        }

        private void insertDataExcel(int row)
        {
            Microsoft.Office.Interop.Excel.Application xlApp = new Microsoft.Office.Interop.Excel.Application();
            if (xlApp == null)
            {
                MessageBox.Show("Excel is not properly installed!");
                return;
            }
            Microsoft.Office.Interop.Excel._Workbook oWB;
            Microsoft.Office.Interop.Excel._Worksheet oSheet;
            oWB = xlApp.Workbooks.Add(Missing.Value);
            oSheet = (Microsoft.Office.Interop.Excel._Worksheet)oWB.ActiveSheet;

            for (int i = 3; i <= 6; i++)
                oSheet.Columns[i].ColumnWidth = 11;
            for (int i = 7; i <= 13; i++)
                oSheet.Columns[i].ColumnWidth = 17;
            oSheet.Columns[2].ColumnWidth = 18;
            oSheet.Columns[7].ColumnWidth = 21;
            oSheet.Columns[9].ColumnWidth = 21;
            oSheet.Columns[10].ColumnWidth = 29;
            oSheet.Columns[11].ColumnWidth = 30;
            oSheet.Columns[12].ColumnWidth = 20;
            oSheet.Columns[13].ColumnWidth = 20;
            oSheet.Columns[14].ColumnWidth = 20;
            oSheet.Columns[15].ColumnWidth = 15;
            oSheet.Columns[16].ColumnWidth = 21;
            oSheet.Columns[17].ColumnWidth = 13;

            oSheet.Rows[2].RowHeight = 45;

            oSheet.Cells[1, 1] = "Name";
            oSheet.Cells[1, 2] = "Description";
            oSheet.Cells[1, 3] = "Length (cm)";
            oSheet.Cells[1, 4] = "Width (cm)";
            oSheet.Cells[1, 5] = "Height (cm)";
            oSheet.Cells[1, 6] = "Weight (kg)";
            oSheet.Cells[1, 7] = "Total number of boxes";
            oSheet.Cells[1, 8] = "Number of levels";
            oSheet.Cells[1, 9] = "Number of boxes/level";
            oSheet.Cells[1, 10] = "Load dimensions (cm x cm x cm)";
            oSheet.Cells[1, 11] = "Pallet dimensions (cm x cm x cm)";
            oSheet.Cells[1, 12] = "Pallet length (cm)";
            oSheet.Cells[1, 13] = "Pallet width (cm)";
            oSheet.Cells[1, 14] = "Pallet height (cm)";
            oSheet.Cells[1, 15] = "Load weight (kg)";
            oSheet.Cells[1, 16] = "Total pallet weight (kg)";
            oSheet.Cells[1, 17] = "Efficiency (%)";

            oSheet.Cells[row, 1] = "Box";
            oSheet.Cells[row, 3] = c.length;
            oSheet.Cells[row, 4] = c.width;
            oSheet.Cells[row, 5] = c.height;
            oSheet.Cells[row, 6] = c.weight;
            oSheet.Cells[row, 7] = nr_levels * box_type_nr;
            oSheet.Cells[row, 8] = nr_levels;
            oSheet.Cells[row, 9] = box_type_nr;
            oSheet.Cells[row, 10] = length + "x" + width + "x" + (eff_height - p.height);
            oSheet.Cells[row, 11] = p.length + "x" + p.width + "x" + eff_height;
            oSheet.Cells[row, 12] = p.length;
            oSheet.Cells[row, 13] = p.width;
            oSheet.Cells[row, 14] = eff_height;
            oSheet.Cells[row, 15] = nr_levels * box_type_nr * c.weight;
            oSheet.Cells[row, 16] = nr_levels * box_type_nr * c.weight + p.weight;
            oSheet.Cells[row, 17] = Math.Round((area_occupied / aria_palet * 100), 2, MidpointRounding.AwayFromZero) + "%";

            oSheet.Cells[1, 1].EntireRow.Font.Bold = true;
            var tableRange = oSheet.get_Range("a1", "q2");
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeLeft).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeRight).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideHorizontal).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideVertical).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.BorderAround2();

            oWB.SaveAs("stack-solver.xlsx");
            oWB.Close(true, Missing.Value, Missing.Value);
            xlApp.Quit();
            MessageBox.Show("File saved in C:/Users/User/Documents/stack-solver.xlsx");
        }

        private string excelFilePath;

        private void openToolStripMenuItem_Click()
        {
            OpenFileDialog theDialog = new OpenFileDialog();
            theDialog.Title = "Open Excel File";
            theDialog.Filter = "Excel files|*.xlsx";
            theDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (theDialog.ShowDialog() == true)
            {
                excelFilePath = theDialog.FileName.ToString();
                excelFileLabel.Content = excelFilePath;
            }
        }

        private void chooseExcelFileButton_Click(object sender, RoutedEventArgs e)
        {
            openToolStripMenuItem_Click();
        }

        private void downloadSampleButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Office.Interop.Excel.Application xlApp = new Microsoft.Office.Interop.Excel.Application();
            if (xlApp == null)
            {
                MessageBox.Show("Excel is not properly installed!");
                return;
            }
            Microsoft.Office.Interop.Excel._Workbook oWB;
            Microsoft.Office.Interop.Excel._Worksheet oSheet;
            oWB = xlApp.Workbooks.Add(Missing.Value);
            oSheet = (Microsoft.Office.Interop.Excel._Worksheet)oWB.ActiveSheet;

            oSheet.Cells[1, 1] = "Box Length";
            oSheet.Cells[1, 2] = "Box Width";
            oSheet.Cells[1, 3] = "Box Height";
            oSheet.Cells[1, 4] = "Box Weight";
            oSheet.Columns[1].ColumnWidth = 20;
            oSheet.Columns[2].ColumnWidth = 20;
            oSheet.Columns[3].ColumnWidth = 20;
            oSheet.Columns[4].ColumnWidth = 20;

            oWB.SaveAs("stack-solver-sample.xlsx");
            oWB.Close(true, Missing.Value, Missing.Value);
            xlApp.Quit();
            MessageBox.Show("Sample file saved!");
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (pickMultipleBoxSizes.IsChecked == true)
            {
                chooseExcelFileButton.IsEnabled = true;
                downloadSampleButton.IsEnabled = true;
                bL.IsReadOnly = true;
                bW.IsReadOnly = true;
                bH.IsReadOnly = true;
                bWght.IsReadOnly = true;
            }
            else
            {
                chooseExcelFileButton.IsEnabled = false;
                downloadSampleButton.IsEnabled = false;
                bL.IsReadOnly = false;
                bW.IsReadOnly = false;
                bH.IsReadOnly = false;
                bWght.IsReadOnly = false;
            }
        }

        struct pallet_sizes
        {
            public double length, width, height, weight;
            public double areaMax1, areaMax2;
            public int areaMaxPos1, areaMaxPos2;
            public arii[] pareas1, pareas2;
        }

        pallet_sizes[] ps = new pallet_sizes[4];

        private void init_dimensions()
        {
            for (int i = 0; i < 3; i++)
            {
                ps[i].pareas1 = new arii[105];
                ps[i].pareas2 = new arii[105];
                ps[i].areaMax1 = 0;
                ps[i].areaMax2 = 0;
                ps[i].areaMaxPos1 = 0;
                ps[i].areaMaxPos2 = 0;
            }
            ps[0].length = 120;
            ps[0].width = 100;
            ps[0].height = 14.4;
            ps[0].weight = 33;
            ps[1].length = 120;
            ps[1].width = 80;
            ps[1].height = 14.5;
            ps[1].weight = 25;
            ps[2].length = 80;
            ps[2].width = 60;
            ps[2].height = 14.4;
            ps[2].weight = 9.5;
        }

        public void run_all_tests()
        {
            init_dimensions();
            for (int i = 0; i < 3; i++)
            {
                calcul_arie(ps[i].length, ps[i].width, 1, ref ps[i].areaMax1, ref ps[i].areaMaxPos1, ref ps[i].pareas1);
                calcul_arie(ps[i].width, ps[i].length, 2, ref ps[i].areaMax2, ref ps[i].areaMaxPos2, ref ps[i].pareas2);
            }
            double vmax = 0, best_eff = 0;
            int palType = -1, orientType = -1;
            if (bestEffSolutionCheckbox.IsChecked == true)
            {
                for (int i = 0; i < 3; i++)
                {
                    aria_palet = ps[i].length * ps[i].width;
                    if (ps[i].areaMax1 / aria_palet * 100 > best_eff)
                    {
                        best_eff = ps[i].areaMax1 / aria_palet * 100;
                        palType = i;
                        orientType = 1;
                    }
                    if (ps[i].areaMax2 / aria_palet * 100 > best_eff)
                    {
                        best_eff = ps[i].areaMax2 / aria_palet * 100;
                        palType = i;
                        orientType = 2;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    aria_palet = ps[i].length * ps[i].width;
                    if (ps[i].areaMax1 >= vmax)
                    {
                        vmax = ps[i].areaMax1;
                        palType = i;
                        orientType = 1;
                    }
                    if (ps[i].areaMax2 > vmax)
                    {
                        vmax = ps[i].areaMax2;
                        palType = i;
                        orientType = 2;
                    }
                }
            }
            p.length = ps[palType].length;
            p.width = ps[palType].width;
            p.weight = ps[palType].weight;
            p.height = ps[palType].height;
            p.height_limit = Convert.ToDouble(pHlim.Text);
            if (orientType == 1)
                generateDrawing(ps[palType].length, ps[palType].width, 1, (int)ps[palType].areaMax1, ps[palType].areaMaxPos1, ps[palType].pareas1);
            else
                generateDrawing(ps[palType].width, ps[palType].length, 2, (int)ps[palType].areaMax2, ps[palType].areaMaxPos2, ps[palType].pareas2);
        }

        private void runAllCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (runAllCheckbox.IsChecked == true)
            {
                pL.IsReadOnly = true;
                pL.IsEnabled = false;
                pW.IsReadOnly = true;
                pW.IsEnabled = false;
                bestEffSolutionCheckbox.IsEnabled = true;
            }
            else
            {
                pL.IsReadOnly = false;
                pL.IsEnabled = true;
                pW.IsReadOnly = false;
                pW.IsEnabled = true;
                bestEffSolutionCheckbox.IsEnabled = false;
            }
        }

        private void runAllCheckbox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void pickMultipleBoxSizes_Checked(object sender, RoutedEventArgs e)
        {
            boxSizeComboBox.IsEnabled = true;
            bL.IsEnabled = false;
            bW.IsEnabled = false;
            bH.IsEnabled = false;
            bWght.IsEnabled = false;
        }

        private double[][] allBoxSizesFromExcel = new double[4][];

        private void readExcelFile(int mode)
        {
            allBoxSizesFromExcel = new double[4][];
            for (int i = 0; i < 4; i++)
            {
                allBoxSizesFromExcel[i] = new double[1001];
            }

            boxSizeComboBox.Items.Clear();
            boxSizeComboBox.Items.Add("None");
            boxSizeComboBox.SelectedIndex = 0;
            Microsoft.Office.Interop.Excel.Application excel = new Microsoft.Office.Interop.Excel.Application();
            Workbook wb;
            Worksheet ws;
            wb = excel.Workbooks.Open(excelFilePath);
            ws = wb.Worksheets[1];

            Microsoft.Office.Interop.Excel.Application excelf = new Microsoft.Office.Interop.Excel.Application();
            Workbook oWB;
            Worksheet oSheet;
            oWB = excelf.Workbooks.Add(Missing.Value);
            oSheet = oWB.Worksheets[1];

            for (int i = 3; i <= 6; i++)
                oSheet.Columns[i].ColumnWidth = 11;
            for (int i = 7; i <= 13; i++)
                oSheet.Columns[i].ColumnWidth = 17;
            oSheet.Columns[2].ColumnWidth = 18;
            oSheet.Columns[7].ColumnWidth = 21;
            oSheet.Columns[9].ColumnWidth = 21;
            oSheet.Columns[10].ColumnWidth = 29;
            oSheet.Columns[11].ColumnWidth = 30;
            oSheet.Columns[12].ColumnWidth = 20;
            oSheet.Columns[13].ColumnWidth = 20;
            oSheet.Columns[14].ColumnWidth = 20;
            oSheet.Columns[15].ColumnWidth = 15;
            oSheet.Columns[16].ColumnWidth = 21;
            oSheet.Columns[17].ColumnWidth = 13;

            oSheet.Rows[2].RowHeight = 45;

            oSheet.Cells[1, 1] = "Name";
            oSheet.Cells[1, 2] = "Description";
            oSheet.Cells[1, 3] = "Length (cm)";
            oSheet.Cells[1, 4] = "Width (cm)";
            oSheet.Cells[1, 5] = "Height (cm)";
            oSheet.Cells[1, 6] = "Weight (kg)";
            oSheet.Cells[1, 7] = "Total number of boxes";
            oSheet.Cells[1, 8] = "Number of levels";
            oSheet.Cells[1, 9] = "Number of boxes/level";
            oSheet.Cells[1, 10] = "Load dimensions (cm x cm x cm)";
            oSheet.Cells[1, 11] = "Pallet dimensions (cm x cm x cm)";
            oSheet.Cells[1, 12] = "Pallet length (cm)";
            oSheet.Cells[1, 13] = "Pallet width (cm)";
            oSheet.Cells[1, 14] = "Pallet height (cm)";
            oSheet.Cells[1, 15] = "Load weight (kg)";
            oSheet.Cells[1, 16] = "Total pallet weight (kg)";
            oSheet.Cells[1, 17] = "Efficiency (%)";
            int lastRow = ws.Cells.SpecialCells(XlCellType.xlCellTypeLastCell, Type.Missing).Row;
            for (int row = 2; row <= lastRow; row++)
            {
                if (ws.Cells[row, 1].Value2 == null || ws.Cells[row, 2].Value2 == null || ws.Cells[row, 3].Value2 == null || ws.Cells[row, 4].Value2 == null)
                    continue;
                c.length = double.Parse(ws.Cells[row, 1].Value2.ToString());
                c.width = double.Parse(ws.Cells[row, 2].Value2.ToString());
                c.height = double.Parse(ws.Cells[row, 3].Value2.ToString());
                c.weight = double.Parse(ws.Cells[row, 4].Value2.ToString());
                //MessageBox.Show(double.Parse(ws.Cells[row, 1].Value2.ToString()).ToString());
                boxSizeComboBox.Items.Add(c.length + " x " + c.width + " x " + c.height + "cm³ / " + c.weight + "kg");

                allBoxSizesFromExcel[0][row - 2] = c.length;
                allBoxSizesFromExcel[1][row - 2] = c.width;
                allBoxSizesFromExcel[2][row - 2] = c.height;
                allBoxSizesFromExcel[3][row - 2] = c.weight;

                if (mode == 0)
                    run_all_tests();
                else
                    compare_results();
                clearViewport();

                oSheet.Cells[row, 1] = "Box" + (row - 1).ToString();
                oSheet.Cells[row, 3] = c.length;
                oSheet.Cells[row, 4] = c.width;
                oSheet.Cells[row, 5] = c.height;
                oSheet.Cells[row, 6] = c.weight;
                oSheet.Cells[row, 7] = nr_levels * box_type_nr;
                oSheet.Cells[row, 8] = nr_levels;
                oSheet.Cells[row, 9] = box_type_nr;
                oSheet.Cells[row, 10] = length + "x" + width + "x" + (eff_height - p.height);
                oSheet.Cells[row, 11] = p.length + "x" + p.width + "x" + eff_height;
                oSheet.Cells[row, 12] = p.length;
                oSheet.Cells[row, 13] = p.width;
                oSheet.Cells[row, 14] = eff_height;
                oSheet.Cells[row, 15] = nr_levels * box_type_nr * c.weight;
                oSheet.Cells[row, 16] = nr_levels * box_type_nr * c.weight + p.weight;
                oSheet.Cells[row, 17] = Math.Round((area_occupied / aria_palet * 100), 2, MidpointRounding.AwayFromZero) + "%";
            }

            oSheet.Cells[1, 1].EntireRow.Font.Bold = true;
            var tableRange = oSheet.get_Range("a1", "q" + lastRow.ToString());
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeLeft).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlEdgeRight).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideHorizontal).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.Borders.get_Item(Microsoft.Office.Interop.Excel.XlBordersIndex.xlInsideVertical).LineStyle = Microsoft.Office.Interop.Excel.XlLineStyle.xlContinuous;
            tableRange.BorderAround2();

            wb.Close(true, Missing.Value, Missing.Value);
            excel.Quit();

            oWB.SaveAs("stack-solver.xlsx");
            oWB.Close(true, Missing.Value, Missing.Value);
            excelf.Quit();
            MessageBox.Show("File saved!");
            resultRunTextBlock.Text = "Select box type to display.";
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

        private void palletColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (palletColorComboBox.SelectedIndex == 0)
                palletBrush = Brushes.BurlyWood;
            else if (palletColorComboBox.SelectedIndex == 1)
                palletBrush = Brushes.SaddleBrown;
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

        private void pL_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            keyboardFocusSelectAll(sender, e);
        }

        private void pickMultipleBoxSizes_Unchecked(object sender, RoutedEventArgs e)
        {
            boxSizeComboBox.IsEnabled = false;
            bL.IsEnabled = true;
            bW.IsEnabled = true;
            bH.IsEnabled = true;
            bWght.IsEnabled = true;
        }

        private void boxSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            clearViewport();
            if (boxSizeComboBox.SelectedIndex == 0)
            {
                resultRunTextBlock.Text = "Select box type to display.";
                return;
            }
            c.length = allBoxSizesFromExcel[0][boxSizeComboBox.SelectedIndex - 1];
            c.width = allBoxSizesFromExcel[1][boxSizeComboBox.SelectedIndex - 1];
            c.height = allBoxSizesFromExcel[2][boxSizeComboBox.SelectedIndex - 1];
            c.weight = allBoxSizesFromExcel[3][boxSizeComboBox.SelectedIndex - 1];
            if (runAllCheckbox.IsChecked == true)
                run_all_tests();
            else
                compare_results();
        }

        private void saveImageButton_Click(object sender, RoutedEventArgs e)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(2560, 1440, 200, 200, PixelFormats.Pbgra32);
            bmp.Render(MainViewPort);
            PngBitmapEncoder png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bmp));
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Image|*.png";
            saveFileDialog.Title = "Save output";
            saveFileDialog.FileName = "stack-solver-image.png";
            if (saveFileDialog.ShowDialog() == true)
            {
                using (Stream stm = File.Create(saveFileDialog.FileName))
                {
                    png.Save(stm);
                }
            }
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

        private void ShowAxes()
        {
            var lineX1 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + p.height, 1 + p.width/2),
                       new Point3D( 500, zPositionOffset + p.height, 1 + p.width/2),
                       new Point3D(-500, zPositionOffset + p.height, p.width/2),
                       new Point3D( 500, zPositionOffset + p.height, p.width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX2 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + p.height, 1 - p.width/2),
                       new Point3D( 500, zPositionOffset + p.height, 1 - p.width/2),
                       new Point3D(-500, zPositionOffset + p.height, -p.width/2),
                       new Point3D( 500, zPositionOffset + p.height, -p.width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX1Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + eff_height, 1 + p.width/2),
                       new Point3D( 500, zPositionOffset + eff_height, 1 + p.width/2),
                       new Point3D(-500, zPositionOffset + eff_height, p.width/2),
                       new Point3D( 500, zPositionOffset + eff_height, p.width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX2Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + eff_height, 1 - p.width/2),
                       new Point3D( 500, zPositionOffset + eff_height, 1 - p.width/2),
                       new Point3D(-500, zPositionOffset + eff_height, -p.width/2),
                       new Point3D( 500, zPositionOffset + eff_height, -p.width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };

            RotateTransform3D rotateTransformX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90));
            lineX1.Transform = rotateTransformX;
            lineX2.Transform = rotateTransformX;
            lineX1Above.Transform = rotateTransformX;
            lineX2Above.Transform = rotateTransformX;

            var lineY1 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + p.height, 1 + p.length/2),
                       new Point3D( 500, zPositionOffset + p.height, 1 + p.length/2),
                       new Point3D(-500, zPositionOffset + p.height, p.length/2),
                       new Point3D( 500, zPositionOffset + p.height, p.length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY2 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + p.height, 1 - p.length/2),
                       new Point3D( 500, zPositionOffset + p.height, 1 - p.length/2),
                       new Point3D(-500, zPositionOffset + p.height, -p.length / 2),
                       new Point3D( 500, zPositionOffset + p.height, -p.length / 2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY1Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + eff_height, 1 + p.length/2),
                       new Point3D( 500, zPositionOffset + eff_height, 1 + p.length/2),
                       new Point3D(-500, zPositionOffset + eff_height, p.length/2),
                       new Point3D( 500, zPositionOffset + eff_height, p.length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY2Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, zPositionOffset + eff_height, 1 - p.length/2),
                       new Point3D( 500, zPositionOffset + eff_height, 1 - p.length/2),
                       new Point3D(-500, zPositionOffset + eff_height, -p.length / 2),
                       new Point3D( 500, zPositionOffset + eff_height, -p.length / 2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };

            var lineXMeasurement = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-p.length/2, zPositionOffset + p.height, 50 + 2 + p.width/2),
                       new Point3D( p.length/2, zPositionOffset + p.height, 50 + 2 + p.width/2),
                       new Point3D(-p.length/2, zPositionOffset + p.height, 50 + p.width/2),
                       new Point3D( p.length/2, zPositionOffset + p.height, 50 + p.width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Blue)
            };
            lineXMeasurement.Transform = rotateTransformX;
            var lineYMeasurement = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-p.width/2, zPositionOffset + p.height, 50 + 2 + p.length/2),
                       new Point3D( p.width/2, zPositionOffset + p.height, 50 + 2 + p.length/2),
                       new Point3D(-p.width/2, zPositionOffset + p.height, 50 + p.length/2),
                       new Point3D( p.width/2, zPositionOffset + p.height, 50 + p.length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Blue)
            };

            var cube = new GeometryModel3D();
            var mat = new DiffuseMaterial();
            var vb = new VisualBrush();
            var st = new StackPanel();
            st.Children.Add(new System.Windows.Controls.TextBlock(new Run("Length: " + p.length + " cm")));
            vb.Visual = st;
            mat.Brush = vb;
            cube.Material = mat;
            cube.Geometry=new MeshGeometry3D()
            {
                Positions = { 
                    new Point3D(1, 1, 1),
                    new Point3D(-1, 1, 1),
                    new Point3D(-1, -1, 1),
                    new Point3D(1, -1, 1),
                },
                TriangleIndices = { 
                    0, 1, 2,
                    0, 2, 3
                },
                TextureCoordinates = {
                    new Point(1, 1),
                    new Point(0, 1),
                    new Point(0, 0),
                    new Point(1, 0)
                }
            };

            foreach (object o in MainViewPort.Children)
                if (o is ModelVisual3D)
                {
                    ModelVisual3D? mdv = o as ModelVisual3D;
                    Model3DGroup? mdg = mdv.Content as Model3DGroup;
                    if (mdv == null || mdg == null)
                        continue;
                    mdg.Children.Add(lineX1);
                    mdg.Children.Add(lineX2);
                    mdg.Children.Add(lineX1Above);
                    mdg.Children.Add(lineX2Above);
                    mdg.Children.Add(lineY1);
                    mdg.Children.Add(lineY2);
                    mdg.Children.Add(lineY1Above);
                    mdg.Children.Add(lineY2Above);
                    mdg.Children.Add(lineXMeasurement);
                    mdg.Children.Add(lineYMeasurement);
                    mdg.Children.Add(cube);
                    return;
                }
        }
    }
}
