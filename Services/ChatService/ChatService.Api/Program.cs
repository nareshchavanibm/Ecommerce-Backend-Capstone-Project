using System.Text;
using System.Threading.RateLimiting;
using ChatService.Api;
using ChatService.Api.Agents;
using ChatService.Api.Foundry;
using ChatService.Api.Governance;
using ChatService.Api.Options;
using ChatService.Api.Rag;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.Section));
builder.Services.Configure<ServiceUrlOptions>(builder.Configuration.GetSection(ServiceUrlOptions.Section));
builder.Services.Configure<GovernanceOptions>(builder.Configuration.GetSection(GovernanceOptions.Section));
builder.Services.Configure<KnowledgeOptions>(builder.Configuration.GetSection(KnowledgeOptions.Section));

builder.Services.AddHttpClient<FoundryClient>(c => c.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddSingleton<IChatModel>(sp => sp.GetRequiredService<FoundryClient>());
builder.Services.AddSingleton<IEmbeddingModel>(sp => sp.GetRequiredService<FoundryClient>());

builder.Services.AddHttpClient("catalog", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient<OrderLookupClient>(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddSingleton<KnowledgeIndex>();
builder.Services.AddSingleton<KnowledgeIndexer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<KnowledgeIndexer>());

builder.Services.AddSingleton<IAgentTool, SearchKnowledgeTool>();
builder.Services.AddSingleton<IAgentTool, SearchAdminHelpTool>();
builder.Services.AddSingleton<IAgentTool, SearchProductsTool>();
builder.Services.AddSingleton<IAgentTool, EscalateTool>();
builder.Services.AddTransient<IAgentTool, LookupOrderTool>();
builder.Services.AddTransient<IAgentTool, ListMyOrdersTool>();
builder.Services.AddTransient<IAgentTool, ListRecentOrdersTool>();

builder.Services.AddSingleton<InputGuard>();
builder.Services.AddSingleton<OutputGuard>();
builder.Services.AddSingleton<AuditLog>();
builder.Services.AddSingleton<AgentRouter>();
builder.Services.AddScoped<AgentRunner>();
builder.Services.AddScoped<OfflineResponder>();
builder.Services.AddScoped<ChatOrchestrator>();

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

var perMinute = builder.Configuration.GetValue("Governance:RequestsPerMinute", 20);

// Each customer (or client address) gets their own budget so one user cannot run up model costs.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("chat", context =>
    {
        var key = context.User.Identity?.IsAuthenticated == true
            ? "user:" + context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : "ip:" + ClientAddress(context);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = perMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

var foundry = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoundryOptions>>().Value;

if (!foundry.IsConfigured)
{
    app.Logger.LogWarning("Foundry:Endpoint is not set. The assistant runs in retrieval-only mode without a language model.");
}

app.Run();

// The gateway appends the caller's address, so the last X-Forwarded-For entry is the one it observed.
static string ClientAddress(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();

    return !string.IsNullOrWhiteSpace(forwarded)
        ? forwarded.Split(',')[^1].Trim()
        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program;
