using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HackerNewsApi.Tests.Fakes;

internal sealed class FakeHackerNewsHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, object?> _responses = new();
    private readonly ConcurrentDictionary<string, int> _requests = new();

    public void AddJsonResponse(string path, object? response)
    {
        _responses[Normalize(path)] = response;
    }

    public int RequestCount(string path)
    {
        return _requests.TryGetValue(Normalize(path), out var count) ? count : 0;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Normalize(request.RequestUri?.PathAndQuery ?? string.Empty);
        _requests.AddOrUpdate(path, 1, static (_, current) => current + 1);

        if (!_responses.TryGetValue(path, out var response))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response, options: JsonOptions),
            RequestMessage = request
        });
    }

    private static string Normalize(string path)
    {
        var normalized = path.TrimStart('/');

        return normalized.StartsWith("v0/", StringComparison.OrdinalIgnoreCase)
            ? normalized["v0/".Length..]
            : normalized;
    }
}
