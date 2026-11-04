using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Compiled into every API project so logging and health endpoints behave the same everywhere.
namespace ECommerce.Observability
{
    internal static class ObservabilityExtensions
    {
        // Readable lines in Development, one JSON object per line elsewhere.
        public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
        {
            var development = builder.Environment.IsDevelopment();
            var service = builder.Environment.ApplicationName;

            builder.Host.UseSerilog((context, _, logger) =>
            {
                logger
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Service", service)
                    .Enrich.WithSpan();

                if (development)
                {
                    logger.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} {TraceId} {Message:lj}{NewLine}{Exception}");
                }
                else
                {
                    logger.WriteTo.Console(new RenderedCompactJsonFormatter());
                }
            });

            builder.Services.AddHealthChecks();

            return builder;
        }

        // /health/live never touches dependencies; /health/ready runs the checks tagged "ready".
        public static WebApplication UseObservability(this WebApplication app)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (context, _, exception) =>
                    exception != null || context.Response.StatusCode >= 500
                        ? LogEventLevel.Error
                        : context.Request.Path.StartsWithSegments("/health")
                            ? LogEventLevel.Verbose
                            : LogEventLevel.Information;
            });

            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });

            return app;
        }
    }
}
