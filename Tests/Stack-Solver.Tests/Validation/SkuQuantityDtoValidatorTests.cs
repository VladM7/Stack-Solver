using Stack_Solver.Models.Inputs;
using Stack_Solver.Validation;

namespace Validation
{
    public class SkuQuantityDtoValidatorTests
    {
        [Fact]
        public void Validate_WithValidQuantity_ReturnsValid()
        {
            var dto = new SkuQuantityDto
            {
                SkuId = "sku-1",
                Quantity = 10
            };

            var validator = new SkuQuantityDtoValidator();
            var result = validator.Validate(dto);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithInvalidQuantity_ReturnsInvalid()
        {
            var dto = new SkuQuantityDto
            {
                SkuId = "",
                Quantity = -1
            };

            var validator = new SkuQuantityDtoValidator();
            var result = validator.Validate(dto);

            Assert.False(result.IsValid);
        }
    }
}
