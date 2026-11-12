using Microsoft.AspNetCore.Mvc;
using Maintenance.Api.DTOs;
using Maintenance.Api.Integration;
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
    private readonly IPropertyRegistry _registry;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Intervention business logic.</param>
    /// <param name="registry">Property Registry integration (REG-01 / REG-02).</param>
    public InterventionsController(InterventionService service, IPropertyRegistry registry)
    {
        _service = service;
        _registry = registry;
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
    /// <param name="technicianId">Optional assigned technician ID (MNT-02 fix, GitHub #2).</param>
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? priority,
        [FromQuery] string? location,
        [FromQuery] string? status,
        [FromQuery] int? technicianId)
    {
        return Ok(
            _service.Filter(
                priority,
                location,
                status,
                technicianId
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

    /// <summary>
    /// Lists the full details of the properties linked to an intervention (REG-02).
    /// Details are read from the Property Registry using the stored Property IDs.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>200 with the property details; 404; 503 when the registry is unavailable.</returns>
    [HttpGet("{id:int}/properties")]
    public IActionResult GetLinkedProperties(int id)
    {
        var intervention = _service.GetById(id);
        if (intervention is null)
        {
            return NotFound(new { message = "Intervention not found." });
        }

        try
        {
            var properties = intervention.LinkedPropertyIds
                .Select(pid => _registry.GetById(pid)
                    ?? new Property { PropertyId = pid, Address = "(not found in registry)" })
                .ToList();
            return Ok(properties);
        }
        catch (PropertyRegistryUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = ex.Message, linkedPropertyIds = intervention.LinkedPropertyIds });
        }
    }

    /// <summary>
    /// Links a property from the Property Registry to an intervention (REG-02).
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">The Property ID to link.</param>
    /// <returns>
    /// 200 when linked; 400 when no Property ID is given; 404 when the intervention or
    /// property does not exist; 409 when already linked; 503 when the registry is unavailable.
    /// </returns>
    [HttpPost("{id:int}/properties")]
    public IActionResult LinkProperty(int id, LinkPropertyDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PropertyId))
        {
            return BadRequest(new { message = "Property ID is required." });
        }

        Property? property;
        try
        {
            property = _registry.GetById(request.PropertyId);
        }
        catch (PropertyRegistryUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        if (property is null)
        {
            return NotFound(new { message = $"Property '{request.PropertyId}' was not found in the Property Registry." });
        }

        return _service.LinkProperty(id, property.PropertyId) switch
        {
            PropertyLinkResult.Success => Ok(new { message = $"Property {property.PropertyId} linked to intervention {id}." }),
            PropertyLinkResult.AlreadyLinked => Conflict(new { message = $"Property {property.PropertyId} is already linked." }),
            _ => NotFound(new { message = "Intervention not found." })
        };
    }

    /// <summary>Removes a property link from an intervention (REG-02).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="propertyId">The Property ID to unlink.</param>
    /// <returns>200 when removed; 404 when the intervention or link does not exist.</returns>
    [HttpDelete("{id:int}/properties/{propertyId}")]
    public IActionResult UnlinkProperty(int id, string propertyId)
    {
        return _service.UnlinkProperty(id, propertyId) switch
        {
            PropertyLinkResult.Success => Ok(new { message = $"Property {propertyId} unlinked from intervention {id}." }),
            PropertyLinkResult.NotLinked => NotFound(new { message = $"Property {propertyId} is not linked to this intervention." }),
            _ => NotFound(new { message = "Intervention not found." })
        };
    }

    /// <summary>
    /// Lists the interventions assigned to one technician (MNT-06).
    /// Each intervention includes its location, planned dates, instructions and priority.
    /// Example: GET /api/interventions/assigned/101
    /// </summary>
    /// <param name="technicianId">Technician ID (from MNT-05).</param>
    /// <param name="includeClosed">Also return Completed / Cancelled work. Default false.</param>
    /// <returns>200 with the list (empty when nothing is assigned); 400 for an invalid ID.</returns>
    [HttpGet("assigned/{technicianId:int}")]
    public IActionResult GetMyAssignments(int technicianId, [FromQuery] bool includeClosed = false)
    {
        if (technicianId < InterventionRules.MinimumTechnicianId)
        {
            return BadRequest(new { message = "A valid technician ID is required." });
        }

        return Ok(_service.GetAssignedTo(technicianId, includeClosed));
    }

    /// <summary>
    /// Closes an intervention (MNT-08). A completion report is required; the actual
    /// completion date is recorded (now when omitted); the actual cost can be entered.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Completion report, optional completion date and cost.</param>
    /// <returns>200 when closed; 400 with errors; 404 not found; 409 already Completed or Cancelled.</returns>
    [HttpPut("{id:int}/close")]
    public IActionResult CloseIntervention(int id, CloseInterventionDto request)
    {
        var intervention = _service.GetById(id);
        if (intervention is null)
        {
            return NotFound(new { message = "Intervention not found." });
        }

        var errors = InterventionService.ValidateClose(request, intervention.CreatedAt, DateTime.UtcNow);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        return _service.CloseIntervention(id, request) switch
        {
            CloseResult.Closed => Ok(new { message = $"Intervention {id} closed." }),
            CloseResult.AlreadyClosed => Conflict(new { message = $"Intervention {id} is already {intervention.Status}." }),
            _ => NotFound(new { message = "Intervention not found." })
        };
    }
}
