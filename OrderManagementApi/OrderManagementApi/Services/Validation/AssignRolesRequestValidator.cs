using FluentValidation;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Validation;

public sealed class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    public AssignRolesRequestValidator()
    {
        RuleFor(request => request.Roles)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one role is required.")
            .Must(roles => roles is null || roles.Count <= 10)
            .WithMessage("No more than 10 roles can be assigned at once.");

        RuleForEach(request => request.Roles)
            .Must(role => !string.IsNullOrWhiteSpace(role))
            .WithMessage("Role names cannot be empty.")
            .Must(role => role is null || role.Trim().Length <= 256)
            .WithMessage("Role names cannot exceed 256 characters.");
    }
}
