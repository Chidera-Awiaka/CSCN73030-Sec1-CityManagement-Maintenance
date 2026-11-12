using Maintenance.Api.Integration;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>
/// REST endpoints for searching the Property Registry (REG-01).
/// Base route: /api/properties
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyRegistry _registry;

    /// <summary>Creates the controller.</summary>
    /// <param name="registry">Property Registry integration.</param>
    public PropertiesController(IPropertyRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Searches properties by Property ID and/or address (REG-01).
    /// Example: GET /api/properties?propertyId=PR-1001 or GET /api/properties?address=King
    /// </summary>
    /// <param name="propertyId">Exact Property ID (case-insensitive).</param>
    /// <param name="address">Part of the address (case-insensitive).</param>
    /// <returns>
    /// 200 with the matching properties; 400 when no search term is given;
    /// 404 when nothing matches; 503 when the Property Registry is unavailable.
    /// </returns>
    [HttpGet]
    public IActionResult Search([FromQuery] string? propertyId, [FromQuery] string? address)
    {
        if (string.IsNullOrWhiteSpace(propertyId) && string.IsNullOrWhiteSpace(address))
        {
            return BadRequest(new { message = "Enter a Property ID or an address to search." });
        }

        try
        {
            var results = _registry.Search(propertyId, address);
            if (results.Count == 0)
            {
                return NotFound(new { message = "No property found matching the search." });
            }

            return Ok(results);
        }
        catch (PropertyRegistryUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}
