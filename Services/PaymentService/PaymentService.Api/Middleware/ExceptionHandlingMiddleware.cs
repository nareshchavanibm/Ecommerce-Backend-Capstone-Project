using PaymentService.Application.Exceptions;

namespace PaymentService.Api.Middleware
{
    // Turns expected failures into JSON { message } responses the storefront already reads.
    public sealed class ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted && Map(ex) is not null)
            {
                var (status, message) = Map(ex)!.Value;

                if (status >= 500)
                {
                    logger.LogError(ex, "Request failed with {Status}.", status);
                }

                context.Response.StatusCode = status;

                await context.Response.WriteAsJsonAsync(new { message });
            }
        }

        private static (int Status, string Message)? Map(Exception ex) => ex switch
        {
            BusinessRuleException => (StatusCodes.Status400BadRequest, ex.Message),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            DependencyUnavailableException => (StatusCodes.Status502BadGateway, ex.Message),
            _ => null
        };
    }
}
