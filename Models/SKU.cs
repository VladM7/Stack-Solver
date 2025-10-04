using System.ComponentModel.DataAnnotations;

namespace Stack_Solver.Models
{
    public class SKU
    {
        [Key]
        public string SkuId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Weight { get; set; }
        public bool Rotatable { get; set; }
        public string? Notes { get; set; }

        public int Quantity { get; set; } = 0;

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
    }
}
