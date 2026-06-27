using Stack_Solver.Models;

namespace Stack_Solver.Infrastructure
{
    public interface IUserSettingsService
    {
        Task SaveAsync(PalletDefaultsOptions palletDefaults, GenerationOptions genOptions);
    }
}
