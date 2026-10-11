using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrderService.Api.Filters
{
    // Guards service-to-service endpoints that must never be callable by customers.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class InternalApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        public const string HeaderName = "X-Internal-Key";

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var expected = context.HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["Internal:ApiKey"];

            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Result = new ObjectResult(new
                {
                    message = "Internal API key is not configured."
                })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };

                return;
            }

            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

            var matches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected));

            if (!matches)
            {
                context.Result = new UnauthorizedResult();

                return;
            }

            await next();
        }
    }
}
