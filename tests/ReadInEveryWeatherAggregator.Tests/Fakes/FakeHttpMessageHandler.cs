using System.Net;
using System.Text;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Tests.Fakes;

// Returns ready-made HTTP responses, so providers can be tested without internet
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public sealed class StaticOptionsMonitor(ExternalApiOptions value) : IOptionsMonitor<ExternalApiOptions>
{
    public ExternalApiOptions CurrentValue => value;
    public ExternalApiOptions Get(string? name) => value;
    public IDisposable? OnChange(Action<ExternalApiOptions, string?> listener) => null;
}
