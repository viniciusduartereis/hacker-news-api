using HackerNewsApi.ExternalServices.HackerNews.Contracts;
using HackerNewsApi.Features.Stories.Contracts;

namespace HackerNewsApi.Features.Stories.Mapping;

public interface IHackerNewsStoryMapper
{
    StoryDto Map(HackerNewsItem item);
}
