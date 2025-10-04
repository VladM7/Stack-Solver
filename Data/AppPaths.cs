using System.IO;

namespace Stack_Solver.Data
{
    public static class AppPaths
    {
        public static readonly string AppDataDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StackSolver");

        public static readonly string DatabaseFile = Path.Combine(AppDataDirectory, "stacksolver.db");

        public static void EnsureAppData()
        {
            if (!Directory.Exists(AppDataDirectory))
                Directory.CreateDirectory(AppDataDirectory);
        }
    }
}
