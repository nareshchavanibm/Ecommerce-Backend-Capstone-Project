using ECommerce.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// =========================
// CORS
// =========================
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    if (origins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins is not configured.");
    }

    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =========================
// JWT Authentication
// =========================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                RequireExpirationTime = true,

                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization();

// =========================
// Rate Limiting
// =========================
builder.Services.AddRateLimiter(options =>
{
    // One budget per client address (not shared by everyone); sign-in endpoints get a much smaller one.
    options.AddPolicy("GatewayPolicy", context =>
    {
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var isAuth = context.Request.Path.StartsWithSegments("/api/auth");

        return RateLimitPartition.GetFixedWindowLimiter(
            isAuth ? "auth:" + client : "api:" + client,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isAuth ? 20 : 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;
});

// =========================
// YARP Reverse Proxy
// =========================
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseObservability();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// =========================
// Security Headers
// =========================
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] =
        "nosniff";

    context.Response.Headers["X-Frame-Options"] =
        "DENY";

    context.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";

    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";

    await next();
});

// =========================
// Middleware
// =========================
app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// =========================
// Reverse Proxy
// =========================
app.MapReverseProxy()
    .RequireRateLimiting("GatewayPolicy");

app.Run();