using DataAccessLayer.Models;
using OrderManagementApi.Services;

namespace OrderManagementApi.Tests;

public sealed class SadcCurrencyPolicyTests
{
    [Theory]
    [InlineData("AO", "AOA")]
    [InlineData("BW", "BWP")]
    [InlineData("KM", "KMF")]
    [InlineData("CD", "CDF")]
    [InlineData("SZ", "SZL")]
    [InlineData("LS", "LSL")]
    [InlineData("MG", "MGA")]
    [InlineData("MW", "MWK")]
    [InlineData("MU", "MUR")]
    [InlineData("MZ", "MZN")]
    [InlineData("NA", "NAD")]
    [InlineData("SC", "SCR")]
    [InlineData("ZA", "ZAR")]
    [InlineData("TZ", "TZS")]
    [InlineData("ZM", "ZMW")]
    [InlineData("ZW", "ZWL")]
    [InlineData("ZW", "USD")]
    public void IsCurrencyAllowed_AcceptsConfiguredCountryCurrency(string countryCode, string currencyCode)
    {
        var policy = new SadcCurrencyPolicy();

        Assert.True(policy.IsValidCountry(countryCode));
        Assert.True(policy.IsCurrencyAllowed(countryCode, currencyCode));
    }

    [Theory]
    [InlineData("US", "USD")]
    [InlineData("ZA", "USD")]
    [InlineData("ZW", "ZAR")]
    [InlineData("", "ZAR")]
    [InlineData("ZA", "")]
    public void IsCurrencyAllowed_RejectsUnsupportedCountryOrCurrency(string countryCode, string currencyCode)
    {
        var policy = new SadcCurrencyPolicy();

        Assert.False(policy.IsCurrencyAllowed(countryCode, currencyCode));
    }

    [Fact]
    public void IsCurrencyAllowed_TrimsAndIgnoresCase()
    {
        var policy = new SadcCurrencyPolicy();

        Assert.True(policy.IsCurrencyAllowed(" za ", " zar "));
    }
}

public sealed class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Fulfilled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    public void CanTransition_AllowsConfiguredTransitions(OrderStatus from, OrderStatus to)
    {
        var stateMachine = new OrderStateMachine();

        Assert.True(stateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatus.Pending, OrderStatus.Fulfilled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Pending)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Paid)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    [InlineData((OrderStatus)99, OrderStatus.Pending)]
    public void CanTransition_RejectsInvalidTransitions(OrderStatus from, OrderStatus to)
    {
        var stateMachine = new OrderStateMachine();

        Assert.False(stateMachine.CanTransition(from, to));
    }
}
