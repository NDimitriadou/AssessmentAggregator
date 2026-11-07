using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class TokenServiceTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-0123456789-abcdef",
        DemoUsername = "demo",
        DemoPassword = "pass"
    };

    private readonly TokenService _service = new(MsOptions.Create(Jwt), TimeProvider.System);

    [Fact]
    public async Task ValidCredentials_ProduceValidSignedToken()
    {
        var result = _service.CreateToken("demo", "pass");

        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(result.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = TokenService.GetSigningKey(Jwt)
        });
        Assert.True(validation.IsValid);
        Assert.Equal("demo", validation.Claims[JwtRegisteredClaimNames.Sub]);
    }

    [Theory]
    [InlineData("demo", "wrong")]
    [InlineData("other", "pass")]
    [InlineData("", "")]
    public void InvalidCredentials_ReturnNull(string user, string password) =>
        Assert.Null(_service.CreateToken(user, password));
}
