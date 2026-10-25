using Maintenance.Api.DTOs;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Tests;

/// <summary>
/// Unit tests for intervention creation (MNT-01) and filtering (MNT-02).
/// Test cases map to TC-01 and TC-02 in the Maintenance SRS.
/// </summary>
public sealed class InterventionServiceTests
{
    private static CreateInterventionDto ValidRequest() => new()
    {
        Description = "Broken streetlight",
        Location = "Erb Street",
        IssueType = "Electrical"
    };

    // ---------- MNT-01: Create an intervention (TC-01) ----------

    [Fact]
    public void Create_ValidRequest_AssignsUniqueIdAndToDoStatus()
    {
        var service = new InterventionService();

        var first = service.Create(ValidRequest());
        var second = service.Create(ValidRequest());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("To Do", first.Status);
        Assert.Equal("To Do", second.Status);
    }

    [Fact]
    public void Create_ValidRequest_IsAddedToList()
    {
        var service = new InterventionService();
        int before = service.GetAll().Count;

        var created = service.Create(ValidRequest());

        Assert.Equal(before + 1, service.GetAll().Count);
        Assert.NotNull(service.GetById(created.Id));
    }

    [Fact]
    public void Create_NoPriority_DefaultsToMedium()
    {
        var service = new InterventionService();

        var created = service.Create(ValidRequest());

        Assert.Equal("Medium", created.Priority);
    }

    [Theory]
    [InlineData("", "Erb Street", "Road", "Description is required.")]
    [InlineData("Pothole", "  ", "Road", "Location is required.")]
    [InlineData("Pothole", "Erb Street", "", "Issue type is required.")]
    public void Validate_MissingRequiredField_ReturnsError(
        string description, string location, string issueType, string expectedError)
    {
        var request = new CreateInterventionDto
        {
            Description = description,
            Location = location,
            IssueType = issueType
        };

        var errors = InterventionService.Validate(request);

        Assert.Contains(expectedError, errors);
    }

    [Fact]
    public void Validate_InvalidPriority_ReturnsError()
    {
        var request = ValidRequest();
        request.Priority = "Urgent";

        var errors = InterventionService.Validate(request);

        Assert.Single(errors);
    }

    [Fact]
    public void Create_InvalidRequest_ThrowsAndAddsNothing()
    {
        var service = new InterventionService();
        int before = service.GetAll().Count;

        Assert.Throws<ArgumentException>(() =>
            service.Create(new CreateInterventionDto()));

        Assert.Equal(before, service.GetAll().Count);
    }

    [Fact]
    public void Create_IsSavedAndReloadedFromFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"interventions-{Guid.NewGuid()}.json");
        try
        {
            var created = new InterventionService(path).Create(ValidRequest());

            var reloaded = new InterventionService(path);

            Assert.NotNull(reloaded.GetById(created.Id));
            Assert.Equal("Broken streetlight", reloaded.GetById(created.Id)!.Description);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- MNT-02: View and filter interventions (TC-02) ----------

    private static InterventionService ServiceWithData()
    {
        var service = new InterventionService();
        service.Create(new CreateInterventionDto
            { Description = "Pothole", Location = "King Street North", IssueType = "Road", Priority = "Critical" });
        service.Create(new CreateInterventionDto
            { Description = "Leak", Location = "University Ave", IssueType = "Water", Priority = "Low" });
        return service;
    }

    [Fact]
    public void Filter_NoFilters_ReturnsAll()
    {
        var service = ServiceWithData();

        Assert.Equal(service.GetAll().Count, service.Filter(null, null, null).Count);
    }

    [Fact]
    public void Filter_ByPriority_ReturnsOnlyMatching()
    {
        var result = ServiceWithData().Filter("critical", null, null);

        Assert.NotEmpty(result);
        Assert.All(result, x => Assert.Equal("Critical", x.Priority));
    }

    [Fact]
    public void Filter_ByLocationText_ReturnsOnlyMatching()
    {
        var result = ServiceWithData().Filter(null, "king", null);

        Assert.NotEmpty(result);
        Assert.All(result, x => Assert.Contains("King", x.Location));
    }

    [Fact]
    public void Filter_ByStatus_ReturnsOnlyMatching()
    {
        var service = ServiceWithData();
        var target = service.GetAll().First();
        service.UpdateStatus(target.Id, "On Hold");

        var result = service.Filter(null, null, "On Hold");

        Assert.Single(result);
        Assert.Equal(target.Id, result[0].Id);
    }

    [Fact]
    public void Filter_CombinedFilters_ReturnsOnlyMatchingAll()
    {
        var result = ServiceWithData().Filter("Critical", "King", "To Do");

        Assert.Single(result);
        Assert.Equal("Pothole", result[0].Description);
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmptyList()
    {
        var result = ServiceWithData().Filter(null, "Nowhere Road", null);

        Assert.Empty(result);
    }

    [Fact]
    public void Filter_SortsByPriorityHighestFirst()
    {
        var result = ServiceWithData().Filter(null, null, null);

        var ranks = result.Select(x => Array.IndexOf(InterventionRules.Priorities, x.Priority)).ToList();
        Assert.Equal(ranks.OrderByDescending(r => r).ToList(), ranks);
    }
}
