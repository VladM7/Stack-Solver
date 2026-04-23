using FluentValidation;
using Stack_Solver.Models.Inputs;

namespace Stack_Solver.Validation
{
    public class SkuQuantityDtoValidator : AbstractValidator<SkuQuantityDto>
    {
        public SkuQuantityDtoValidator()
        {
            RuleFor(x => x.SkuId).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        }
    }
}
