using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Stack_Solver
{
    internal class ExcelOps
    {
        public double[][] allBoxSizesFromExcel { get; set; }
        public string excelFilePath { get; set; }

        public ExcelOps()
        {
            allBoxSizesFromExcel = new double[4][];
        }

        public void insertDataExcel(int row, PalletClass p, BoxClass c, int nrLevels, double totalHeight, double palletArea, double maxCargoAreaOccupied, int nrBoxesPerLevel, double cargoLength, double cargoWidth)
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
            oSheet.Cells[row, 3] = c.Length;
            oSheet.Cells[row, 4] = c.Width;
            oSheet.Cells[row, 5] = c.Height;
            oSheet.Cells[row, 6] = c.Weight;
            oSheet.Cells[row, 7] = nrLevels * nrBoxesPerLevel;
            oSheet.Cells[row, 8] = nrLevels;
            oSheet.Cells[row, 9] = nrBoxesPerLevel;
            oSheet.Cells[row, 10] = cargoLength + "x" + cargoWidth + "x" + (totalHeight - p.Height);
            oSheet.Cells[row, 11] = p.Length + "x" + p.Width + "x" + totalHeight;
            oSheet.Cells[row, 12] = p.Length;
            oSheet.Cells[row, 13] = p.Width;
            oSheet.Cells[row, 14] = totalHeight;
            oSheet.Cells[row, 15] = nrLevels * nrBoxesPerLevel * c.Weight;
            oSheet.Cells[row, 16] = nrLevels * nrBoxesPerLevel * c.Weight + p.Weight;
            oSheet.Cells[row, 17] = Math.Round((maxCargoAreaOccupied / palletArea * 100), 2, MidpointRounding.AwayFromZero) + "%";

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

        public void readExcelFile(int mode, ref ComboBox boxSizeComboBox, PalletClass p, BoxClass c, int nrLevels, double totalHeight, double palletArea, double maxCargoAreaOccupied, int nrBoxesPerLevel, double cargoLength, double cargoWidth)
        {
            allBoxSizesFromExcel = new double[4][];
            for (int i = 0; i < 4; i++)
            {
                allBoxSizesFromExcel[i] = new double[1001];
            }

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
                c.Length = double.Parse(ws.Cells[row, 1].Value2.ToString());
                c.Width = double.Parse(ws.Cells[row, 2].Value2.ToString());
                c.Height = double.Parse(ws.Cells[row, 3].Value2.ToString());
                c.Weight = double.Parse(ws.Cells[row, 4].Value2.ToString());
                //MessageBox.Show(double.Parse(ws.Cells[row, 1].Value2.ToString()).ToString());
                boxSizeComboBox.Items.Add(c.Length + " x " + c.Width + " x " + c.Height + "cm³ / " + c.Weight + "kg");

                allBoxSizesFromExcel[0][row - 2] = c.Length;
                allBoxSizesFromExcel[1][row - 2] = c.Width;
                allBoxSizesFromExcel[2][row - 2] = c.Height;
                allBoxSizesFromExcel[3][row - 2] = c.Weight;

                /*
                if (mode == 0)
                    mw.run_all_tests();
                else
                    mw.compare_results();
                mw.clearViewport();
                */

                oSheet.Cells[row, 1] = "Box" + (row - 1).ToString();
                oSheet.Cells[row, 3] = c.Length;
                oSheet.Cells[row, 4] = c.Width;
                oSheet.Cells[row, 5] = c.Height;
                oSheet.Cells[row, 6] = c.Weight;
                oSheet.Cells[row, 7] = nrLevels * nrBoxesPerLevel;
                oSheet.Cells[row, 8] = nrLevels;
                oSheet.Cells[row, 9] = nrBoxesPerLevel;
                oSheet.Cells[row, 10] = cargoLength + "x" + cargoWidth + "x" + (totalHeight - p.Height);
                oSheet.Cells[row, 11] = p.Length + "x" + p.Width + "x" + totalHeight;
                oSheet.Cells[row, 12] = p.Length;
                oSheet.Cells[row, 13] = p.Width;
                oSheet.Cells[row, 14] = totalHeight;
                oSheet.Cells[row, 15] = nrLevels * nrBoxesPerLevel * c.Weight;
                oSheet.Cells[row, 16] = nrLevels * nrBoxesPerLevel * c.Weight + p.Weight;
                oSheet.Cells[row, 17] = Math.Round((maxCargoAreaOccupied / palletArea * 100), 2, MidpointRounding.AwayFromZero) + "%";
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
        }

        public void createSampleFile()
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
    }
}
