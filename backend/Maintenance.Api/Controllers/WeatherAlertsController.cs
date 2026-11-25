using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Models;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>
/// Weather alerts from E-services. Base route: /api/weather-alerts
/// ESV-03 receive / list, ESV-04 assess, ESV-05 act.
/// </summary>
[ApiController]
[Route("api/weather-alerts")]
public class WeatherAlertsController : ControllerBase
{
    private readonly WeatherAlertService _service;

    /// <summary>Creates the controller.</summary>
    /// <param name="service">Service that implements the operations.</param>
    public WeatherAlertsController(WeatherAlertService service)
    {
        _service = service;
    }

    /// <summary>
    /// Receives a weather alert from E-services (ESV-03).
    /// 201 with the new alert; 200 with { duplicate: true, alert } when it was already received; 400 invalid.
    /// </summary>
    /// <param name="request">Alert message sent by E-services.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost]
    public IActionResult Receive(IncomingWeatherAlertDto request)
    {
        var outcome = _service.Receive(request);
        return this.ToResult(outcome, alert => outcome.Duplicate
            ? Ok(new { duplicate = true, alert })
            : CreatedAtAction(nameof(GetById), new { id = alert.Id }, alert));
    }

    /// <summary>Lists alerts, newest first (ESV-03). Optional ?reviewStatus=Awaiting Review|Reviewed.</summary>
    /// <param name="reviewStatus">Awaiting Review or Reviewed.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? reviewStatus)
    {
        if (!string.IsNullOrWhiteSpace(reviewStatus)
            && !WeatherAlertRules.ReviewStatuses.Contains(reviewStatus.Trim(), StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { errors = new[] { "Review status must be Awaiting Review or Reviewed." } });
        return Ok(_service.GetAll(reviewStatus));
    }

    /// <summary>Gets one alert with its assessments and actions.</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var alert = _service.GetById(id);
        return alert is null ? NotFound(new { errors = new[] { "Weather alert not found." } }) : Ok(alert);
    }

    /// <summary>Records an assessment (ESV-04): decision, rationale, reviewer and date. Marks the alert Reviewed.</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="request">Decision, rationale and optional reviewer.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("{id:int}/assessments")]
    public IActionResult Assess(int id, WeatherAssessmentDto request) =>
        this.ToResult(_service.Assess(id, request, RequestContext.Actor(Request)));

    /// <summary>Marks an alert Awaiting Review or Reviewed (ESV-04).</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="request">New review status.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPut("{id:int}/review-status")]
    public IActionResult SetReviewStatus(int id, WeatherReviewStatusDto request) =>
        this.ToResult(_service.SetReviewStatus(id, request?.ReviewStatus ?? "", RequestContext.Actor(Request)));

    /// <summary>
    /// Takes an action from the alert (ESV-05): CreateIntervention, LinkInterventions,
    /// NoActionRequired, Reschedule or PutOnHold.
    /// </summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="request">Action type and its details.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("{id:int}/actions")]
    public IActionResult TakeAction(int id, WeatherAlertActionDto request) =>
        this.ToResult(_service.TakeAction(id, request, RequestContext.Actor(Request)));
}
