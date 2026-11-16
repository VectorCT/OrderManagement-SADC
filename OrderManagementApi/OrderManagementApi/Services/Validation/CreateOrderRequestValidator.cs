using System.Text.RegularExpressions;
using FluentValidation;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private readonly CreateOrderLineItemRequestValidator _lineItemValidator = new();

    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required.");

        RuleFor(request => request.CurrencyCode)
            .NotEmpty()
            .Must(currencyCode => currencyCode is not null && Regex.IsMatch(currencyCode.Trim(), "^[A-Za-z]{3}$"))
            .WithMessage("Currency code must be a valid ISO 4217 alpha-3 code.");

        RuleFor(request => request.LineItems)
            .NotNull()
            .NotEmpty()
            .WithMessage("Order must contain at least one line item.");

        RuleForEach(request => request.LineItems)
            .NotNull()
            .WithMessage("Line item is required.")
            .SetValidator(_lineItemValidator);

        RuleFor(request => request.LineItems)
            .Must(HaveValidTotal)
            .WithMessage("Order total must not exceed the maximum supported amount.");
    }

    private static bool HaveValidTotal(List<CreateOrderLineItemRequest> lineItems)
    {
        if (lineItems is null)
        {
            return true;
        }

        decimal total = 0;
        foreach (var item in lineItems)
        {
            if (item is null || item.Quantity <= 0 ||
                item.UnitPrice < 0 ||
                item.UnitPrice > CreateOrderLineItemRequestValidator.MaximumMoneyValue)
            {
                continue;
            }

            var maximumPrice = CreateOrderLineItemRequestValidator.MaximumMoneyValue;
            if (item.UnitPrice > maximumPrice / item.Quantity)
            {
                return false;
            }

            var lineTotal = item.UnitPrice * item.Quantity;
            if (total > maximumPrice - lineTotal)
            {
                return false;
            }

            total += lineTotal;
        }

        return true;
    }
}
