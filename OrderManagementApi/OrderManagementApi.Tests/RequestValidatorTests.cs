using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;

namespace OrderManagementApi.Tests;

public sealed class RequestValidatorTests
{
    private readonly CreateCustomerRequestValidator _customerValidator =
        new(new TestSadcCurrencyPolicy());

    private readonly CreateOrderRequestValidator _orderValidator = new();

    [Fact]
    public void CreateCustomerRequestValidator_AcceptsValidSadcCustomer()
    {
        var result = _customerValidator.Validate(
            new CreateCustomerRequest("Customer", "customer@example.com", "ZA"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateCustomerRequestValidator_RejectsInvalidCustomerFields()
    {
        var result = _customerValidator.Validate(
            new CreateCustomerRequest(" ", "invalid-email", "US"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCustomerRequest.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCustomerRequest.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCustomerRequest.CountryCode));
    }

    [Fact]
    public void CreateOrderRequestValidator_AcceptsValidOrder()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZAR",
            [new CreateOrderLineItemRequest("SKU-1", 2, 12.50m)]);

        var result = _orderValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateOrderRequestValidator_RejectsInvalidOrderAndLineItemFields()
    {
        var request = new CreateOrderRequest(
            Guid.Empty,
            "US Dollar",
            [new CreateOrderLineItemRequest(" ", 0, -1m)]);

        var result = _orderValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateOrderRequest.CustomerId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateOrderRequest.CurrencyCode));
        Assert.Contains(result.Errors, error => error.PropertyName.Contains(nameof(CreateOrderRequest.LineItems)));
    }

    [Fact]
    public void CreateOrderRequestValidator_RejectsTotalsExceedingSupportedMoneyPrecision()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZAR",
            [new CreateOrderLineItemRequest(
                "SKU-1",
                2,
                CreateOrderLineItemRequestValidator.MaximumMoneyValue)]);

        var result = _orderValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Order total"));
    }

    [Fact]
    public void CreateOrderRequestValidator_RejectsNullLineItems()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "ZAR",
            [null!]);

        var result = _orderValidator.Validate(request);

        Assert.Contains(result.Errors, error => error.ErrorMessage == "Line item is required.");
    }
}

internal sealed class TestSadcCurrencyPolicy : Services.Interfaces.ISadcCurrencyPolicy
{
    public bool IsValidCountry(string countryCode) =>
        string.Equals(countryCode.Trim(), "ZA", StringComparison.OrdinalIgnoreCase);

    public bool IsCurrencyAllowed(string countryCode, string currencyCode) =>
        IsValidCountry(countryCode) &&
        string.Equals(currencyCode.Trim(), "ZAR", StringComparison.OrdinalIgnoreCase);
}
