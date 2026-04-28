using Polly;

namespace HackerNewsApi.Configurations;

internal static class HttpResiliencePipelineExtensions
{
    public static ValueTask<HttpResponseMessage> SendAsync(
        this ResiliencePipeline<HttpResponseMessage> pipeline,
        HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(request);

        return pipeline.ExecuteAsync(
            static async (state, token) =>
            {
                using var clonedRequest = await state.Request.CloneAsync(token);
                return await state.HttpClient.SendAsync(clonedRequest, token);
            },
            new HttpPipelineExecutionState(httpClient, request),
            cancellationToken);
    }

    private readonly record struct HttpPipelineExecutionState(HttpClient HttpClient, HttpRequestMessage Request);

    private static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
