using FluentValidation;
using Scalar.AspNetCore;
using HackerNewsApi.Features.Health;
using HackerNewsApi.Features.Stories;
using HackerNewsApi.Configurations;
using HackerNewsApi.Settings;
using HackerNewsApi.IoC;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});


builder.Services.Configure<HackerNewsSettings>(builder.Configuration.GetSection(HackerNewsSettings.SectionName));

var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnectionString");
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.ConnectRetry = 3;
        redisOptions.ConnectTimeout = 3_000;
        redisOptions.SyncTimeout = 3_000;

        options.ConfigurationOptions = redisOptions;
        options.InstanceName = "HackerNews:";
    });
}

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddOpenApi();
builder.Services.AddHealthCheck();
builder.Services.AddVersioning();
builder.Services.AddRateLimiting();
builder.Services.AddAppInsightsTelemetry(builder.Configuration);
builder.Logging.AddLoggingInsightsTelemetry(builder.Configuration);

builder.Services.RegisterDependeciesApi(builder.Configuration);
builder.Services.ConfigureHttpHackerNewsClient(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.Title = "Hacker News API";
        options.Theme = ScalarTheme.DeepSpace; 
    });
    app.UseDeveloperExceptionPage();
}
app.UseRateLimiter();
app.MapOpenApi();

if (builder.Configuration.GetValue("AppSettings:EnableHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.MapHealthEndpoints();

var versionedApi = app.SetupVersioning();
versionedApi.MapStoriesEndpoints();

app.UseForwardedHeaders();

await app.RunAsync();

public partial class Program;
