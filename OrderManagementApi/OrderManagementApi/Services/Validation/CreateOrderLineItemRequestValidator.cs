using FluentValidation;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Validation;

public sealed class CreateOrderLineItemRequestValidator : AbstractValidator<CreateOrderLineItemRequest>
{
    public const decimal MaximumMoneyValue = 9_999_999_999_999_999.99m;

    public CreateOrderLineItemRequestValidator()
    {
        RuleFor(item => item.ProductSku)
            .Must(sku => !string.IsNullOrWhiteSpace(sku))
            .WithMessage("Product SKU is required.")
            .Must(sku => sku is null || sku.Trim().Length <= 64)
            .WithMessage("Product SKU must not exceed 64 characters.");

        RuleFor(item => item.Quantity)
            .GreaterThan(0)
            .WithMessage("Line item quantity must be greater than zero.");

        RuleFor(item => item.UnitPrice)
            .InclusiveBetween(0, MaximumMoneyValue)
            .WithMessage($"Line item unit price must be between 0 and {MaximumMoneyValue}.")
            .Must(price => decimal.Round(price, 2, MidpointRounding.ToEven) == price)
            .WithMessage("Line item unit price cannot have more than two decimal places.");
    }
}
