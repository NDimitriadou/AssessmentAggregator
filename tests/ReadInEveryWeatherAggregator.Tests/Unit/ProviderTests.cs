using System.Net;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Providers.Books;
using ReadInEveryWeatherAggregator.Api.Providers.News;
using ReadInEveryWeatherAggregator.Api.Providers.Weather;
using ReadInEveryWeatherAggregator.Tests.Fakes;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class ProviderTests
{
    private static readonly ProviderRequest Request = new("dotnet", "Athens");

    [Fact]
    public async Task WeatherProvider_GeocodesCity_AndMapsDailyForecast()
    {
        var handler = new FakeHttpMessageHandler(req => req.RequestUri!.Host.StartsWith("geo")
            ? FakeHttpMessageHandler.Json("""{"results":[{"name":"Athens","country":"Greece","latitude":37.98,"longitude":23.72}]}""")
            : FakeHttpMessageHandler.Json("""
                {"daily":{"time":["2025-01-01","2025-01-02"],
                  "temperature_2m_max":[15.2,16],"temperature_2m_min":[8,9.5],
                  "precipitation_sum":[0,2.5],"weathercode":[0,61]}}
                """));
        var provider = new WeatherProvider(new HttpClient(handler), new StaticOptionsMonitor(new ExternalApiOptions
        {
            BaseUrl = "https://api.example.com/", GeocodingUrl = "https://geo.example.com/", MaxItems = 2
        }));

        var items = await provider.FetchAsync(Request, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("name=Athens", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("latitude=37.98", handler.Requests[1].RequestUri!.Query); // uses a dot, not a comma, whatever the PC's language
        Assert.Equal(2, items.Count);
        Assert.Equal("Athens: Clear sky, 8°C – 15.2°C", items[0].Title);
        Assert.Equal("Athens: Rain, 9.5°C – 16°C", items[1].Title);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), items[0].Date);
        Assert.All(items, i => Assert.Equal("weather", i.Category));
    }

    [Fact]
    public async Task WeatherProvider_ReturnsEmpty_WhenCityNotFound()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("{}"));
        var provider = new WeatherProvider(new HttpClient(handler), new StaticOptionsMonitor(new ExternalApiOptions
        {
            BaseUrl = "https://api.example.com/", GeocodingUrl = "https://geo.example.com/"
        }));

        var items = await provider.FetchAsync(Request, CancellationToken.None);

        Assert.Empty(items);
        Assert.Single(handler.Requests); // forecast is not requested
    }

    [Fact]
    public async Task NewsProvider_MapsHits_AndFallsBackToDiscussionUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""
            {"hits":[
              {"title":"Story A","url":"https://a.com","author":"bob","points":10,"num_comments":3,"created_at":"2025-01-01T10:00:00Z","objectID":"1"},
              {"title":"Ask HN","url":null,"author":"amy","points":5,"num_comments":1,"created_at":"2025-01-02T10:00:00Z","objectID":"2"},
              {"title":null,"objectID":"3"}
            ]}
            """));
        var provider = new NewsProvider(new HttpClient(handler), new StaticOptionsMonitor(new ExternalApiOptions { BaseUrl = "https://hn.example.com", MaxItems = 5 }));

        var items = await provider.FetchAsync(Request, CancellationToken.None);

        Assert.Contains("query=dotnet", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("hitsPerPage=5", handler.Requests[0].RequestUri!.Query);
        Assert.Equal(2, items.Count); // item without title is skipped
        Assert.Equal("https://a.com", items[0].Url);
        Assert.Equal("https://news.ycombinator.com/item?id=2", items[1].Url);
        Assert.Equal("by bob · 10 points · 3 comments", items[0].Description);
        Assert.Equal("news", items[0].Category);
    }

    [Fact]
    public async Task BooksProvider_SendsUserAgent_AndMapsBooks()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""
            {"docs":[{"key":"/works/OL1W","title":".NET and XML","author_name":["Niel M. Bornstein"],
                      "first_publish_year":2004,"edition_count":3},
                     {"key":"/works/OL2W"}]}
            """));
        var provider = new BooksProvider(new HttpClient(handler), new StaticOptionsMonitor(new ExternalApiOptions { BaseUrl = "https://ol.example.com/", MaxItems = 5 }));

        var items = await provider.FetchAsync(Request, CancellationToken.None);

        var sent = handler.Requests[0];
        Assert.NotEmpty(sent.Headers.UserAgent);
        Assert.Contains("q=dotnet", sent.RequestUri!.Query);
        Assert.Contains("limit=5", sent.RequestUri!.Query);
        var item = Assert.Single(items); // book without title is skipped
        Assert.Equal(".NET and XML", item.Title);
        Assert.Equal("by Niel M. Bornstein · first published 2004 · 3 editions", item.Description);
        Assert.Equal("https://ol.example.com/works/OL1W", item.Url);
        Assert.Equal(new DateTimeOffset(2004, 1, 1, 0, 0, 0, TimeSpan.Zero), item.Date);
        Assert.Equal("books", item.Category);
    }

    [Fact]
    public async Task BooksProvider_Throws_OnErrorStatus_SoFallbackCanApply()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        var provider = new BooksProvider(new HttpClient(handler), new StaticOptionsMonitor(new ExternalApiOptions { BaseUrl = "https://ol.example.com/" }));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchAsync(Request, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(3, "Overcast")]
    [InlineData(63, "Rain")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(null, "Unknown")]
    public void WeatherProvider_DescribesWeatherCodes(int? code, string expected) =>
        Assert.Equal(expected, WeatherProvider.DescribeWeatherCode(code));
}
