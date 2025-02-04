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
using Microsoft.Office.Interop.Excel;
using Window = System.Windows.Window;
using Point = System.Windows.Point;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBox = System.Windows.MessageBox;
using System.IO;
using Stack_Solver_v3;
using System.Threading;

namespace Stack_Solver
{
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

        PalletClass pallet = new PalletClass();
        BoxClass box = new BoxClass();

        AreaClass[] area1 = new AreaClass[105];
        AreaClass[] area2 = new AreaClass[105];

        public int maxBoxesLength;
        public int maxBoxesWidth1;
        public int maxBoxesWidth2;

        public double palletArea;
        public double boxArea;

        public int nrBoxesPerLevel;
        public int nrLevels;
        public double totalHeight;

        public double cargoLength;
        public double cargoWidth;
        double maxCargoAreaOccupied;

        bool generationError = false;

        private int cameraType = 0;

        public PerspectiveCamera myPCamera = new PerspectiveCamera();

        private Rendering render = new Rendering();
        private ExcelOps excelOps = new ExcelOps();
        private ViewClass viewClass = new ViewClass();

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

        private int verifPalletValidity()
        {
            if (pL.Text == "" || pW.Text == "" || pH.Text == "" || pHlim.Text == ""
                || pWght.Text == "" || pWghtlim.Text == "")
            {
                infoBarMessage(true, "Please fill in all the fields.", "Error", InfoBarSeverity.Error);
                return 0;
            }
            if (Convert.ToDouble(pL.Text) <= 0 || Convert.ToDouble(pW.Text) <= 0 || Convert.ToDouble(pH.Text) <= 0 || Convert.ToDouble(pHlim.Text) <= 0
                || Convert.ToDouble(pWght.Text) <= 0 || Convert.ToDouble(pWghtlim.Text) <= 0)
            {
                infoBarMessage(true, "All fields must contain positive values.", "Error", InfoBarSeverity.Error);
                return 0;
            }
            return 1;
        }

        private int verifBoxValidity()
        {
            if (bL.Text == "" || bW.Text == "" || bH.Text == "" || bWght.Text == "" || pWghtlim.Text == "")
            {
                infoBarMessage(true, "Please fill in all the fields.", "Error", InfoBarSeverity.Error);
                return 0;
            }
            if (Convert.ToDouble(bL.Text) <= 0 || Convert.ToDouble(bW.Text) <= 0 || Convert.ToDouble(bH.Text) <= 0 || Convert.ToDouble(bWght.Text) <= 0 || Convert.ToDouble(pWghtlim.Text) <= 0)
            {
                infoBarMessage(true, "All fields must contain positive values.", "Error", InfoBarSeverity.Error);
                return 0;
            }
            return 1;
        }

        private void readValues()
        {
            if (verifPalletValidity() == 0 || verifBoxValidity() == 0)
                return;
            pallet.Length = Convert.ToDouble(pL.Text);
            pallet.Width = Convert.ToDouble(pW.Text);
            pallet.Height = Convert.ToDouble(pH.Text);
            pallet.MaxHeight = Convert.ToDouble(pHlim.Text);
            pallet.Weight = Convert.ToDouble(pWght.Text);
            pallet.MaxWeight = Convert.ToDouble(pWghtlim.Text);

            box.Length = Convert.ToDouble(bL.Text);
            box.Width = Convert.ToDouble(bW.Text);
            box.Height = Convert.ToDouble(bH.Text);
            box.Weight = Convert.ToDouble(bWght.Text);
        }

        private void readBoxes()
        {
            if (verifBoxValidity() == 0)
                return;
            pallet.MaxWeight = Convert.ToDouble(pWghtlim.Text);
            box.Length = Convert.ToDouble(bL.Text);
            box.Width = Convert.ToDouble(bW.Text);
            box.Height = Convert.ToDouble(bH.Text);
            box.Weight = Convert.ToDouble(bWght.Text);
        }

        private void readPallets()
        {
            if (verifPalletValidity() == 0)
                return;
            pallet.Length = Convert.ToDouble(pL.Text);
            pallet.Width = Convert.ToDouble(pW.Text);
            pallet.Height = Convert.ToDouble(pH.Text);
            pallet.MaxHeight = Convert.ToDouble(pHlim.Text);
            pallet.Weight = Convert.ToDouble(pWght.Text);
            pallet.MaxWeight = Convert.ToDouble(pWghtlim.Text);
        }

        double max(double a, double b)
        {
            return Math.Max(a, b);
        }

        double min(double a, double b)
        {
            return Math.Min(a, b);
        }

        private void calcul_arie(double pallet_len, double pallet_width, int var, ref double maxCargoAreaOccupied, ref int nrb, ref AreaClass[] aria)
        {
            palletArea = pallet_width * pallet_len;
            boxArea = box.Length * box.Width;

            maxBoxesLength = (int)(pallet_len / box.Length);
            maxBoxesWidth1 = (int)(pallet_width / box.Length);
            maxBoxesWidth2 = (int)(pallet_width / box.Width);

            for (int nr_boxes1 = maxBoxesLength; nr_boxes1 >= 0; nr_boxes1--)
            {
                double diff = pallet_len - nr_boxes1 * box.Length;
                int nr_boxes2 = (int)(diff / box.Width) * maxBoxesWidth1;
                if (aria[nr_boxes1] == null) aria[nr_boxes1] = new AreaClass();
                aria[nr_boxes1].cargoArea = boxArea * (nr_boxes1 * maxBoxesWidth2 + nr_boxes2);
                aria[nr_boxes1].nrBoxesOrientation1 = nr_boxes1 * maxBoxesWidth2;
                aria[nr_boxes1].nrBoxesOrientation2 = nr_boxes2;
            }

            nrBoxesPerLevel = -1;
            for (int i = 0; i <= maxBoxesLength; i++)
                if (aria[i].cargoArea > maxCargoAreaOccupied)
                {
                    maxCargoAreaOccupied = aria[i].cargoArea;
                    nrBoxesPerLevel = aria[i].nrBoxesOrientation1 + aria[i].nrBoxesOrientation2;
                    nrb = i;
                }
        }

        private void generateDrawing(double pallet_len, double pallet_width, int var, int maxCargoAreaOccupied, int nrb, AreaClass[] aria)
        {
            palletArea = pallet_width * pallet_len;
            boxArea = box.Length * box.Width;

            maxBoxesLength = (int)(pallet_len / box.Length);
            maxBoxesWidth1 = (int)(pallet_width / box.Length);
            maxBoxesWidth2 = (int)(pallet_width / box.Width);

            nrBoxesPerLevel = aria[nrb].nrBoxesOrientation1 + aria[nrb].nrBoxesOrientation2;

            if (pallet.MaxWeight == 0)
                nrLevels = (int)((pallet.MaxHeight - pallet.Height) / box.Height);
            else
                nrLevels = (int)min((int)((pallet.MaxHeight - pallet.Height) / box.Height), (int)((pallet.MaxWeight - pallet.Weight) / (nrBoxesPerLevel * box.Weight)));
            
            if (nrLevels == 0)
            {
                infoBarMessage(true, "No levels can be placed on the pallet.", "Error", InfoBarSeverity.Error);
                resultTextBox.Text = "Error.";
                return;
            }

            totalHeight = nrLevels * box.Height + pallet.Height;

            if (maxBoxesWidth1 == 0)
                cargoLength = aria[nrb].nrBoxesOrientation1 / maxBoxesWidth2 * box.Length;
            else if (maxBoxesWidth2 == 0)
                cargoLength = aria[nrb].nrBoxesOrientation2 / maxBoxesWidth1 * box.Width;
            else
                cargoLength = aria[nrb].nrBoxesOrientation1 / maxBoxesWidth2 * box.Length + aria[nrb].nrBoxesOrientation2 / maxBoxesWidth1 * box.Width;
            if (aria[nrb].nrBoxesOrientation1 == 0)
                cargoWidth = maxBoxesWidth1 * box.Length;
            else if (aria[nrb].nrBoxesOrientation2 == 0)
                cargoWidth = maxBoxesWidth2 * box.Width;
            else
                cargoWidth = max(maxBoxesWidth1 * box.Length, maxBoxesWidth2 * box.Width);

            double offsetX, offsetY;

            if (var == 1)
            {
                offsetX = (pallet.Length - cargoLength) / 2;
                offsetY = (pallet.Width - cargoWidth) / 2;
            }
            else
            {
                offsetX = (pallet.Width - cargoLength) / 2;
                offsetY = (pallet.Length - cargoWidth) / 2;
            }

            int inset = 0, insetType = 0;
            if (aria[nrb].nrBoxesOrientation1 != 0 && aria[nrb].nrBoxesOrientation2 != 0)
            {
                if (maxBoxesWidth1 * box.Length > maxBoxesWidth2 * box.Width)
                {
                    inset = (int)(maxBoxesWidth1 * box.Length - maxBoxesWidth2 * box.Width) / 2;
                    insetType = 1;
                }
                else
                {
                    inset = (int)(maxBoxesWidth2 * box.Width - maxBoxesWidth1 * box.Length) / 2;
                    insetType = 2;
                }
            }

            int start_x_above = 200;
            int start_y_above = 161;

            int start_x_lateral1 = (int)(230 + max(pallet.Length, pallet.Width));
            int start_x_lateral2 = start_x_lateral1 + (int)(aria[nrb].nrBoxesOrientation1 / maxBoxesWidth2 * box.Length);
            int start_y_lateral = 161 + (int)totalHeight;

            start_x_above += (int)offsetX;
            start_y_above += (int)offsetY;

            string generationResult = "Area occupied: " + maxCargoAreaOccupied + "cm³\nUnoccupied space: "
                + (palletArea - maxCargoAreaOccupied)
                + "cm²\nEfficiency: " + Math.Round((maxCargoAreaOccupied / palletArea * 100), 2, MidpointRounding.AwayFromZero) + "%\n\nPallet type: " +
                max(pallet_len, pallet_width) + "x" + min(pallet_len, pallet_width) +
                "cm²\nBox type: " + box.Length + "x" + box.Width + "x" + box.Height + "cm³ / " + box.Weight + "kg\n" +
                "_________________________________________\n\n" +
                "Number of boxes / level: " + nrBoxesPerLevel
                + "\nNumber of levels: "
                + nrLevels + "\nEffective height: " + Math.Round(totalHeight, 2) + "cm\nLoad dimensions: " + Math.Round(cargoLength, 2) + "x" + Math.Round(cargoWidth, 2) + "x"
                + Math.Round((totalHeight - pallet.Height), 2)
                + "cm³\nPallet dimensions: " + pallet_len + "x" + pallet_width + "x"
                + Math.Round(totalHeight, 2) + "cm³\nOffset (x, y): " + Math.Round(offsetX, 2) + "cm, " + Math.Round(offsetY, 2) +
                "cm\n" +
                "_________________________________________\n\n" +
                "Load weight: " + (nrLevels * nrBoxesPerLevel * box.Weight) + "kg\nTotal weight: "
                + (nrLevels * nrBoxesPerLevel * box.Weight + pallet.Weight)
                + "kg\nWeight / level: " + nrBoxesPerLevel * box.Weight +
                "kg\nTotal number of boxes: " + (nrLevels * nrBoxesPerLevel) + "\n\n";
            resultRunTextBlock.Text = generationResult;
            render.draw3D(ref MainViewPort, pallet, box, aria, nrb, pallet_len, pallet_width, nrLevels, inset, insetType, offsetX, offsetY, maxBoxesWidth1, maxBoxesWidth2);
        }

        private void infoBarMessage(bool error, string infoBarText, string infoBarTitle, InfoBarSeverity infoBarSeverity)
        {
            if (error)
            {
                resultRunTextBlock.Text = "Generation failed: " + infoBarText;
                generationError = true;
            }
            statusInfoBar.Severity = infoBarSeverity;
            statusInfoBar.Title = infoBarTitle;
            statusInfoBar.Message = infoBarText;
            statusInfoBar.IsOpen = true;
        }

        public void compare_results()
        {
            if (box.Height + pallet.Height > pallet.MaxHeight)
            {
                infoBarMessage(true, "Cargo exceeds height limit.", "Error", InfoBarSeverity.Error);
                return;
            }
            if (max(box.Length, box.Width) > max(pallet.Length, pallet.Width))
            {
                infoBarMessage(true, "Cargo exceeds pallet dimensions.", "Error", InfoBarSeverity.Error);
                return;
            }
            double areaMax1 = 0, areaMax2 = 0;
            int areaMaxPos1 = 0, areaMaxPos2 = 0;
            calcul_arie(pallet.Length, pallet.Width, 1, ref areaMax1, ref areaMaxPos1, ref area1);
            calcul_arie(pallet.Width, pallet.Length, 2, ref areaMax2, ref areaMaxPos2, ref area2);
            if (areaMax1 >= areaMax2)
                generateDrawing(pallet.Length, pallet.Width, 1, (int)areaMax1, areaMaxPos1, area1);
            else
                generateDrawing(pallet.Width, pallet.Length, 2, (int)areaMax2, areaMaxPos2, area2);
        }

        public void clearViewport()
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
            render.zPositionOffset = Convert.ToInt16(ZPositionTextBox.Text);
            if (pickMultipleBoxSizes.IsChecked == true)
            {
                if (excelOps.excelFilePath == null)
                {
                    infoBarMessage(true, "Pick an Excel file first.", "Error", InfoBarSeverity.Error);
                    return;
                }
                if (runAllCheckbox.IsChecked == true)
                {
                    boxSizeComboBox.Items.Clear();
                    boxSizeComboBox.Items.Add("None");
                    boxSizeComboBox.SelectedIndex = 0;
                    excelOps.readExcelFile(0, ref boxSizeComboBox, pallet, box, nrLevels, totalHeight, palletArea, maxCargoAreaOccupied, nrBoxesPerLevel, cargoLength, cargoWidth);
                    resultRunTextBlock.Text = "Select box type to display.";
                }
                else
                {
                    readPallets();
                    if (generationError == false)
                    {
                        boxSizeComboBox.Items.Clear();
                        boxSizeComboBox.Items.Add("None");
                        boxSizeComboBox.SelectedIndex = 0;
                        excelOps.readExcelFile(1, ref boxSizeComboBox, pallet, box, nrLevels, totalHeight, palletArea, maxCargoAreaOccupied, nrBoxesPerLevel, cargoLength, cargoWidth);
                        resultRunTextBlock.Text = "Select box type to display.";
                    }
                }
            }
            else
            {
                if (runAllCheckbox.IsChecked == true)
                {
                    readBoxes();
                    if (generationError == false)
                        run_all_tests();
                }
                else
                {
                    readValues();
                    if (generationError == false)
                        compare_results();
                }
            }
            excelBtn.IsEnabled = true;
            //readValues();
            //compare_results();
            if (generationError == false)
            {
                infoBarMessage(false, "Generation complete.", "Success", InfoBarSeverity.Success);
                saveImageButton.IsEnabled = true;
                excelBtn.IsEnabled = true;
            }
            else
            {
                saveImageButton.IsEnabled = false;
                excelBtn.IsEnabled = false;
            }
            //ShowAxes();
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
            excelOps.insertDataExcel(2, pallet, box, nrLevels, totalHeight, palletArea, maxCargoAreaOccupied, nrBoxesPerLevel, cargoLength, cargoWidth);
        }

        private void openToolStripMenuItem_Click()
        {
            OpenFileDialog theDialog = new OpenFileDialog();
            theDialog.Title = "Open Excel File";
            theDialog.Filter = "Excel files|*.xlsx";
            theDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (theDialog.ShowDialog() == true)
            {
                excelOps.excelFilePath = theDialog.FileName.ToString();
                excelFileLabel.Content = excelOps.excelFilePath;
            }
        }

        private void chooseExcelFileButton_Click(object sender, RoutedEventArgs e)
        {
            openToolStripMenuItem_Click();
        }

        private void downloadSampleButton_Click(object sender, RoutedEventArgs e)
        {
            excelOps.createSampleFile();
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
            public AreaClass[] pareas1, pareas2;
        }

        pallet_sizes[] ps = new pallet_sizes[4];

        private void init_dimensions()
        {
            for (int i = 0; i < 3; i++)
            {
                ps[i].pareas1 = new AreaClass[105];
                ps[i].pareas2 = new AreaClass[105];
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
            maxCargoAreaOccupied = 0;
            double best_eff = 0;
            int palType = -1, orientType = -1;
            if (bestEffSolutionCheckbox.IsChecked == true)
            {
                for (int i = 0; i < 3; i++)
                {
                    palletArea = ps[i].length * ps[i].width;
                    if (ps[i].areaMax1 / palletArea * 100 > best_eff)
                    {
                        best_eff = ps[i].areaMax1 / palletArea * 100;
                        palType = i;
                        orientType = 1;
                    }
                    if (ps[i].areaMax2 / palletArea * 100 > best_eff)
                    {
                        best_eff = ps[i].areaMax2 / palletArea * 100;
                        palType = i;
                        orientType = 2;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    palletArea = ps[i].length * ps[i].width;
                    if (ps[i].areaMax1 >= maxCargoAreaOccupied)
                    {
                        maxCargoAreaOccupied = ps[i].areaMax1;
                        palType = i;
                        orientType = 1;
                    }
                    if (ps[i].areaMax2 > maxCargoAreaOccupied)
                    {
                        maxCargoAreaOccupied = ps[i].areaMax2;
                        palType = i;
                        orientType = 2;
                    }
                }
            }
            pallet.Length = ps[palType].length;
            pallet.Width = ps[palType].width;
            pallet.Weight = ps[palType].weight;
            pallet.Height = ps[palType].height;
            pallet.MaxHeight = Convert.ToDouble(pHlim.Text);
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

        private void pickMultipleBoxSizes_Checked(object sender, RoutedEventArgs e)
        {
            boxSizeComboBox.IsEnabled = true;
            bL.IsEnabled = false;
            bW.IsEnabled = false;
            bH.IsEnabled = false;
            bWght.IsEnabled = false;
        }

        private void keyboardFocusSelectAll(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.Tab))
                ((System.Windows.Controls.TextBox)sender).SelectAll();
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

        private void palletColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (palletColorComboBox.SelectedIndex == 0)
                pallet.Brush = Brushes.BurlyWood;
            else if (palletColorComboBox.SelectedIndex == 1)
                pallet.Brush = Brushes.SaddleBrown;
            else if (palletColorComboBox.SelectedIndex == 2)
                pallet.Brush = Brushes.Gold;
            else if (palletColorComboBox.SelectedIndex == 3)
                pallet.Brush = Brushes.Black;
            else if (palletColorComboBox.SelectedIndex == 4)
                pallet.Brush = Brushes.White;
        }

        private void boxColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (boxColorComboBox.SelectedIndex == 0)
                box.Brush = Brushes.SandyBrown;
            else if (boxColorComboBox.SelectedIndex == 1)
                box.Brush = Brushes.Chocolate;
            else if (boxColorComboBox.SelectedIndex == 2)
                box.Brush = Brushes.Sienna;
            else if (boxColorComboBox.SelectedIndex == 3)
                box.Brush = Brushes.Black;
            else if (boxColorComboBox.SelectedIndex == 4)
                box.Brush = Brushes.White;
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
            box.Length = excelOps.allBoxSizesFromExcel[0][boxSizeComboBox.SelectedIndex - 1];
            box.Width = excelOps.allBoxSizesFromExcel[1][boxSizeComboBox.SelectedIndex - 1];
            box.Height = excelOps.allBoxSizesFromExcel[2][boxSizeComboBox.SelectedIndex - 1];
            box.Weight = excelOps.allBoxSizesFromExcel[3][boxSizeComboBox.SelectedIndex - 1];
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
            viewClass.TopDownView(FindName("threedOrthoCamera") as OrthographicCamera, threedCamera);
        }

        private void freeCameraRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            viewClass.FreeView(FindName("threedOrthoCamera") as OrthographicCamera, threedCamera);
        }

        private void resetRotation()
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
        }

        private void sideViewXRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            resetRotation();
            viewClass.SideViewX(FindName("threedOrthoCamera") as OrthographicCamera, threedCamera);
        }

        private void sideViewYRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            resetRotation();
            viewClass.SideViewY(FindName("threedOrthoCamera") as OrthographicCamera, threedCamera);
        }

        private void epalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            pL.Text = "120";
            pW.Text = "80";
            pH.Text = "14.4";
            pHlim.Text = "180";
            pWght.Text = "33";
            pWghtlim.Text = "950";
        }

        private void industrialMenuItem_Click(object sender, RoutedEventArgs e)
        {
            pL.Text = "120";
            pW.Text = "100";
            pH.Text = "14.4";
            pHlim.Text = "180";
            pWght.Text = "33";
            pWghtlim.Text = "950";
        }

        private void asiaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            pL.Text = "110";
            pW.Text = "110";
            pH.Text = "14.4";
            pHlim.Text = "180";
            pWght.Text = "33";
            pWghtlim.Text = "950";
        }

        private void customSizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            pL.Text = "0";
            pW.Text = "0";
            pH.Text = "0";
            pHlim.Text = "0";
            pWght.Text = "0";
            pWghtlim.Text = "0";
        }

        private void ShowAxes()
        {
            viewClass.ViewAxes(pallet, ref MainViewPort, totalHeight);
        }
    }
}
