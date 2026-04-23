using FluentValidation;
using Stack_Solver.Models.Inputs;

namespace Stack_Solver.Validation
{
    public class SkuInputDtoValidator : AbstractValidator<SkuInputDto>
    {
        public SkuInputDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Length).GreaterThan(0);
            RuleFor(x => x.Width).GreaterThan(0);
            RuleFor(x => x.Height).GreaterThan(0);
            RuleFor(x => x.Weight).GreaterThanOrEqualTo(0);
        }
    }
}
