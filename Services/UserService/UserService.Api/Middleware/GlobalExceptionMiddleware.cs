using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace UserService.Api.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unhandled exception occurred.");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception)
        {
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = exception switch
            {
                InvalidOperationException => StatusCodes.Status400BadRequest,

                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,

                // A unique index rejected a concurrent duplicate (for example the same email registered twice).
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
                    => StatusCodes.Status409Conflict,

                _ => StatusCodes.Status500InternalServerError
            };

            var response = new
            {
                message = context.Response.StatusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred. Please try again."
                    : context.Response.StatusCode == StatusCodes.Status409Conflict
                        ? "This record already exists."
                        : exception.Message
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}