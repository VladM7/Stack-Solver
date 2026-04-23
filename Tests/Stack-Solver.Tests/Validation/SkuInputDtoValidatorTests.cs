using Stack_Solver.Models.Inputs;
using Stack_Solver.Validation;

namespace Validation
{
    public class SkuInputDtoValidatorTests
    {
        [Fact]
        public void Validate_WithValidSku_ReturnsValid()
        {
            var dto = new SkuInputDto
            {
                Name = "Box",
                Length = 10,
                Width = 5,
                Height = 2,
                Weight = 1.5,
                Rotatable = true,
                Notes = ""
            };

            var validator = new SkuInputDtoValidator();
            var result = validator.Validate(dto);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithInvalidDimensions_ReturnsInvalid()
        {
            var dto = new SkuInputDto
            {
                Name = "Box",
                Length = 0,
                Width = -1,
                Height = 0,
                Weight = -0.1,
                Rotatable = true
            };

            var validator = new SkuInputDtoValidator();
            var result = validator.Validate(dto);

            Assert.False(result.IsValid);
        }
    }
}
