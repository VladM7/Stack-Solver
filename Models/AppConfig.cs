namespace Stack_Solver.Models
{
    /// <summary>
    /// Represents the application configuration settings.
    /// </summary>
    /// <remarks>This class requires the specification of both the configurations folder and the application
    /// properties file name to function correctly.</remarks>
    public class AppConfig
    {
        public required string ConfigurationsFolder { get; set; }

        public required string AppPropertiesFileName { get; set; }
    }
}
