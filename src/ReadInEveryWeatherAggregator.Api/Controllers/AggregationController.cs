using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ReadInEveryWeatherAggregator.Api.Controllers;

//One endpoint that returns data from all external APIs together.
[ApiController]
[Authorize]
[Route("api/aggregation")]
[Produces("application/json")]
public sealed class AggregationController(IAggregationService aggregationService) : ControllerBase
{
    //Returns items from Weather, News and Books in one list, filtered and sorted
    [HttpGet]
    [ProducesResponseType<AggregationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AggregationResponse>> Get(
        // Name = "" lets the URL use ?query=... directly; without it, "query" would clash with the parameter name
        [FromQuery(Name = "")] AggregationQuery query,
        CancellationToken cancellationToken)
    {
        var known = aggregationService.GetProviders().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = query.GetSources().Where(s => !known.Contains(s)).ToList();
        if (unknown.Count > 0)
        {
            ModelState.AddModelError(nameof(query.Sources), $"Unknown source(s): {string.Join(", ", unknown)}. Valid: {string.Join(", ", known)}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await aggregationService.AggregateAsync(query, cancellationToken));
    }

    //Lists the sources you can ask for, and their categories.
    [HttpGet("sources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSources() =>
        Ok(aggregationService.GetProviders().Select(p => new { p.Name, p.Category }));
}
