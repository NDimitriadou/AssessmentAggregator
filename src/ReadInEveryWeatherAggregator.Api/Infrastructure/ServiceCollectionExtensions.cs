using System.Text;
using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Providers;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;

namespace ReadInEveryWeatherAggregator.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers one external API and gives it an HttpClient that retries failed calls,
    /// gives up on slow calls, and stops calling an API for a while if it keeps failing.
    /// Adding a new API only needs one call to this method.
    /// </summary>
    /// <typeparam name="TInterface">The provider's own interface, e.g. IWeatherProvider.</typeparam>
    /// <typeparam name="TImplementation">The class that calls the API, e.g. WeatherProvider.</typeparam>
    public static IServiceCollection AddExternalApiProvider<TInterface, TImplementation>(this IServiceCollection services, string name, IConfiguration configuration)
        where TInterface : class, IExternalApiProvider
        where TImplementation : class, TInterface
    {
        services.Configure<ExternalApiOptions>(name, configuration.GetSection($"ExternalApis:{name}"));

        var resilience = configuration.GetSection(ResilienceOptions.SectionName).Get<ResilienceOptions>() ?? new ResilienceOptions();

        services.AddHttpClient<TInterface, TImplementation>()
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = resilience.MaxRetryAttempts;
                o.Retry.Delay = TimeSpan.FromMilliseconds(resilience.RetryBaseDelayMs);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(resilience.AttemptTimeoutSeconds);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(resilience.TotalTimeoutSeconds);
                // Required by the library: this window must be at least twice the per-call timeout
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, resilience.AttemptTimeoutSeconds * 2));
            });

        // Also register it as IExternalApiProvider, so the aggregator gets all providers in one list
        services.AddTransient<IExternalApiProvider>(sp => sp.GetRequiredService<TInterface>());
        return services;
    }

    public static IServiceCollection AddAggregationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CachingOptions>(configuration.GetSection(CachingOptions.SectionName));
        services.Configure<StatisticsOptions>(configuration.GetSection(StatisticsOptions.SectionName));
        services.Configure<AnomalyDetectionOptions>(configuration.GetSection(AnomalyDetectionOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<ProviderCache>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<PerformanceAnomalyDetector>();
        services.AddScoped<IAggregationService, AggregationService>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();

        // The signing key must be at least 32 characters long
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
        }

        services.AddSingleton<TokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenService.GetSigningKey(jwt),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "ReadInEveryWeather Aggregator", Version = "v1" });

            var xmlFile = Path.Combine(AppContext.BaseDirectory, "ReadInEveryWeatherAggregator.Api.xml");
            if (File.Exists(xmlFile))
            {
                o.IncludeXmlComments(xmlFile);
            }

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token from POST /api/auth/token",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            o.AddSecurityDefinition("Bearer", scheme);
            o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
        });
        return services;
    }
}
