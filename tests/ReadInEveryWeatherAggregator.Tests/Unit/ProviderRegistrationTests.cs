using ReadInEveryWeatherAggregator.Api.Infrastructure;
using ReadInEveryWeatherAggregator.Api.Providers;
using ReadInEveryWeatherAggregator.Api.Providers.Books;
using ReadInEveryWeatherAggregator.Api.Providers.News;
using ReadInEveryWeatherAggregator.Api.Providers.Weather;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class ProviderRegistrationTests
{
    private static ServiceProvider BuildServices()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services
            .AddExternalApiProvider<IWeatherProvider, WeatherProvider>(WeatherProvider.ProviderName, config)
            .AddExternalApiProvider<INewsProvider, NewsProvider>(NewsProvider.ProviderName, config)
            .AddExternalApiProvider<IBooksProvider, BooksProvider>(BooksProvider.ProviderName, config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void EachProvider_ResolvesThroughItsOwnInterface()
    {
        using var sp = BuildServices();
        Assert.IsType<WeatherProvider>(sp.GetRequiredService<IWeatherProvider>());
        Assert.IsType<NewsProvider>(sp.GetRequiredService<INewsProvider>());
        Assert.IsType<BooksProvider>(sp.GetRequiredService<IBooksProvider>());
    }

    [Fact]
    public void AllProviders_ResolveAsOneList_ForTheAggregator()
    {
        using var sp = BuildServices();
        var names = sp.GetServices<IExternalApiProvider>().Select(p => p.Name);
        Assert.Equal(["Weather", "News", "Books"], names);
    }
}
