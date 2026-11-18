using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderManagementApi.Contracts;
using OrderManagementApi.Middleware;
using OrderManagementApi.Services;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;
using OrderManagement.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var rabbitMqOptions = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddSingleton<IOrderEventPublisher, RabbitMqOrderEventPublisher>();
builder.Services.AddSingleton<IRabbitMqQueueInspector, RabbitMqQueueInspector>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<ISadcCurrencyPolicy, SadcCurrencyPolicy>();
builder.Services.AddSingleton<IOrderStateMachine, OrderStateMachine>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IMessagingDiagnosticsService, MessagingDiagnosticsService>();
builder.Services.AddScoped<IAuthenticatedUserService, AuthenticatedUserService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();
builder.Services.AddScoped<ApplicationDataSeeder>();
builder.Services.AddHostedService<OrderCreatedOutboxPublisher>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

var jwtConfiguration = builder.Configuration.GetSection("Jwt");
var issuer = jwtConfiguration["Issuer"];
var audience = jwtConfiguration["Audience"];
var signingKey = jwtConfiguration["SigningKey"];
var accessTokenMinutes = jwtConfiguration.GetValue<int?>("AccessTokenMinutes") ?? 60;
var refreshTokenDays = jwtConfiguration.GetValue<int?>("RefreshTokenDays") ?? 14;
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
    string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32 ||
    accessTokenMinutes is < 5 or > 1440 || refreshTokenDays is < 1 or > 90)
{
    throw new InvalidOperationException(
        "JWT issuer, audience, a signing key of at least 32 bytes, an access token lifetime from 5 to 1440 minutes, and a refresh token lifetime from 1 to 90 days must be configured.");
}

builder.Services.Configure<JwtAuthenticationOptions>(jwtConfiguration);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RequireSignedTokens = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (context.Principal is null)
                {
                    context.Fail("The token did not contain an authenticated principal.");
                    return;
                }

                var authenticatedUserService = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthenticatedUserService>();
                var applicationPrincipal = await authenticatedUserService.CreateApplicationPrincipalAsync(
                    context.Principal,
                    context.HttpContext.RequestAborted);
                if (applicationPrincipal is null)
                {
                    context.Fail("The token subject does not map to an application user.");
                    return;
                }

                context.Principal = applicationPrincipal;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse.Create(
                    StatusCodes.Status401Unauthorized,
                    "Authentication is required.",
                    null,
                    context.HttpContext), context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse.Create(
                    StatusCodes.Status403Forbidden,
                    "You are not authorized to perform this operation.",
                    null,
                    context.HttpContext), context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Orders.Read", policy => policy.RequireAssertion(context =>
        HasPermission(context.User, "Orders.Read") || HasPermission(context.User, "Orders.Admin")));
    options.AddPolicy("Orders.Write", policy => policy.RequireAssertion(context =>
        HasPermission(context.User, "Orders.Write") || HasPermission(context.User, "Orders.Admin")));
    options.AddPolicy("Orders.Admin", policy => policy.RequireAssertion(context =>
        HasPermission(context.User, "Orders.Admin")));
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The supplied value is invalid." : error.ErrorMessage).ToArray());
        var response = ApiResponse.Create(
            StatusCodes.Status400BadRequest,
            "Request binding failed.",
            errors,
            context.HttpContext);
        return new BadRequestObjectResult(response);
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Supply a JWT issued for the configured issuer and audience."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ApplicationDataSeeder>();
    var adminEmail = app.Configuration["SeedData:AdminEmail"] ?? "ctshabalala89@gmail.com";
    var seedOrders = app.Configuration.GetValue("SeedData:SeedOrders", true);
    await seeder.SeedAsync(adminEmail, seedOrders, app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var httpContext = statusContext.HttpContext;
    var statusCode = httpContext.Response.StatusCode;
    var message = statusCode switch
    {
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status405MethodNotAllowed => "The requested method is not allowed.",
        _ => "The request could not be completed."
    };
    await httpContext.Response.WriteAsJsonAsync(
        ApiResponse.Create(statusCode, message, null, httpContext),
        httpContext.RequestAborted);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool HasPermission(ClaimsPrincipal user, string permission) =>
    user.IsInRole("Admin") || user.IsInRole("Orders.Admin") ||
    user.IsInRole(permission) || user.HasClaim("permission", permission);

public partial class Program { }