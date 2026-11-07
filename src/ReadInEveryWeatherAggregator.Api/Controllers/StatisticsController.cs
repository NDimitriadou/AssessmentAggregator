using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ReadInEveryWeatherAggregator.Api.Controllers;

//Shows how the external APIs have been performing
[ApiController]
[Authorize]
[Route("api/statistics")]
[Produces("application/json")]
public sealed class StatisticsController(IStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// For each external API: how many calls were made, how many failed, how long they took on average,
    /// and how many were fast, average or slow.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<StatisticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<StatisticsResponse> Get() => Ok(statisticsService.GetStatistics());
}
