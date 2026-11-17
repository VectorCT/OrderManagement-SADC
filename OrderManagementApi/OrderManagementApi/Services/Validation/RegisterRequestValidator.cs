using System.ComponentModel.DataAnnotations;
using FluentValidation;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.FirstName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("First name is required.")
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("First name must not exceed 100 characters.");

        RuleFor(request => request.LastName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Last name is required.")
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("Last name must not exceed 100 characters.");

        RuleFor(request => request.Email)
            .NotEmpty()
            .Must(value => value is not null && new EmailAddressAttribute().IsValid(value.Trim()))
            .WithMessage("A valid email address is required.")
            .Must(value => value is null || value.Trim().Length <= 256)
            .WithMessage("Email address must not exceed 256 characters.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("Password must contain at least 12 characters.")
            .Must(value => value is not null && value.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(value => value is not null && value.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(value => value is not null && value.Any(char.IsDigit))
            .WithMessage("Password must contain a number.")
            .Must(value => value is not null && value.Any(character => !char.IsLetterOrDigit(character)))
            .WithMessage("Password must contain a non-alphanumeric character.");
    }
}
