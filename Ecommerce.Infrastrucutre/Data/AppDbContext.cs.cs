using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Inventory> Inventories => Set<Inventory>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Cart> Carts { get; set; }

    public DbSet<CartItem> CartItems { get; set; }

    public DbSet<ImportJob> ImportJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
    typeof(AppDbContext).Assembly);

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 4,
                FirstName = "System",
                LastName = "Administrator",
                Email = "admin@e.com",
                // Use a precomputed bcrypt hash so migrations are deterministic
                PasswordHash = "$2a$11$G5jN7tDC/71CKW3O/uiIEet7s3/z6ANMqNEIH7cdQUDuhvZeen5sm",
                Role = "Admin",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            });
    }
}