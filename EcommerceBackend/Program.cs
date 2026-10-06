using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Application.Services;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// In-memory caching
builder.Services.AddMemoryCache();

// SignalR
builder.Services.AddSignalR();

// Background jobs
// Register background task queue and worker
builder.Services.AddSingleton<ECommerce.Infrastructure.BackgroundServices.IBackgroundTaskQueue, ECommerce.Infrastructure.BackgroundServices.BackgroundTaskQueue>();
builder.Services.AddHostedService<ECommerce.Infrastructure.BackgroundServices.BackgroundTaskQueueHostedService>();

builder.Services.AddHostedService<ECommerce.Infrastructure.BackgroundServices.ProductCacheWarmupService>();
builder.Services.AddHostedService<ECommerce.Infrastructure.BackgroundServices.InventoryReconciliationService>();
builder.Services.AddHostedService<ECommerce.Infrastructure.BackgroundServices.EmailScheduledService>();

// Email sender
builder.Services.AddSingleton<ECommerce.Application.Interfaces.IEmailSender, ECommerce.Infrastructure.Services.EmailSender>();

// Notification service (SignalR)
builder.Services.AddSingleton<ECommerce.Application.Interfaces.INotificationService, ECommerce.Infrastructure.Services.NotificationService>();

// Import service
builder.Services.AddScoped<ECommerce.Infrastructure.Services.ImportProductsService>();

// Configure default import batch size (can be overridden in appsettings.json)
builder.Configuration["Import:BatchSize"] ??= "1000";

builder.Services.AddExceptionHandler<
    ECommerce.Api.Exceptions.GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

// Add Swagger
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// Add Entity Framework Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories

builder.Services.AddScoped<
    IProductRepository,
    ProductRepository>();

builder.Services.AddScoped<
    ICategoryRepository,
    CategoryRepository>();

builder.Services.AddScoped<
    IInventoryRepository,
    InventoryRepository>();

builder.Services.AddScoped<
    IPaymentRepository,
    PaymentRepository>();


// Services

builder.Services.AddScoped<
    ICheckoutService,
    CheckoutService>();

builder.Services.AddScoped<
    ITransactionService,
    TransactionService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    IPaymentService,
    PaymentService>();

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

builder.Services.AddScoped<
    IInventoryService,
    InventoryService>();

 builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<ICartRepository, 
    CartRepository>();

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    IOrderRepository,
    OrderRepository>();

builder.Services.AddScoped<
    IOrderService,
    OrderService>();
builder.Services.AddScoped<IUnitOfWork, 
    UnitOfWork>();

builder.Services.AddScoped<ICartService, 
    CartService>();


builder.Services.AddScoped<JwtService>();

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key is not configured.");

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey))
            };
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

//Console.WriteLine(
//    BCrypt.Net.BCrypt.HashPassword("Admin@12345"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseExceptionHandler();
// Configure Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
   
}

// HTTPS
app.UseHttpsRedirection();

app.UseCors("AngularClient");

// Authorization
app.UseAuthentication();
app.UseAuthorization();



// Map controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<ECommerce.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");
app.Run();
