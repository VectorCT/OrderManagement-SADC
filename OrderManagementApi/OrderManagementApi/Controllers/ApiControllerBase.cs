using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected async Task<IActionResult> CallValidatedRequestAsync<TRequest>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<TRequest, CancellationToken, Task<IActionResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
            var response = ApiResponse.Create(
                StatusCodes.Status400BadRequest,
                "Request validation failed.",
                errors,
                HttpContext);
            return new BadRequestObjectResult(response);
        }

        return await executeAsync(request, cancellationToken);
    }
}
