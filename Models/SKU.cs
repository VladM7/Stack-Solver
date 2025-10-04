using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Stack_Solver.Models
{
    public class SKU
    {
        public string SkuId { get; set; }
        public string Name { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Weight { get; set; }
        public bool Rotatable { get; set; }
        public string Notes { get; set; }

        public int Quantity { get; set; } = 0;

        private static readonly string filePath = "sku.json";

        public SKU(string skuId, string name, int length, int width, int height, double weight, bool rotatable, string notes)
        {
            SkuId = skuId;
            Name = name;
            Length = length;
            Width = width;
            Height = height;
            Weight = weight;
            Rotatable = rotatable;
            Notes = notes;
        }

        public SKU() { }

        public static ObservableCollection<SKU> LoadSKUs()
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<ObservableCollection<SKU>>(json);
            }
            catch (Exception)
            {

            }

            return [];
        }

        public static void SaveSKUs(ObservableCollection<SKU> SKUs)
        {
            string json = JsonSerializer.Serialize(SKUs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            // MessageBox.Show($"Saved SKUs to: {filePath}");
        }
    }
}
