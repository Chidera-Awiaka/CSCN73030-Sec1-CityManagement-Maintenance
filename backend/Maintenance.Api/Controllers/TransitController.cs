using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>Public Transport endpoints (TRP-01, TRP-03). Road closures to Transport (TRP-02) are in NoticesController.</summary>
[ApiController]
[Route("api/transit")]
public class TransitController : ControllerBase
{
    private readonly ITransitService _transit;
    private readonly TransitDiversionService _diversions;

    /// <summary>Creates the controller.</summary>
    /// <param name="transit">Public Transport routes (TRP-01).</param>
    /// <param name="diversions">Route-change service (TRP-03).</param>
    public TransitController(ITransitService transit, TransitDiversionService diversions)
    {
        _transit = transit;
        _diversions = diversions;
    }

    /// <summary>
    /// Routes and schedules from Public Transport (TRP-01), optionally only routes on a street.
    /// Shows the source and update time; 503 with available = false when the data cannot be retrieved.
    /// </summary>
    /// <param name="street">Optional text the street name must contain.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("routes")]
    public IActionResult Routes([FromQuery] string? street)
    {
        try
        {
            var routes = _transit.GetRoutes(street);
            return Ok(new
            {
                available = true,
                source = ExternalServiceNames.PublicTransport,
                retrievedAt = DateTime.UtcNow,
                routes = routes.Select(r => new
                {
                    r.RouteId,
                    r.Name,
                    r.Streets,
                    r.Schedule,
                    lastUpdated = r.LastUpdated,
                    lastUpdatedText = r.LastUpdated is null ? "Update time not supplied" : null
                }),
                message = routes.Count == 0 ? "No routes use this street." : null
            });
        }
        catch (TransitUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                available = false,
                source = ExternalServiceNames.PublicTransport,
                message = $"Route and schedule data is unavailable: {ex.Message}"
            });
        }
    }

    /// <summary>Receives a route change / diversion (TRP-03). Requires X-Source-Service: Public Transport.</summary>
    /// <param name="request">Route-change message sent by Public Transport.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("diversions")]
    public IActionResult Receive(RouteDiversionDto request)
    {
        var outcome = _diversions.Receive(request, RequestContext.SourceService(Request));
        return this.ToResult(outcome, d => outcome.Duplicate
            ? Ok(new { duplicate = true, diversion = d })
            : StatusCode(StatusCodes.Status201Created, d));
    }

    /// <summary>Received route changes, optionally only those linked to an intervention (TRP-03).</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("diversions")]
    public IActionResult Diversions([FromQuery] int? interventionId) => Ok(_diversions.GetAll(interventionId));

    /// <summary>Links a route change to an intervention (TRP-03).</summary>
    /// <param name="id">Route change ID.</param>
    /// <param name="request">Intervention to link.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("diversions/{id:int}/links")]
    public IActionResult Link(int id, InterventionLinkDto request) => this.ToResult(_diversions.Link(id, request.InterventionId));

    /// <summary>Removes a link (TRP-03).</summary>
    /// <param name="id">Route change ID.</param>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpDelete("diversions/{id:int}/links/{interventionId:int}")]
    public IActionResult Unlink(int id, int interventionId) => this.ToResult(_diversions.Unlink(id, interventionId));
}
