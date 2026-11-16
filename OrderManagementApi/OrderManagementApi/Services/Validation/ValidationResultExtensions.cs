using FluentValidation.Results;

namespace OrderManagementApi.Services.Validation;

internal static class ValidationResultExtensions
{
    public static string ToErrorMessage(this ValidationResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}"));
}
