using System.Text;
using System.Text.Json.Serialization;
using ECommerce.Api;
using ECommerce.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReviewService.Api.Clients;
using ReviewService.Api.Controllers;
using ReviewService.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidationMessage();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var orderServiceUrl = builder.Configuration["Services:OrderServiceUrl"]
    ?? throw new InvalidOperationException("Services:OrderServiceUrl is not configured.");

var internalKey = builder.Configuration["Internal:ApiKey"];

builder.Services.AddHttpClient<IPurchaseVerifier, OrderPurchaseVerifier>(client =>
{
    client.BaseAddress = new Uri(orderServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);

    if (!string.IsNullOrWhiteSpace(internalKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
    }
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Own database, created and updated on startup so the service needs no manual step.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseObservability();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
