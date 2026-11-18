using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IOptions<JwtAuthenticationOptions> jwtOptions) : ApiControllerBase
{
    private const string RefreshCookieName = "__Host-om-refresh";

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            registerValidator,
            async (validatedRequest, ct) =>
            {
                var result = await authenticationService.RegisterAsync(validatedRequest, ct);
                if (!result.Succeeded)
                {
                    var statusCode = result.Conflict
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest;
                    return StatusCode(statusCode, ApiResponse.Create(
                        statusCode,
                        result.Conflict ? "An account with this email already exists." : "Registration failed.",
                        result.Errors,
                        HttpContext));
                }

                return StatusCode(StatusCodes.Status201Created, ApiResponse.Create(
                    StatusCodes.Status201Created,
                    "Account created successfully. Log in to receive an access token.",
                    result.User,
                    HttpContext));
            },
            cancellationToken);

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            loginValidator,
            async (validatedRequest, ct) =>
            {
                var result = await authenticationService.LoginAsync(validatedRequest, ct);
                if (result.Token is null || result.RefreshToken is null)
                {
                    return Unauthorized(ApiResponse.Create(
                        StatusCodes.Status401Unauthorized,
                        "Email or password is incorrect.",
                        null,
                        HttpContext));
                }

                SetRefreshCookie(result.RefreshToken);
                return Ok(ApiResponse.Create(
                    StatusCodes.Status200OK,
                    "Login successful.",
                    result.Token,
                    HttpContext));
            },
            cancellationToken);

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            Request.Cookies[RefreshCookieName],
            cancellationToken);
        if (result.Token is null || result.RefreshToken is null)
        {
            Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
            return Unauthorized(ApiResponse.Create(
                StatusCodes.Status401Unauthorized,
                "The refresh session is missing, expired, or invalid.",
                null,
                HttpContext));
        }

        SetRefreshCookie(result.RefreshToken);
        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Session refreshed.",
            result.Token,
            HttpContext));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authenticationService.RevokeRefreshTokenAsync(
            Request.Cookies[RefreshCookieName],
            cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Signed out.",
            null,
            HttpContext));
    }

    private void SetRefreshCookie(string token) =>
        Response.Cookies.Append(RefreshCookieName, token, RefreshCookieOptions());

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)
    };
}
