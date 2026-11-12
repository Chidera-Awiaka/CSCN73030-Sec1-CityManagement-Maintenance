using Microsoft.AspNetCore.Mvc;
using Maintenance.Api.DTOs;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Controllers;

/// <summary>
/// REST endpoints for maintenance interventions.
/// Base route: /api/interventions
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InterventionsController : ControllerBase
{
    private readonly InterventionService _service;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    public InterventionsController(InterventionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lists interventions, optionally filtered (MNT-02).
    ///
    /// Example:
    /// GET /api/interventions?priority=High&amp;location=King&amp;status=To%20Do
    /// </summary>
    /// <param name="priority">Optional exact priority.</param>
    /// <param name="location">Optional text the location must contain.</param>
    /// <param name="status">Optional exact operational status.</param>
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? priority,
        [FromQuery] string? location,
        [FromQuery] string? status)
    {
        return Ok(
            _service.Filter(
                priority,
                location,
                status
            )
        );
    }

    /// <summary>
    /// Gets one intervention by ID.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var intervention = _service.GetById(id);

        if (intervention is null)
        {
            return NotFound("Intervention not found.");
        }

        return Ok(intervention);
    }

    /// <summary>
    /// Creates an intervention (MNT-01).
    /// Returns 201 with the new intervention,
    /// or 400 with validation errors.
    /// </summary>
    [HttpPost]
    public IActionResult Create(CreateInterventionDto request)
    {
        var errors = InterventionService.Validate(request);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                errors
            });
        }

        var created = _service.Create(request);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = created.Id
            },
            created
        );
    }

    /// <summary>
    /// Updates the priority of an intervention (MNT-03).
    /// </summary>
    [HttpPut("{id:int}/priority")]
    public IActionResult UpdatePriority(
        int id,
        UpdatePriorityDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Priority))
        {
            return BadRequest("Priority is required.");
        }

        var priority = request.Priority.Trim();

        if (!InterventionRules.Priorities.Contains(priority))
        {
            return BadRequest(
                "Invalid priority. Allowed values: Low, Medium, High, Critical."
            );
        }

        if (!_service.UpdatePriority(id, priority))
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Priority updated successfully."
        });
    }

    /// <summary>
    /// Updates the operational status of an intervention (MNT-07).
    /// </summary>
    [HttpPut("{id:int}/status")]
    public IActionResult UpdateStatus(
        int id,
        UpdateStatusDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest("Status is required.");
        }

        var status = request.Status.Trim();

        if (!InterventionRules.Statuses.Contains(status))
        {
            return BadRequest(
                "Invalid status."
            );
        }

        if (!_service.UpdateStatus(id, status))
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Status updated successfully."
        });
    }

    /// <summary>
    /// Schedules an intervention (MNT-04).
    ///
    /// Stores planned start and end dates.
    /// The planned end date cannot be earlier than
    /// the planned start date.
    ///
    /// A successfully scheduled intervention is
    /// moved to the Scheduled operational status.
    /// </summary>
    [HttpPut("{id:int}/schedule")]
    public IActionResult ScheduleIntervention(
        int id,
        ScheduleInterventionDto request)
    {
        if (request.PlannedStartDate == default)
        {
            return BadRequest(
                "Planned start date is required."
            );
        }

        if (request.PlannedEndDate == default)
        {
            return BadRequest(
                "Planned end date is required."
            );
        }

        if (request.PlannedEndDate < request.PlannedStartDate)
        {
            return BadRequest(
                "Planned end date cannot be before planned start date."
            );
        }

        bool updated = _service.ScheduleIntervention(
            id,
            request.PlannedStartDate,
            request.PlannedEndDate
        );

        if (!updated)
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Intervention scheduled successfully."
        });
    }

    /// <summary>
    /// Assigns a technician to an intervention (MNT-05).
    ///
    /// In the final enterprise system, technician information
    /// can come from the Staff service. Mock/sample technician
    /// data may be used during development.
    /// </summary>
    [HttpPut("{id:int}/assign")]
    public IActionResult AssignTechnician(
        int id,
        AssignTechnicianDto request)
    {
        if (request.TechnicianId < InterventionRules.MinimumTechnicianId)
        {
            return BadRequest(
                "A valid technician ID is required."
            );
        }

        if (string.IsNullOrWhiteSpace(request.TechnicianName))
        {
            return BadRequest(
                "Technician name is required."
            );
        }

        bool updated = _service.AssignTechnician(
            id,
            request.TechnicianId,
            request.TechnicianName.Trim()
        );

        if (!updated)
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Technician assigned successfully."
        });
    }
}