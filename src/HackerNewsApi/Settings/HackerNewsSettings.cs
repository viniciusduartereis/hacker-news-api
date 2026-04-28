namespace HackerNewsApi.Settings;

public class HackerNewsSettings
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0";

    public TimeSpan BestStoriesCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan StoryCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan CacheRefreshInterval { get; set; } = TimeSpan.FromMinutes(4);

    public TimeSpan CacheRefreshLockDuration { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan StorySingleFlightLockDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan StorySingleFlightWaitDuration { get; set; } = TimeSpan.FromSeconds(2);

    public int CacheWarmupStoryCount { get; set; } = 500;

    public int FetchConcurrency { get; set; } = 20;
}
