namespace OrderManagementApi.Services.Interfaces;

public interface ISadcCurrencyPolicy
{
    bool IsValidCountry(string countryCode);
    bool IsCurrencyAllowed(string countryCode, string currencyCode);
}
