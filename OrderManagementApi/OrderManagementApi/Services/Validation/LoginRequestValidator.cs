using System.ComponentModel.DataAnnotations;
using FluentValidation;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .Must(value => value is not null && new EmailAddressAttribute().IsValid(value.Trim()))
            .WithMessage("A valid email address is required.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
