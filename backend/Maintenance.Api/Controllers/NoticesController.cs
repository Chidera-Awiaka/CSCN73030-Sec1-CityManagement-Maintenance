using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>
/// Notices sent to other services: road closures (ESV-06, SEC-03, TRP-02), work-status updates (SEC-03)
/// and property-related service disruptions (REG-04).
/// </summary>
[ApiController]
public class NoticesController : ControllerBase
{
    private readonly NoticeService _service;

    /// <summary>Creates the controller.</summary>
    /// <param name="service">Service that implements the operations.</param>
    public NoticesController(NoticeService service)
    {
        _service = service;
    }

    /// <summary>
    /// Sends a road closure, update or reopening (ESV-06 / SEC-03 / TRP-02) to E-services, Security and
    /// Public Transport (or the services listed). 200 with the notice and per-service delivery results.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Closure status, location, dates and services to notify.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/interventions/{id:int}/road-closures")]
    public IActionResult RoadClosure(int id, RoadClosureDto request) =>
        Result(_service.SendRoadClosure(id, request, RequestContext.Actor(Request)));

    /// <summary>Sends the current work status to Security (or the services listed) (SEC-03).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Services to notify (optional).</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/interventions/{id:int}/work-status-updates")]
    public IActionResult WorkStatus(int id, WorkStatusUpdateDto? request) =>
        Result(_service.SendWorkStatus(id, request, RequestContext.Actor(Request)));

    /// <summary>Sends a property-related service disruption to the Property Registry (REG-04).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Disruption type, update type and period.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/interventions/{id:int}/property-disruptions")]
    public IActionResult PropertyDisruption(int id, PropertyDisruptionDto request) =>
        Result(_service.SendPropertyDisruption(id, request, RequestContext.Actor(Request)));

    /// <summary>Notices sent for an intervention, with delivery status.</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="kind">Optional notice kind: RoadClosure, WorkStatusUpdate or PropertyDisruption.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/interventions/{id:int}/notices")]
    public IActionResult ForIntervention(int id, [FromQuery] string? kind) => Ok(_service.GetAll(id, kind));

    /// <summary>All notices, optionally by kind (RoadClosure, WorkStatusUpdate, PropertyDisruption).</summary>
    /// <param name="kind">Optional notice kind: RoadClosure, WorkStatusUpdate or PropertyDisruption.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/notices")]
    public IActionResult All([FromQuery] string? kind) => Ok(_service.GetAll(null, kind));

    private IActionResult Result(ServiceOutcome<Models.OutboundNotice> outcome) =>
        this.ToResult(outcome, n => Ok(new
        {
            duplicate = outcome.Duplicate,
            allDelivered = n.Deliveries.All(d => d.Status == Models.ExchangeRules.Succeeded),
            notice = n
        }));
}
