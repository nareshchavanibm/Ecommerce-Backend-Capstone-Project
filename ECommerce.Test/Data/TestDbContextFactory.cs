using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Data;

public static class TestDbContextFactory
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;" +
        "Database=ECommerceIntegrationTestDb;" +
        "Integrated Security=True;" +
        "TrustServerCertificate=True;" +
        "Pooling=False;";

    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}