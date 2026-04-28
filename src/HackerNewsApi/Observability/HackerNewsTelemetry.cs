using System.Diagnostics;

namespace HackerNewsApi.Observability;

public static class HackerNewsTelemetry
{
    public const string ActivitySourceName = "HackerNewsApi";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
