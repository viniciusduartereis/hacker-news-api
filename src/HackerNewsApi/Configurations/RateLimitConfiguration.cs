using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace HackerNewsApi.Configurations
{
    public static class RateLimitConfiguration
    {
        public static void AddRateLimiting(this IServiceCollection services)
        {
            
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            title = "Too Many Requests",
                            status = StatusCodes.Status429TooManyRequests,
                            detail = "Too many requests. Please try again in a minute."
                        },
                        cancellationToken);
                };

                options.AddPolicy("FixedWindow", context =>
                    RateLimitPartition.GetFixedWindowLimiter(partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown", factory: partition => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));
            });

        }
    }
}
