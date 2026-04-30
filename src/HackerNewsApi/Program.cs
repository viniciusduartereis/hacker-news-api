using Scalar.AspNetCore;
using HackerNewsApi.Features.Health;
using HackerNewsApi.Features.Stories;
using HackerNewsApi.Configurations;
using HackerNewsApi.IoC;
using HackerNewsApi.Features.Stories.Settings;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});


builder.Services.Configure<HackerNewsSettings>(builder.Configuration.GetSection(HackerNewsSettings.SectionName));

builder.Services.AddCache(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();

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
app.SetupHealthCheck();

var versionedApi = app.SetupVersioning();
versionedApi.MapStoriesEndpoints();

app.UseForwardedHeaders();

await app.RunAsync();
