using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;

namespace HackerNewsApi.Configurations;

public static class CacheResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationCacheResiliencePipelines(this IServiceCollection services)
    {
        services.AddResiliencePipeline(
            CacheResiliencePipelineKeys.DistributedCache,
            static builder =>
            {
                builder
                    .AddCircuitBreaker(CreateCircuitBreakerOptions())
                    .AddRetry(CreateRetryOptions())
                    .AddTimeout(TimeSpan.FromSeconds(2));
            });

        return services;
    }

    private static RetryStrategyOptions CreateRetryOptions()
    {
        return new RetryStrategyOptions
        {
            ShouldHandle = static args => ValueTask.FromResult(ShouldHandle(args.Outcome.Exception)),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        };
    }

    private static CircuitBreakerStrategyOptions CreateCircuitBreakerOptions()
    {
        return new CircuitBreakerStrategyOptions
        {
            ShouldHandle = static args => ValueTask.FromResult(ShouldHandle(args.Outcome.Exception)),
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(20),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(15)
        };
    }

    private static bool ShouldHandle(Exception? exception)
    {
        return exception is not null and not OperationCanceledException;
    }
}
