using ProductService.Application.Exceptions;

namespace ProductService.Api.Middleware
{
    // Turns expected failures into the { message } JSON the storefront reads; anything else stays a 500.
    public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (BusinessRuleException ex) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;

                await context.Response.WriteAsJsonAsync(new { message = ex.Message });
            }
        }
    }
}
