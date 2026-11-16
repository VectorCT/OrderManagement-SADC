using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Contracts;

namespace OrderManagementApi.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            DbUpdateException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var message = statusCode == StatusCodes.Status409Conflict
            ? "The request conflicts with existing data."
            : "An unexpected error occurred.";

        logger.LogError(exception, "Request failed with status code {StatusCode}.", statusCode);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse.Create(statusCode, message, null, httpContext),
            cancellationToken);
        return true;
    }
}