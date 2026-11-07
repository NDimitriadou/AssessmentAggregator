using System.Text.Json.Serialization;
using ReadInEveryWeatherAggregator.Api.BackgroundServices;
using ReadInEveryWeatherAggregator.Api.Infrastructure;
using ReadInEveryWeatherAggregator.Api.Providers.Books;
using ReadInEveryWeatherAggregator.Api.Providers.News;
using ReadInEveryWeatherAggregator.Api.Providers.Weather;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add a new API here with one line
builder.Services
    .AddExternalApiProvider<IWeatherProvider, WeatherProvider>(WeatherProvider.ProviderName, config)
    .AddExternalApiProvider<INewsProvider, NewsProvider>(NewsProvider.ProviderName, config)
    .AddExternalApiProvider<IBooksProvider, BooksProvider>(BooksProvider.ProviderName, config);

builder.Services.AddAggregationServices(config);
builder.Services.AddHostedService<PerformanceMonitorService>();
builder.Services.AddJwtAuthentication(config);
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Made public so the integration tests can start the app
public partial class Program;
