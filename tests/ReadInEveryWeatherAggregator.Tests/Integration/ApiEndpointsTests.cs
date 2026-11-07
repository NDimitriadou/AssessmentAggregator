using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Providers;
using ReadInEveryWeatherAggregator.Api.Services;
using ReadInEveryWeatherAggregator.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ReadInEveryWeatherAggregator.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<ApiEndpointsTests.Factory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly Factory _factory;

    public ApiEndpointsTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("AnomalyDetection:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IExternalApiProvider>();
                services.AddSingleton<IExternalApiProvider>(FakeProvider.Returning("News", "news", 3));
                services.AddSingleton<IExternalApiProvider>(FakeProvider.Returning("Books", "books", 2));
                services.AddSingleton<IExternalApiProvider>(FakeProvider.Failing("Weather", "weather"));
            });
        }
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { username = "demo", password = "demo123" });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResult>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    [Theory]
    [InlineData("/api/aggregation")]
    [InlineData("/api/statistics")]
    public async Task ProtectedEndpoints_Require_Token(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/token", new { username = "demo", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Aggregation_ReturnsMergedItems_AndPerSourceStatus()
    {
        var client = await CreateAuthorizedClientAsync();

        var body = await client.GetFromJsonAsync<AggregationResponse>("/api/aggregation?query=dotnet&sortBy=title&order=asc", Json);

        Assert.NotNull(body);
        Assert.Equal(5, body.TotalItems);
        Assert.Equal(SourceState.Failed, body.Sources.Single(s => s.Name == "Weather").State);
        Assert.Equal("Books item 0", body.Items[0].Title); // sorted by title ascending
    }

    [Fact]
    public async Task Aggregation_AppliesQueryParameters_EvenWhenQueryKeywordPresent()
    {
        var client = await CreateAuthorizedClientAsync();

        var body = await client.GetFromJsonAsync<AggregationResponse>("/api/aggregation?query=x&category=news&limit=2", Json);

        Assert.Equal(2, body!.TotalItems);
        Assert.All(body.Items, i => Assert.Equal("news", i.Category));
    }

    [Theory]
    [InlineData("/api/aggregation?sources=unknown")]
    [InlineData("/api/aggregation?from=2025-02-01&to=2025-01-01")]
    [InlineData("/api/aggregation?limit=0")]
    [InlineData("/api/aggregation?sortBy=popularity")]
    public async Task Aggregation_InvalidParameters_Return400(string url)
    {
        var client = await CreateAuthorizedClientAsync();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Statistics_ReturnBucketsPerApi()
    {
        var client = await CreateAuthorizedClientAsync();
        await client.GetAsync("/api/aggregation?query=stats-test");

        var stats = await client.GetFromJsonAsync<StatisticsResponse>("/api/statistics", Json);

        Assert.Contains(stats!.Apis, a => a.Name == "News" && a.TotalRequests >= 1);
        Assert.All(stats.Apis, a => Assert.Equal(["fast", "average", "slow"], a.Buckets.Select(b => b.Name)));
    }

    [Fact]
    public async Task Sources_ListsRegisteredProviders()
    {
        var client = await CreateAuthorizedClientAsync();
        var json = await client.GetStringAsync("/api/aggregation/sources");
        Assert.Contains("Books", json);
        Assert.Contains("weather", json);
    }
}
