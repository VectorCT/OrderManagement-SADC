namespace OrderManagementApi.Contracts;

public sealed record ApiResponse(int Code, string Message, object? Results, string Endpoint)
{
    public static ApiResponse Create(int code, string message, object? results, HttpContext context) =>
        new(code, message, results, context.Request.Path.Value ?? "/");
}