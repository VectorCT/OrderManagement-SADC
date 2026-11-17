using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Filters;

public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (validationResult.IsValid)
            {
                continue;
            }

            var errors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
            var response = ApiResponse.Create(
                StatusCodes.Status400BadRequest,
                "Request validation failed.",
                errors,
                context.HttpContext);
            context.Result = new ObjectResult(response) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}