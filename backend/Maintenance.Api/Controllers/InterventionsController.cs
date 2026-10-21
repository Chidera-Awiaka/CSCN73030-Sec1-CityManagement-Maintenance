using Microsoft.AspNetCore.Mvc;
using Maintenance.Api.DTOs;
using Maintenance.Api.Services;

namespace Maintenance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterventionsController : ControllerBase
{
    private readonly InterventionService _service;

    public InterventionsController(InterventionService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetAll());
    }

    [HttpPut("{id}/priority")]
    public IActionResult UpdatePriority(
        int id,
        UpdatePriorityDto request)
    {
        string[] allowedPriorities =
        {
            "Low",
            "Medium",
            "High",
            "Critical"
        };

        if (!allowedPriorities.Contains(request.Priority))
        {
            return BadRequest("Invalid priority.");
        }

        bool updated =
            _service.UpdatePriority(id, request.Priority);

        if (!updated)
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Priority updated successfully."
        });
    }

    [HttpPut("{id}/status")]
    public IActionResult UpdateStatus(
        int id,
        UpdateStatusDto request)
    {
        string[] allowedStatuses =
        {
            "To Do",
            "Scheduled",
            "In Progress",
            "On Hold",
            "Completed",
            "Cancelled"
        };

        if (!allowedStatuses.Contains(request.Status))
        {
            return BadRequest("Invalid status.");
        }

        bool updated =
            _service.UpdateStatus(id, request.Status);

        if (!updated)
        {
            return NotFound("Intervention not found.");
        }

        return Ok(new
        {
            message = "Status updated successfully."
        });
    }
}