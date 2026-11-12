using Maintenance.Api.Controllers;
using Maintenance.Api.DTOs;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Maintenance.Api.Tests;

/// <summary>
/// Endpoint-level tests for REG-01 (GET /api/properties) and
/// REG-02 (/api/interventions/{id}/properties).
/// Test cases map to TC-REG-01 and TC-REG-02 in the Release Note.
/// </summary>
public sealed class PropertyEndpointTests
{
    private readonly InterventionService _service = new();
    private readonly MockPropertyRegistry _registry = new();

    private PropertiesController Properties() => new(_registry);

    private InterventionsController Interventions() => new(_service, _registry);

    private static int? StatusOf(IActionResult result) =>
        (result as IStatusCodeActionResult)?.StatusCode;

    // ---------- REG-01 ----------

    /// <summary>Test: search by Property ID returns 200 with ID and address.</summary>
    [Fact]
    public void Search_ById_Returns200WithIdAndAddress()
    {
        var result = Assert.IsType<OkObjectResult>(Properties().Search("PR-1002", null));

        var property = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Property>>(result.Value));
        Assert.Equal("PR-1002", property.PropertyId);
        Assert.False(string.IsNullOrEmpty(property.Address));
    }

    /// <summary>Test: search by address returns every matching property.</summary>
    [Fact]
    public void Search_ByAddress_Returns200WithMatches()
    {
        var result = Assert.IsType<OkObjectResult>(Properties().Search(null, "King"));

        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyList<Property>>(result.Value).Count);
    }

    /// <summary>Test: no match returns 404 with a clear message.</summary>
    [Fact]
    public void Search_NoMatch_Returns404()
    {
        Assert.Equal(404, StatusOf(Properties().Search(null, "Nowhere Road")));
    }

    /// <summary>Test: an empty search returns 400.</summary>
    [Fact]
    public void Search_NoSearchTerm_Returns400()
    {
        Assert.Equal(400, StatusOf(Properties().Search(" ", null)));
    }

    /// <summary>Test: registry unavailable returns 503.</summary>
    [Fact]
    public void Search_RegistryDown_Returns503()
    {
        _registry.IsAvailable = false;

        Assert.Equal(503, StatusOf(Properties().Search("PR-1001", null)));
    }

    // ---------- REG-02 ----------

    /// <summary>Test: a linked property is visible in the intervention details.</summary>
    [Fact]
    public void Link_ThenVisibleInDetails()
    {
        var controller = Interventions();

        Assert.Equal(200, StatusOf(controller.LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-1003" })));

        var details = Assert.IsType<OkObjectResult>(controller.GetLinkedProperties(1));
        var properties = Assert.IsType<List<Property>>(details.Value);
        Assert.Contains(properties, p => p.PropertyId == "PR-1003" && p.Address.Contains("Victoria"));
    }

    /// <summary>Test: linking a property that is not in the registry returns 404.</summary>
    [Fact]
    public void Link_UnknownProperty_Returns404()
    {
        Assert.Equal(404, StatusOf(Interventions().LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-9999" })));
    }

    /// <summary>Test: linking to an intervention that does not exist returns 404.</summary>
    [Fact]
    public void Link_UnknownIntervention_Returns404()
    {
        Assert.Equal(404, StatusOf(Interventions().LinkProperty(999, new LinkPropertyDto { PropertyId = "PR-1001" })));
    }

    /// <summary>Test: linking the same property twice returns 409.</summary>
    [Fact]
    public void Link_Twice_Returns409()
    {
        var controller = Interventions();
        controller.LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-1004" });

        Assert.Equal(409, StatusOf(controller.LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-1004" })));
    }

    /// <summary>Test: linking with the registry down returns 503.</summary>
    [Fact]
    public void Link_RegistryDown_Returns503()
    {
        _registry.IsAvailable = false;

        Assert.Equal(503, StatusOf(Interventions().LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-1001" })));
    }

    /// <summary>Test: unlink removes the link, a second unlink returns 404.</summary>
    [Fact]
    public void Unlink_RemovesLink()
    {
        var controller = Interventions();
        controller.LinkProperty(1, new LinkPropertyDto { PropertyId = "PR-1001" });

        Assert.Equal(200, StatusOf(controller.UnlinkProperty(1, "PR-1001")));
        Assert.Equal(404, StatusOf(controller.UnlinkProperty(1, "PR-1001")));
    }
}
