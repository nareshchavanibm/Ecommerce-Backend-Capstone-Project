using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Try to locate the API project's appsettings.json
        var current = Directory.GetCurrentDirectory();
        var possible = Path.Combine(current, "..", "EcommerceBackend");
        if (!Directory.Exists(possible))
        {
            possible = Path.Combine(current, "..", "..", "EcommerceBackend");
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(possible) ? possible : current)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var conn = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Could not find a DefaultConnection configuration for design-time DbContext.");

        optionsBuilder.UseSqlServer(conn);

        return new AppDbContext(optionsBuilder.Options);
    }
}
