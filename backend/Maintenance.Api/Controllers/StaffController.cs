using Maintenance.Api.Integration;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>Staff endpoints (STF-01) and overlap check (STF-02).</summary>
[ApiController]
public class StaffController : ControllerBase
{
    private readonly IStaffDirectory _staff;
    private readonly InterventionService _interventions;

    /// <summary>Creates the controller.</summary>
    /// <param name="staff">Staff directory (STF-01).</param>
    /// <param name="interventions">Intervention service.</param>
    public StaffController(IStaffDirectory staff, InterventionService interventions)
    {
        _staff = staff;
        _interventions = interventions;
    }

    /// <summary>
    /// Personnel with IDs, names and specializations from Staff, optionally filtered by
    /// specialization (STF-01). 503 when Staff is unavailable.
    /// </summary>
    /// <param name="specialization">Optional specialization filter, e.g. Road.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/staff")]
    public IActionResult Personnel([FromQuery] string? specialization)
    {
        try
        {
            var people = _staff.GetPersonnel(specialization);
            return Ok(new
            {
                source = ExternalServiceNames.Staff,
                retrievedAt = DateTime.UtcNow,
                specializations = _staff.GetPersonnel().SelectMany(p => p.Specializations).Distinct().OrderBy(s => s),
                personnel = people
            });
        }
        catch (StaffUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { ex.Message } });
        }
    }

    /// <summary>Overlapping assignments for an intervention's technician and planned period (STF-02).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/interventions/{id:int}/conflicts")]
    public IActionResult Conflicts(int id)
    {
        var target = _interventions.GetById(id);
        if (target is null) return NotFound(new { errors = new[] { "Intervention not found." } });
        var conflicts = AssignmentConflicts.For(_interventions.GetAll(), target);
        return Ok(new { interventionId = id, hasConflicts = conflicts.Count > 0, conflicts, scope = AssignmentConflicts.Scope });
    }
}
