using Stack_Solver.Models.Inputs;
using Stack_Solver.Validation;

namespace Validation
{
    public class PalletSettingsDtoValidatorTests
    {
        [Fact]
        public void Validate_WithValidSettings_ReturnsValid()
        {
            var dto = new PalletSettingsDto
            {
                PalletLength = 120,
                PalletWidth = 80,
                PalletHeight = 15,
                UseCpsat = true,
                MaxCpsatCandidates = 1000,
                BlfAttempts = 50,
                SolverTimeLimit = 10,
                MaxStackHeight = 180,
                MaxStackWeight = 1000,
                MaxSkuOverhang = 0
            };

            var validator = new PalletSettingsDtoValidator();
            var result = validator.Validate(dto);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithInvalidSettings_ReturnsInvalid()
        {
            var dto = new PalletSettingsDto
            {
                PalletLength = 0,
                PalletWidth = 0,
                PalletHeight = 0,
                UseCpsat = false,
                MaxCpsatCandidates = 0,
                BlfAttempts = -1,
                SolverTimeLimit = 0,
                MaxStackHeight = 0,
                MaxStackWeight = 0,
                MaxSkuOverhang = -1
            };

            var validator = new PalletSettingsDtoValidator();
            var result = validator.Validate(dto);

            Assert.False(result.IsValid);
        }
    }
}
