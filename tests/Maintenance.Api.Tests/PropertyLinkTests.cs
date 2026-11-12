using Maintenance.Api.DTOs;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Tests;

/// <summary>
/// Unit tests for linking properties to interventions (REG-02).
/// Test cases map to TC-REG-02 in the Release Note.
/// </summary>
public sealed class PropertyLinkTests
{
    private static Intervention NewIntervention(InterventionService service) =>
        service.Create(new CreateInterventionDto
        {
            Description = "Leaking hydrant",
            Location = "King Street North",
            IssueType = "Water"
        });

    // ---------- REG-02: link / unlink properties ----------

    /// <summary>Test: Link Property : Keeps External Property Id.</summary>
[Fact]
    public void LinkProperty_KeepsExternalPropertyId()
    {
        var service = new InterventionService();
        var intervention = NewIntervention(service);

        var result = service.LinkProperty(intervention.Id, "PR-1001");

        Assert.Equal(PropertyLinkResult.Success, result);
        Assert.Equal(new[] { "PR-1001" }, service.GetById(intervention.Id)!.LinkedPropertyIds);
    }

    /// <summary>Test: Link Property : Supports More Than One Property.</summary>
[Fact]
    public void LinkProperty_SupportsMoreThanOneProperty()
    {
        var service = new InterventionService();
        var intervention = NewIntervention(service);

        service.LinkProperty(intervention.Id, "PR-1001");
        service.LinkProperty(intervention.Id, "PR-1002");

        Assert.Equal(2, service.GetById(intervention.Id)!.LinkedPropertyIds.Count);
    }

    /// <summary>Test: Link Property : Same Property Twice : Returns Already Linked.</summary>
[Fact]
    public void LinkProperty_SamePropertyTwice_ReturnsAlreadyLinked()
    {
        var service = new InterventionService();
        var intervention = NewIntervention(service);
        service.LinkProperty(intervention.Id, "PR-1001");

        var result = service.LinkProperty(intervention.Id, "pr-1001");

        Assert.Equal(PropertyLinkResult.AlreadyLinked, result);
        Assert.Single(service.GetById(intervention.Id)!.LinkedPropertyIds);
    }

    /// <summary>Test: Link Property : Unknown Intervention : Returns Not Found.</summary>
[Fact]
    public void LinkProperty_UnknownIntervention_ReturnsNotFound()
    {
        var service = new InterventionService();

        Assert.Equal(PropertyLinkResult.InterventionNotFound, service.LinkProperty(999, "PR-1001"));
    }

    /// <summary>Test: Unlink Property : Removes The Link.</summary>
[Fact]
    public void UnlinkProperty_RemovesTheLink()
    {
        var service = new InterventionService();
        var intervention = NewIntervention(service);
        service.LinkProperty(intervention.Id, "PR-1001");

        var result = service.UnlinkProperty(intervention.Id, "PR-1001");

        Assert.Equal(PropertyLinkResult.Success, result);
        Assert.Empty(service.GetById(intervention.Id)!.LinkedPropertyIds);
    }

    /// <summary>Test: Unlink Property : Not Linked : Returns Not Linked.</summary>
[Fact]
    public void UnlinkProperty_NotLinked_ReturnsNotLinked()
    {
        var service = new InterventionService();
        var intervention = NewIntervention(service);

        Assert.Equal(PropertyLinkResult.NotLinked, service.UnlinkProperty(intervention.Id, "PR-1001"));
        Assert.Equal(PropertyLinkResult.InterventionNotFound, service.UnlinkProperty(999, "PR-1001"));
    }

    /// <summary>Test: Link Property : Is Saved And Reloaded From File.</summary>
[Fact]
    public void LinkProperty_IsSavedAndReloadedFromFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"interventions-{Guid.NewGuid()}.json");
        try
        {
            var service = new InterventionService(file);
            var intervention = NewIntervention(service);
            service.LinkProperty(intervention.Id, "PR-1004");

            var reloaded = new InterventionService(file);

            Assert.Contains("PR-1004", reloaded.GetById(intervention.Id)!.LinkedPropertyIds);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
