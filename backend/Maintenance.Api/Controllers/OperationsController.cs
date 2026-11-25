using Maintenance.Api.DTOs;
using Maintenance.Api.Integration;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>
/// Operational endpoints: exchange log and retries (TEC-04), audit history (TEC-05),
/// and mock service controls for development and testing (TEC-03).
/// </summary>
[ApiController]
public class OperationsController : ControllerBase
{
    private readonly ExchangeService _exchanges;
    private readonly AuditService _audit;
    private readonly ExternalServiceHub _hub;
    private readonly IStaffDirectory _staff;
    private readonly ITransitService _transit;
    private readonly IPropertyRegistry _registry;

    /// <summary>Creates the controller.</summary>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="audit">Audit history (TEC-05).</param>
    /// <param name="hub">Clients for the external services.</param>
    /// <param name="staff">Staff directory (STF-01).</param>
    /// <param name="transit">Public Transport routes (TRP-01).</param>
    /// <param name="registry">Property Registry (REG-01, REG-03).</param>
    public OperationsController(ExchangeService exchanges, AuditService audit, ExternalServiceHub hub,
        IStaffDirectory staff, ITransitService transit, IPropertyRegistry registry)
    {
        _exchanges = exchanges;
        _audit = audit;
        _hub = hub;
        _staff = staff;
        _transit = transit;
        _registry = registry;
    }

    /// <summary>Exchanges with external services, newest first (TEC-04). Use ?status=Failed to see failures.</summary>
    /// <param name="service">Service that implements the operations.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="direction">Optional direction filter: Inbound or Outbound.</param>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/exchanges")]
    public IActionResult Exchanges([FromQuery] string? service, [FromQuery] string? status, [FromQuery] string? direction, [FromQuery] int? interventionId) =>
        Ok(_exchanges.Query(service, status, direction, interventionId));

    /// <summary>Retries a failed outbound exchange (TEC-04). A succeeded exchange is never sent twice.</summary>
    /// <param name="id">Exchange ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/exchanges/{id:int}/retry")]
    public IActionResult Retry(int id)
    {
        var outcome = _exchanges.Retry(id);
        if (outcome is null) return NotFound(new { errors = new[] { "Outbound exchange not found." } });
        return Ok(new { alreadySent = outcome.AlreadySent, succeeded = outcome.Succeeded, exchange = outcome.Exchange });
    }

    /// <summary>Audit history, newest first (TEC-05). Optional ?entityType=Intervention&amp;entityId=3.</summary>
    /// <param name="entityType">Optional record kind, e.g. Intervention.</param>
    /// <param name="entityId">Optional record ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/audit")]
    public IActionResult Audit([FromQuery] string? entityType, [FromQuery] string? entityId) =>
        Ok(_audit.Query(entityType, entityId));

    /// <summary>Mock services and what they have received (TEC-03, development only).</summary>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/mock-services")]
    public IActionResult Mocks() => Ok(MockStates());

    /// <summary>Turns a mock service on or off to test failures and retries (TEC-03, development only).</summary>
    /// <param name="name">Mock service name, e.g. Budget.</param>
    /// <param name="request">True to bring the mock up, false to take it down.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPut("api/mock-services/{name}/availability")]
    public IActionResult SetAvailability(string name, MockAvailabilityDto request)
    {
        var found = false;
        if (_hub.Get(name) is MockExternalService mock) { mock.IsAvailable = request.Available; found = true; }
        if (name.Equals(ExternalServiceNames.Staff, StringComparison.OrdinalIgnoreCase) && _staff is MockStaffDirectory staff) { staff.IsAvailable = request.Available; found = true; }
        if (name.Equals(ExternalServiceNames.PublicTransport, StringComparison.OrdinalIgnoreCase) && _transit is MockTransitService transit) { transit.IsAvailable = request.Available; found = true; }
        if (name.Equals(ExternalServiceNames.PropertyRegistration, StringComparison.OrdinalIgnoreCase) && _registry is MockPropertyRegistry registry) { registry.IsAvailable = request.Available; found = true; }
        return found ? Ok(MockStates()) : NotFound(new { errors = new[] { $"No mock service named {name}." } });
    }

    private object MockStates() => new
    {
        outbound = _hub.All.OfType<MockExternalService>().Select(m => new { service = m.ServiceName, available = m.IsAvailable, received = m.Received }),
        staffAvailable = (_staff as MockStaffDirectory)?.IsAvailable,
        transitAvailable = (_transit as MockTransitService)?.IsAvailable,
        propertyRegistryAvailable = (_registry as MockPropertyRegistry)?.IsAvailable
    };
}
