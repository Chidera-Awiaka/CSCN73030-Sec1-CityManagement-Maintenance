using Microsoft.AspNetCore.Mvc;
using Maintenance.Api.DTOs;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Controllers;

/// <summary>
/// REST endpoints for incident reports received from E-services.
/// Base route: /api/incident-reports
///
/// Related requirements:
/// ESV-01 - Receive incident reports from E-services
/// ESV-02 - Accept or reject a report
/// </summary>
[ApiController]
[Route("api/incident-reports")]
public class IncidentReportsController : ControllerBase
{
    private readonly IncidentReportService _service;

    /// <summary>Creates the controller.</summary>
    public IncidentReportsController(IncidentReportService service)
    {
        _service = service;
    }

    /// <summary>
    /// Receives an incident report from E-services (ESV-01).
    /// Returns 201 with the stored report, 400 with validation errors,
    /// or 409 when the same E-services report was already received.
    /// </summary>
    [HttpPost]
    public IActionResult Receive(IncomingIncidentReportDto request)
    {
        var outcome = _service.Receive(request);
        return outcome.Result == ReviewResult.Success
            ? CreatedAtAction(nameof(GetById), new { id = outcome.Report!.Id }, outcome.Report)
            : ToError(outcome);
    }

    /// <summary>
    /// Lists incident reports, newest first (ESV-01).
    /// Example: GET /api/incident-reports?status=Pending
    /// </summary>
    /// <param name="status">Optional review state: Pending, Accepted or Rejected.</param>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? status)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && !IncidentReportRules.Statuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Status must be Pending, Accepted or Rejected." } });
        }

        return Ok(_service.GetAll(status));
    }

    /// <summary>Gets one incident report by ID.</summary>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var report = _service.GetById(id);
        return report is null ? NotFound(new { errors = new[] { "Incident report not found." } }) : Ok(report);
    }

    /// <summary>
    /// Accepts a pending report and creates an intervention from it (ESV-02).
    /// Returns 200, 400, 404, or 409 when it was already reviewed.
    /// </summary>
    [HttpPost("{id:int}/accept")]
    public IActionResult Accept(int id, AcceptIncidentReportDto request)
    {
        var outcome = _service.Accept(id, request);
        return outcome.Result == ReviewResult.Success ? Ok(outcome.Report) : ToError(outcome);
    }

    /// <summary>
    /// Rejects a pending report with a reason (ESV-02).
    /// Returns 200, 400, 404, or 409 when it was already reviewed.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    public IActionResult Reject(int id, RejectIncidentReportDto request)
    {
        var outcome = _service.Reject(id, request);
        return outcome.Result == ReviewResult.Success ? Ok(outcome.Report) : ToError(outcome);
    }

    /// <summary>Maps a failed outcome to the matching HTTP status code.</summary>
    private IActionResult ToError(ReviewOutcome outcome)
    {
        var body = new { errors = outcome.Errors };
        return outcome.Result switch
        {
            ReviewResult.NotFound => NotFound(body),
            ReviewResult.Conflict => Conflict(body),
            _ => BadRequest(body)
        };
    }
}
