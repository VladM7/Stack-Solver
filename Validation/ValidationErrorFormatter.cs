using FluentValidation.Results;

namespace Stack_Solver.Validation
{
    public static class ValidationErrorFormatter
    {
        public static string Format(IEnumerable<ValidationFailure> errors)
        {
            var messages = errors
                .Where(e => e != null)
                .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                .Distinct()
                .ToArray();

            return messages.Length == 0
                ? "Validation failed."
                : string.Join(Environment.NewLine, messages);
        }
    }
}
