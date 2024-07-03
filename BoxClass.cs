using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Stack_Solver
{
    internal class BoxClass
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Weight { get; set; }

        public Brush Brush { get; set; }

        public BoxClass()
        {
            Brush = Brushes.SandyBrown;
        }
    }
}
