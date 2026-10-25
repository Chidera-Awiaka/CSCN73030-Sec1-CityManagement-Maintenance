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

    /// <summary>Creates the controller.</summary>
    public InterventionsController(InterventionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lists interventions, optionally filtered (MNT-02).
    /// Example: GET /api/interventions?priority=High&amp;location=King&amp;status=To%20Do
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
        return Ok(_service.Filter(priority, location, status));
    }

    /// <summary>Gets one intervention by ID.</summary>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var intervention = _service.GetById(id);
        return intervention is null ? NotFound("Intervention not found.") : Ok(intervention);
    }

    /// <summary>
    /// Creates an intervention (MNT-01).
    /// Returns 201 with the new intervention, or 400 with the list of validation errors.
    /// </summary>
    [HttpPost]
    public IActionResult Create(CreateInterventionDto request)
    {
        var errors = InterventionService.Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var created = _service.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates the priority of an intervention (MNT-03).</summary>
    [HttpPut("{id}/priority")]
    public IActionResult UpdatePriority(int id, UpdatePriorityDto request)
    {
        if (!InterventionRules.Priorities.Contains(request.Priority))
        {
            return BadRequest("Invalid priority.");
        }

        if (!_service.UpdatePriority(id, request.Priority))
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new { message = "Priority updated successfully." });
    }

    /// <summary>Updates the operational status of an intervention (MNT-07).</summary>
    [HttpPut("{id}/status")]
    public IActionResult UpdateStatus(int id, UpdateStatusDto request)
    {
        if (!InterventionRules.Statuses.Contains(request.Status))
        {
            return BadRequest("Invalid status.");
        }

        if (!_service.UpdateStatus(id, request.Status))
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new { message = "Status updated successfully." });
    }
}
