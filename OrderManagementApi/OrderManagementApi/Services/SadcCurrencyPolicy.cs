using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Services;

public sealed class SadcCurrencyPolicy : ISadcCurrencyPolicy
{
    private static readonly HashSet<string> SadcCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AO", "BW", "KM", "CD", "SZ", "LS", "MG", "MW", "MU", "MZ", "NA", "SC", "ZA", "TZ", "ZM", "ZW"
    };

    private static readonly Dictionary<string, HashSet<string>> CountryCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AO"] = new(StringComparer.OrdinalIgnoreCase) { "AOA" },
        ["BW"] = new(StringComparer.OrdinalIgnoreCase) { "BWP" },
        ["KM"] = new(StringComparer.OrdinalIgnoreCase) { "KMF" },
        ["CD"] = new(StringComparer.OrdinalIgnoreCase) { "CDF" },
        ["SZ"] = new(StringComparer.OrdinalIgnoreCase) { "SZL" },
        ["LS"] = new(StringComparer.OrdinalIgnoreCase) { "LSL" },
        ["MG"] = new(StringComparer.OrdinalIgnoreCase) { "MGA" },
        ["MW"] = new(StringComparer.OrdinalIgnoreCase) { "MWK" },
        ["MU"] = new(StringComparer.OrdinalIgnoreCase) { "MUR" },
        ["MZ"] = new(StringComparer.OrdinalIgnoreCase) { "MZN" },
        ["NA"] = new(StringComparer.OrdinalIgnoreCase) { "NAD" },
        ["SC"] = new(StringComparer.OrdinalIgnoreCase) { "SCR" },
        ["ZA"] = new(StringComparer.OrdinalIgnoreCase) { "ZAR" },
        ["TZ"] = new(StringComparer.OrdinalIgnoreCase) { "TZS" },
        ["ZM"] = new(StringComparer.OrdinalIgnoreCase) { "ZMW" },
        ["ZW"] = new(StringComparer.OrdinalIgnoreCase) { "ZWL", "USD" }
    };

    public bool IsValidCountry(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return SadcCountries.Contains(countryCode.Trim());
    }

    public bool IsCurrencyAllowed(string countryCode, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(currencyCode))
        {
            return false;
        }

        var normalizedCountry = countryCode.Trim();
        var normalizedCurrency = currencyCode.Trim();

        if (!CountryCurrencies.TryGetValue(normalizedCountry, out var allowedCurrencies))
        {
            return false;
        }

        return allowedCurrencies.Contains(normalizedCurrency);
    }
}
