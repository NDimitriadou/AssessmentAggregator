using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ReadInEveryWeatherAggregator.Api.Services;

public sealed record TokenResult(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public TokenResult? CreateToken(string username, string password)
    {
        if (!IsValidUser(username, password))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, username), new Claim(ClaimTypes.Name, username)]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(GetSigningKey(_options), SecurityAlgorithms.HmacSha256)
        };

        return new TokenResult(new JsonWebTokenHandler().CreateToken(descriptor), "Bearer", expires);
    }

    public static SymmetricSecurityKey GetSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    // Compare in a way that always takes the same time, so attackers cannot guess the password by timing
    private bool IsValidUser(string username, string password) =>
        !string.IsNullOrEmpty(_options.DemoPassword) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(username ?? ""), Encoding.UTF8.GetBytes(_options.DemoUsername)) &
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password ?? ""), Encoding.UTF8.GetBytes(_options.DemoPassword));
}
