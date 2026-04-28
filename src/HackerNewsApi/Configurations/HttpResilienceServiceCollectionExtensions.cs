using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace HackerNewsApi.Configurations;

public static class HttpResilienceServiceCollectionExtensions
{
    private static readonly PredicateBuilder<HttpResponseMessage> TransientHttpPredicate = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .Handle<TimeoutRejectedException>()
        .HandleResult(static response => IsTransient(response.StatusCode));

    public static IServiceCollection AddApplicationHttpResiliencePipelines(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(
            HttpResiliencePipelineKeys.SafeRead,
            static builder =>
            {
                builder
                    .AddCircuitBreaker(CreateCircuitBreakerOptions())
                    .AddRetry(CreateSafeRetryOptions())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });

        services.AddResiliencePipeline<string, HttpResponseMessage>(
            HttpResiliencePipelineKeys.SideEffectWrite,
            static builder =>
            {
                builder
                    .AddCircuitBreaker(CreateCircuitBreakerOptions())
                    .AddTimeout(TimeSpan.FromSeconds(15));
            });

        services.AddResiliencePipeline<string, HttpResponseMessage>(
            HttpResiliencePipelineKeys.NotificationDispatch,
            static builder =>
            {
                builder
                    .AddCircuitBreaker(CreateNotificationCircuitBreakerOptions())
                    .AddTimeout(TimeSpan.FromSeconds(15));
            });

        return services;
    }

    private static RetryStrategyOptions<HttpResponseMessage> CreateSafeRetryOptions()
    {
        return new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = TransientHttpPredicate,
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        };
    }

    private static CircuitBreakerStrategyOptions<HttpResponseMessage> CreateCircuitBreakerOptions()
    {
        return new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = TransientHttpPredicate,
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 4,
            BreakDuration = TimeSpan.FromSeconds(20)
        };
    }

    private static CircuitBreakerStrategyOptions<HttpResponseMessage> CreateNotificationCircuitBreakerOptions()
    {
        return new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = TransientHttpPredicate,
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(30)
        };
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }
}