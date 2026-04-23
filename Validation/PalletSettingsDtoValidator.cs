using FluentValidation;
using Stack_Solver.Models.Inputs;

namespace Stack_Solver.Validation
{
    public class PalletSettingsDtoValidator : AbstractValidator<PalletSettingsDto>
    {
        public PalletSettingsDtoValidator()
        {
            RuleFor(x => x.PalletLength).GreaterThan(0);
            RuleFor(x => x.PalletWidth).GreaterThan(0);
            RuleFor(x => x.PalletHeight).GreaterThan(0);
            RuleFor(x => x.MaxCpsatCandidates).GreaterThan(0);
            RuleFor(x => x.BlfAttempts).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SolverTimeLimit).GreaterThan(0);
            RuleFor(x => x.MaxStackHeight).GreaterThan(0);
            RuleFor(x => x.MaxStackWeight).GreaterThan(0);
            RuleFor(x => x.MaxSkuOverhang).GreaterThanOrEqualTo(0);
        }
    }
}
