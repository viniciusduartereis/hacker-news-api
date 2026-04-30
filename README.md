# HackerNews Best Stories API

ASP.NET Core 10 REST API that returns Hacker News stories from the official Firebase API, with both the original best-`n` endpoint and paginated feed endpoints.

The original challenge asks for an API that retrieves IDs from:

```text
https://hacker-news.firebaseio.com/v0/beststories.json
```

and retrieves each story detail from:

```text
https://hacker-news.firebaseio.com/v0/item/{id}.json
```

The API is designed to avoid hammering Hacker News by using distributed caching, bounded outbound concurrency, rate limiting, and a background cache refresh worker.

## Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0+ |
| Docker | 24+ |
| Docker Compose | v2+ |
| k6 | Latest stable, optional for local load tests |

## Running

### Docker Compose

The compose file starts the API, Redis, and the Aspire dashboard.

```bash
docker compose up --build
```

The API is exposed at:

```text
http://localhost:5005
```

Redis is available to the API through:

```text
ConnectionStrings__RedisConnectionString=redis:6379
```

The Aspire dashboard is exposed at:

```text
http://localhost:18888
```

The API exports OpenTelemetry data to the dashboard through the Docker network endpoint:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://hacker-news-aspire-dashboard:18889
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
TELEMETRY_EXPORTER=otlp
```

### .NET CLI

Running directly with `dotnet run` uses `IDistributedCache` backed by in-process memory unless a Redis connection string is configured.

```bash
dotnet run --project src/HackerNewsApi/HackerNewsApi.csproj
```

To use a local Redis instance:

```bash
ConnectionStrings__RedisConnectionString=localhost:6379 \
dotnet run --project src/HackerNewsApi/HackerNewsApi.csproj
```

Scalar/OpenAPI is available in Development mode at:

```text
http://localhost:5005/scalar
```

## API

### `GET /api/v1/stories/{count}`

Returns up to `count` stories ordered by score descending.

| Parameter | Type | Rule |
| --- | --- | --- |
| `count` | integer | Must be between `1` and `500` |

Example:

```http
GET /api/v1/stories/5
```

Response shape:

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

Status codes:

| Code | Meaning |
| --- | --- |
| `200` | Stories returned successfully |
| `400` | Invalid `count` |
| `429` | Rate limit exceeded |
| `500` | Unexpected application or upstream failure |

### `GET /api/v1/stories/{feed}?page=1&pageSize=20`

Returns a paginated Hacker News feed.

Supported feeds:

| Feed aliases | Upstream endpoint |
| --- | --- |
| `best`, `beststories` | `/v0/beststories.json` |
| `top`, `topstories` | `/v0/topstories.json` |
| `new`, `newstories` | `/v0/newstories.json` |

Query parameters:

| Parameter | Default | Rule |
| --- | --- | --- |
| `page` | `1` | Must be `>= 1` |
| `pageSize` | `20` | Must be between `1` and `500` |

Example:

```http
GET /api/v1/stories/topstories?page=2&pageSize=10
```

Response shape:

```json
{
  "items": [
    {
      "title": "Example story",
      "uri": "https://example.com/story",
      "postedBy": "author",
      "time": "2026-04-28T12:00:00+00:00",
      "score": 120,
      "commentCount": 31
    }
  ],
  "page": 2,
  "pageSize": 10,
  "totalItems": 500,
  "totalPages": 50
}
```

### `GET /api/health`

Returns a lightweight health response:

```json
{
  "status": "ok",
  "timestamp": "2026-04-28T00:00:00Z"
}
```

## Configuration

Configuration lives under `HackerNews` and `ConnectionStrings`.

```json
{
  "ConnectionStrings": {
    "RedisConnectionString": ""
  },
  "HackerNews": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0",
    "BestStoriesCacheDuration": "00:05:00",
    "StoryCacheDuration": "00:10:00",
    "CacheRefreshInterval": "00:04:00",
    "CacheRefreshLockDuration": "00:10:00",
    "StorySingleFlightLockDuration": "00:00:30",
    "StorySingleFlightWaitDuration": "00:00:02",
    "CacheWarmupStoryCount": 500,
    "FetchConcurrency": 20
  }
}
```

Cache provider behavior:

| Redis connection string | Cache implementation |
| --- | --- |
| Empty | `DistributedMemoryCache` |
| Present | Redis via `Microsoft.Extensions.Caching.StackExchangeRedis` |

## Design Decisions

### Endpoint contracts and validation

Minimal API endpoints bind route/query values into request classes instead of validating loose primitive parameters in the endpoint body.

Validation is implemented with FluentValidation and registered through dependency injection. Endpoint handlers receive the matching `IValidator<TRequest>`, return the shared validation error response when validation fails, and call the application service only after the request is valid.

Current request validators cover:

| Request | Rules |
| --- | --- |
| `GetBestStoriesRequest` | `count` must be between `1` and `500` |
| `GetPagedStoriesRequest` | `feed` must be supported, `page` must be `>= 1`, and `pageSize` must be between `1` and `500` |

### Service boundaries

The story workflow follows a vertical-slice layout. Feature-specific application code lives under `Features/Stories`, while the HTTP integration with the upstream Hacker News API lives under `ExternalServices/HackerNews`.

The slice is split into small services so the application service does not own HTTP, cache, mapping, warm-up, and orchestration concerns at the same time.

| Component | Responsibility |
| --- | --- |
| `HackerNewsService` | Application use cases: best stories, paged feed, ranking, and pagination |
| `HackerNewsClient` | External HTTP calls to the official Hacker News Firebase API |
| `CachedHackerNewsStoryProvider` | Feed/story cache lookup, cache population, single-flight protection, and outbound concurrency limits |
| `IHackerNewsCache` / `DistributedHackerNewsCache` | Slice-specific cache abstraction for Hacker News feed and story data |
| `HackerNewsStoryMapper` | Mapping raw `HackerNewsItem` responses to public `StoryDto` responses |
| `HackerNewsCacheWarmer` | Background warm-up orchestration for configured feeds |
| `HackerNewsSettings` | Slice-specific configuration for base URL, cache TTLs, warm-up, locks, and fetch concurrency |
| `AddStoriesFeature` | Slice-level dependency injection registration |

### Distributed cache abstraction

The application uses `IDistributedCache` behind a small `IHackerNewsCache` abstraction. This keeps the service independent from Redis-specific APIs while allowing the runtime cache backend to change through configuration.

Cache backend failures are treated as cache misses and logged, so a Redis outage does not automatically prevent the API from serving data from Hacker News.

Redis itself is configured with `AbortOnConnectFail=false`, three connection retries, and 3-second connect/sync timeouts to avoid long request stalls when the cache backend is degraded.

Redis is also part of readiness health checks when configured. `/readyz` and `/health/ready` report unhealthy if Redis is unreachable, while liveness remains independent through `/healthz` and `/health/live`.

### Resilience pipelines

Polly v8 resilience pipelines are registered for both upstream HTTP reads and distributed cache operations.

| Dependency | Pipeline behavior |
| --- | --- |
| Hacker News API | Circuit breaker, exponential retry with jitter, and 10-second timeout for safe read calls |
| Redis/distributed cache | Circuit breaker, short exponential retry with jitter, and 2-second timeout |

The cache pipeline is intentionally fail-open: after retries/timeouts/circuit breaking, failures are logged and treated as cache misses. The HTTP pipeline is not fail-open because Hacker News is the source of truth when the cache cannot satisfy a request.

### Observability

OpenTelemetry is configured for logs, traces and metrics. In Docker Compose, all three are exported to the Aspire dashboard through OTLP/gRPC.

Custom spans cover:

| Span | Purpose |
| --- | --- |
| `hackernews.http.get` | Upstream Hacker News calls, including path and status code |
| `cache.get` / `cache.set` | Distributed cache read/write operations, including cache hit and TTL tags |
| `hackernews.cache_refresh` | Background warm-up/refresh cycles and lock acquisition outcome |
| `hackernews.feed_ids.get` | Feed ID retrieval with feed name, force-refresh flag, count and cache hit |
| `hackernews.story.get` | Story retrieval with story ID, cache hit and single-flight behavior |

Export is controlled with `TELEMETRY_EXPORTER`:

| Value | Exporter |
| --- | --- |
| `otlp` | OpenTelemetry Protocol exporter, used by the Docker Compose Aspire dashboard |
| `console` | Console exporter |
| empty | Local instrumentation is registered but no exporter is attached |

### Background cache warm-up

`HackerNewsCacheRefreshService` runs when the API starts and then periodically refreshes the cache. Startup is not blocked by a seed step; if the warm-up fails, requests can still fetch missing stories on demand.

When Redis is configured, the background refresh is protected by a distributed Redis lock using atomic lock acquisition. This prevents multiple pods/containers from refreshing the same cache data at the same time and avoids unnecessary load on both Hacker News and Redis. If another instance already owns the lock, the current instance skips that refresh cycle.

When Redis is not configured, the same abstraction falls back to an in-process lock, which prevents overlapping refreshes within a single local API process.

### Two-level cache

| Cache entry | Default TTL | Purpose |
| --- | --- | --- |
| Feed ID list | 5 minutes | Avoid repeated calls to `/beststories.json`, `/topstories.json` and `/newstories.json` |
| Individual story details | 10 minutes | Avoid repeated calls to `/item/{id}.json` |

### Story single-flight

Story-detail cache misses are protected by a per-story lock. With Redis configured, the lock is distributed, so concurrent pods do not all fetch the same `/item/{id}.json` at once. Without Redis, the same abstraction falls back to an in-process keyed lock.

If another request owns the lock, the current request waits briefly for the cache to be populated. If the wait times out, it fetches the story as a fallback rather than failing the user request.

### Bounded outbound concurrency

Story details are fetched concurrently, but `FetchConcurrency` caps simultaneous calls to the Hacker News API. The default is `20`.

### Ranking and pagination strategy

Hacker News returns feed IDs using its own ranking algorithm. The original `/{count}` endpoint fetches the best-story details, sorts by `score`, and then applies `count` to satisfy the challenge requirement.

The paginated endpoint first slices the requested page of feed IDs, then fetches only those story details and sorts the page items by score. This avoids loading all 500 stories on every paginated request.

### Rate limiting

The stories endpoint uses a fixed-window rate limiter: 60 requests per minute per remote IP with a small queue. This protects the API and indirectly protects the upstream Hacker News API.

## Project Structure

```text
src/HackerNewsApi/
├── Caching/
│   ├── ICacheRefreshLock.cs
│   ├── InMemoryCacheRefreshLock.cs
│   └── RedisCacheRefreshLock.cs
├── Configurations/
├── ExternalServices/
│   └── HackerNews/
│       ├── Contracts/
│       │   └── HackerNewsItem.cs
│       ├── HackerNewsClient.cs
│       └── IHackerNewsClient.cs
├── Features/
│   ├── Health/
│   └── Stories/
│       ├── BackgroundServices/
│       ├── Caching/
│       ├── Contracts/
│       ├── Mapping/
│       ├── Providers/
│       ├── Services/
│       ├── Settings/
│       ├── Validators/
│       ├── StoriesServiceCollectionExtensions.cs
│       └── Endpoints.cs
├── Program.cs
└── Dockerfile

tests/
└── HackerNewsApi.Tests/
    ├── Contract/
    ├── Integration/
    ├── Unit/
    ├── Fakes/
    └── Fixtures/

perf/
└── k6/
    └── hackernews-smoke.js
```

## Verification

Build the API:

```bash
dotnet build src/HackerNewsApi/HackerNewsApi.csproj
```

Restore and run tests:

```bash
dotnet restore tests/HackerNewsApi.Tests/HackerNewsApi.Tests.csproj
dotnet test tests/HackerNewsApi.Tests/HackerNewsApi.Tests.csproj
```

The test project includes unit tests, endpoint integration tests, validation response checks, and contract tests against recorded Hacker News API fixtures under `tests/HackerNewsApi.Tests/Fixtures/HackerNews`.

Run a local k6 smoke benchmark:

```bash
docker compose up --build
BASE_URL=http://localhost:5005 RATE=10 DURATION=1m k6 run perf/k6/hackernews-smoke.js
```

The k6 script exercises the original best-`n` endpoint and the paginated `beststories`, `topstories`, and `newstories` feeds. Each k6 iteration performs 4 requests, so the smoke default uses `RATE=10` iterations/minute to stay below the API rate limit of 60 requests/minute per IP. It fails if more than 1% of requests fail or if p95 latency exceeds 1 second.

To intentionally validate rate limiting under pressure, run:

```bash
BASE_URL=http://localhost:5005 RATE=120 DURATION=1m EXPECT_RATE_LIMITS=true k6 run perf/k6/hackernews-smoke.js
```

In that mode, `429 Too Many Requests` is treated as an expected response so the test validates that the limiter is protecting the API instead of reporting those responses as failures.

## Assumptions

- The maximum valid `count` is `500`, matching the maximum number of IDs returned by Hacker News best stories.
- Native Hacker News posts may not have an external URL, so `uri` can be omitted/null.
- Redis is preferred for Docker/local multi-instance scenarios, while in-memory distributed cache is acceptable for simple local runs.

## Enhancements Given More Time

- Add configurable cache TTLs per feed if production usage shows different freshness requirements for `beststories`, `topstories` and `newstories`.
