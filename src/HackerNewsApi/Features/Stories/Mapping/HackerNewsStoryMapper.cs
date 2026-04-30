using HackerNewsApi.ExternalServices.HackerNews.Contracts;
using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Mapping;

public sealed class HackerNewsStoryMapper : IHackerNewsStoryMapper
{
    public StoryDto Map(HackerNewsItem item)
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(item.Time)
                                 .ToString("yyyy-MM-dd'T'HH:mm:sszzz");

        return new StoryDto(
            Title: item.Title ?? string.Empty,
            Uri: item.Url,
            PostedBy: item.By ?? string.Empty,
            Time: time,
            Score: item.Score,
            CommentCount: item.Descendants);
    }
}
