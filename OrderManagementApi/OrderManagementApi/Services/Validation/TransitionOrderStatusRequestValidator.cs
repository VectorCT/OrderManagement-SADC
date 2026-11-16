using FluentValidation;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Validation;

public sealed class TransitionOrderStatusRequestValidator : AbstractValidator<TransitionOrderStatusRequest>
{
    public TransitionOrderStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("A valid order status is required.");
    }
}