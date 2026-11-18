using System.ComponentModel.DataAnnotations;
using FluentValidation;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Validation;

public sealed class UpdateAdminUserRequestValidator : AbstractValidator<UpdateAdminUserRequest>
{
    public UpdateAdminUserRequestValidator()
    {
        RuleFor(request => request.FirstName)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.LastName)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.Email)
            .NotEmpty()
            .Must(email => email is not null && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("A valid email address is required.")
            .Must(email => email is null || email.Trim().Length <= 256)
            .WithMessage("Email address must not exceed 256 characters.");
    }
}
