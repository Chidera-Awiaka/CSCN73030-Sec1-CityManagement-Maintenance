using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>Security endpoints (SEC-01, SEC-02). Road closures and work-status updates (SEC-03) are in NoticesController.</summary>
[ApiController]
public class SecurityController : ControllerBase
{
    private readonly SecurityService _service;

    /// <summary>Creates the controller.</summary>
    /// <param name="service">Service that implements the operations.</param>
    public SecurityController(SecurityService service)
    {
        _service = service;
    }

    /// <summary>
    /// Sends an assistance request to Security (SEC-01). 201 with the request and its transmission
    /// status (Succeeded or Failed; a failed send can be retried via /api/exchanges/{exchangeId}/retry).
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Reason, location and period.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/interventions/{id:int}/security-requests")]
    public IActionResult Create(int id, SecurityRequestDto request) =>
        this.ToResult(_service.Request(id, request, RequestContext.Actor(Request)), r => StatusCode(StatusCodes.Status201Created, r));

    /// <summary>Requests for one intervention, with transmission status and any response (SEC-01, SEC-02).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/interventions/{id:int}/security-requests")]
    public IActionResult ForIntervention(int id) => Ok(_service.GetAll(id));

    /// <summary>All requests.</summary>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/security/requests")]
    public IActionResult All() => Ok(_service.GetAll());

    /// <summary>
    /// Receives Security's response (SEC-02). Requires header X-Source-Service: Security; Maintenance
    /// users get 403 because they cannot approve requests on Security's behalf.
    /// </summary>
    /// <param name="response">Response message from Security.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/security/responses")]
    public IActionResult Respond(SecurityResponseDto response)
    {
        var outcome = _service.ReceiveResponse(response, RequestContext.SourceService(Request));
        return this.ToResult(outcome, r => Ok(new { duplicate = outcome.Duplicate, request = r }));
    }
}
