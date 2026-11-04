using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api
{
    internal static class ValidationResponseExtensions
    {
        // Keeps the standard 400 body (title, errors) and adds the "message" every client already reads.
        public static IServiceCollection AddValidationMessage(this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problem = new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest
                    };

                    problem.Extensions["message"] = problem.Errors.Values
                        .SelectMany(messages => messages)
                        .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                        ?? "The request is not valid.";

                    return new BadRequestObjectResult(problem)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });

            return services;
        }
    }
}
