using System.ComponentModel.DataAnnotations;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ReadInEveryWeatherAggregator.Api.Controllers;

public sealed record LoginRequest([Required] string Username, [Required] string Password);

//Login endpoint. No token is needed to call it
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(TokenService tokenService) : ControllerBase
{
    //Send username and password, get back a token. Send the token with other calls as "Authorization: Bearer {token}"
    [HttpPost("token")]
    [ProducesResponseType<TokenResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResult> CreateToken(LoginRequest request)
    {
        var token = tokenService.CreateToken(request.Username, request.Password);
        return token is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.")
            : Ok(token);
    }
}
