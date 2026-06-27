using Stack_Solver.Data;
using Stack_Solver.Models;
using System.IO;
using System.Text.Json;

namespace Stack_Solver.Infrastructure
{
    public class UserSettingsService : IUserSettingsService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public async Task SaveAsync(PalletDefaultsOptions palletDefaults, GenerationOptions genOptions)
        {
            AppPaths.EnsureAppData();
            var data = new { LayerGeneration = genOptions, PalletDefaults = palletDefaults };
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(AppPaths.UserSettingsFile, json);
        }
    }
}
