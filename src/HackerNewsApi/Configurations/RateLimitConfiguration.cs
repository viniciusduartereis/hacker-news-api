using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;
using HackerNewsApi.ViewModels;

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
                    var payload = new ResponseViewModel<object?>(
                        StatusCodes.Status429TooManyRequests,
                        new ErrorViewModel(
                            "Too many requests. Please try again in a minute.",
                            StatusCodes.Status429TooManyRequests,
                            "TooManyRequests"));

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
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
