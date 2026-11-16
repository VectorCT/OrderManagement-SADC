using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using FluentValidation;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Validation;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator(ISadcCurrencyPolicy currencyPolicy)
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Customer name is required.")
            .Must(name => name is null || name.Trim().Length <= 200)
            .WithMessage("Customer name must not exceed 200 characters.");

        RuleFor(request => request.Email)
            .NotEmpty()
            .Must(email => email is not null && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("A valid email address is required.")
            .Must(email => email is null || email.Trim().Length <= 320)
            .WithMessage("Email address must not exceed 320 characters.");

        RuleFor(request => request.CountryCode)
            .NotEmpty()
            .Must(countryCode => countryCode is not null && Regex.IsMatch(countryCode.Trim(), "^[A-Za-z]{2}$"))
            .WithMessage("Country code must be a valid ISO 3166-1 alpha-2 code.")
            .Must(countryCode => countryCode is not null && currencyPolicy.IsValidCountry(countryCode.Trim()))
            .WithMessage("Country code must identify a SADC member country.");
    }
}
