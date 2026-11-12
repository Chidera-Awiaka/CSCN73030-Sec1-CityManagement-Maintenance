using Maintenance.Api.Controllers;
using Maintenance.Api.DTOs;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Maintenance.Api.Tests;

/// <summary>
/// Tests for MNT-06 (view my assigned interventions) and MNT-08 (close an intervention).
/// Test cases map to TC-MNT-06 and TC-MNT-08 in the Release Note.
/// </summary>
public sealed class AssignmentAndCloseTests
{
    private readonly InterventionService _service = new();

    private InterventionsController Controller() => new(_service, new MockPropertyRegistry());

    private static int? StatusOf(IActionResult result) =>
        (result as IStatusCodeActionResult)?.StatusCode;

    private Intervention New(string description, string priority = "Medium", string? instructions = null) =>
        _service.Create(new CreateInterventionDto
        {
            Description = description,
            Location = "Erb Street West",
            IssueType = "Road",
            Priority = priority,
            Instructions = instructions
        });

    // ---------- MNT-06 ----------

    /// <summary>Test: only the technician's own assignments are returned.</summary>
    [Fact]
    public void GetAssignedTo_ReturnsOnlyMyAssignments()
    {
        var mine = New("Pothole");
        var theirs = New("Streetlight");
        _service.AssignTechnician(mine.Id, 101, "Alex Technician");
        _service.AssignTechnician(theirs.Id, 102, "Sam Technician");

        var result = _service.GetAssignedTo(101);

        Assert.Equal(mine.Id, Assert.Single(result).Id);
    }

    /// <summary>Test: each assignment carries location, dates, instructions and priority.</summary>
    [Fact]
    public void GetAssignedTo_IncludesLocationDatesInstructionsPriority()
    {
        var item = New("Pothole", "High", "  Bring cold patch and cones  ");
        _service.AssignTechnician(item.Id, 101, "Alex Technician");
        _service.ScheduleIntervention(item.Id, new DateTime(2026, 11, 15, 9, 0, 0), new DateTime(2026, 11, 15, 17, 0, 0));

        var mine = Assert.Single(_service.GetAssignedTo(101));

        Assert.Equal("Erb Street West", mine.Location);
        Assert.Equal("High", mine.Priority);
        Assert.Equal("Bring cold patch and cones", mine.Instructions);
        Assert.Equal(new DateTime(2026, 11, 15, 9, 0, 0), mine.PlannedStartDate);
        Assert.Equal(new DateTime(2026, 11, 15, 17, 0, 0), mine.PlannedEndDate);
    }

    /// <summary>Test: earliest planned start first, unscheduled work last.</summary>
    [Fact]
    public void GetAssignedTo_OrderedByPlannedStart()
    {
        var later = New("Later");
        var unscheduled = New("Unscheduled", "Critical");
        var sooner = New("Sooner");
        foreach (var i in new[] { later, unscheduled, sooner }) _service.AssignTechnician(i.Id, 101, "Alex Technician");
        _service.ScheduleIntervention(later.Id, new DateTime(2026, 11, 20), new DateTime(2026, 11, 21));
        _service.ScheduleIntervention(sooner.Id, new DateTime(2026, 11, 14), new DateTime(2026, 11, 14));

        var ids = _service.GetAssignedTo(101).Select(x => x.Id).ToList();

        Assert.Equal(new List<int> { sooner.Id, later.Id, unscheduled.Id }, ids);
    }

    /// <summary>Test: closed work is hidden unless includeClosed is true.</summary>
    [Fact]
    public void GetAssignedTo_HidesClosedUnlessRequested()
    {
        var item = New("Pothole");
        _service.AssignTechnician(item.Id, 101, "Alex Technician");
        _service.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Patched" });

        Assert.Empty(_service.GetAssignedTo(101));
        Assert.Single(_service.GetAssignedTo(101, includeClosed: true));
    }

    /// <summary>Test: endpoint returns 200 with an empty list when nothing is assigned.</summary>
    [Fact]
    public void Endpoint_NoAssignments_Returns200Empty()
    {
        var ok = Assert.IsType<OkObjectResult>(Controller().GetMyAssignments(999));

        Assert.Empty(Assert.IsType<List<Intervention>>(ok.Value));
    }

    /// <summary>Test: endpoint rejects an invalid technician ID with 400.</summary>
    [Fact]
    public void Endpoint_InvalidTechnicianId_Returns400()
    {
        Assert.Equal(400, StatusOf(Controller().GetMyAssignments(0)));
    }

    // ---------- MNT-08 ----------

    /// <summary>Test: closing records report, date, cost and sets status Completed.</summary>
    [Fact]
    public void Close_RecordsReportDateCostAndCompletes()
    {
        var item = New("Pothole");
        var done = DateTime.UtcNow.AddMinutes(-10);

        var result = _service.CloseIntervention(item.Id, new CloseInterventionDto
        {
            CompletionReport = "  Filled pothole with cold patch  ",
            ActualCompletionDate = done,
            ActualCost = 245.50m
        });

        var closed = _service.GetById(item.Id)!;
        Assert.Equal(CloseResult.Closed, result);
        Assert.Equal("Completed", closed.Status);
        Assert.Equal("Filled pothole with cold patch", closed.CompletionReport);
        Assert.Equal(done, closed.ActualCompletionDate);
        Assert.Equal(245.50m, closed.ActualCost);
    }

    /// <summary>Test: completion date defaults to now when omitted.</summary>
    [Fact]
    public void Close_NoDate_DefaultsToNow()
    {
        var item = New("Pothole");
        var before = DateTime.UtcNow;

        _service.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Done" });

        var date = _service.GetById(item.Id)!.ActualCompletionDate!.Value;
        Assert.True(date >= before && date <= DateTime.UtcNow);
    }

    /// <summary>Test: completion report is required.</summary>
    [Fact]
    public void ValidateClose_MissingReport_Error()
    {
        var errors = InterventionService.ValidateClose(new CloseInterventionDto { CompletionReport = "  " }, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.Contains("A completion report is required.", errors);
    }

    /// <summary>Test: completion date cannot be in the future or before creation; cost cannot be negative.</summary>
    [Fact]
    public void ValidateClose_BadDatesAndNegativeCost_Errors()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var created = now.AddDays(-2);

        var future = InterventionService.ValidateClose(new CloseInterventionDto { CompletionReport = "x", ActualCompletionDate = now.AddDays(1) }, created, now);
        var early = InterventionService.ValidateClose(new CloseInterventionDto { CompletionReport = "x", ActualCompletionDate = created.AddDays(-1) }, created, now);
        var negative = InterventionService.ValidateClose(new CloseInterventionDto { CompletionReport = "x", ActualCost = -1 }, created, now);
        var valid = InterventionService.ValidateClose(new CloseInterventionDto { CompletionReport = "x", ActualCompletionDate = now.AddHours(-1), ActualCost = 0 }, created, now);

        Assert.Contains("Actual completion date cannot be in the future.", future);
        Assert.Contains("Actual completion date cannot be before the intervention was created.", early);
        Assert.Contains("Actual cost cannot be negative.", negative);
        Assert.Empty(valid);
    }

    /// <summary>Test: an already closed or cancelled intervention cannot be closed again.</summary>
    [Fact]
    public void Close_AlreadyClosedOrCancelled_ReturnsAlreadyClosed()
    {
        var item = New("Pothole");
        var cancelled = New("Duplicate report");
        _service.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Done" });
        _service.UpdateStatus(cancelled.Id, "Cancelled");

        Assert.Equal(CloseResult.AlreadyClosed, _service.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Again" }));
        Assert.Equal(CloseResult.AlreadyClosed, _service.CloseIntervention(cancelled.Id, new CloseInterventionDto { CompletionReport = "x" }));
        Assert.Equal(CloseResult.NotFound, _service.CloseIntervention(999, new CloseInterventionDto { CompletionReport = "x" }));
    }

    /// <summary>Test: close endpoint returns 200, 400, 404 and 409.</summary>
    [Fact]
    public void Endpoint_Close_StatusCodes()
    {
        var controller = Controller();
        var item = New("Pothole");

        Assert.Equal(400, StatusOf(controller.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "" })));
        Assert.Equal(404, StatusOf(controller.CloseIntervention(999, new CloseInterventionDto { CompletionReport = "Done" })));
        Assert.Equal(200, StatusOf(controller.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Done", ActualCost = 80 })));
        Assert.Equal(409, StatusOf(controller.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Again" })));
    }

    /// <summary>Test: instructions are saved to file and reloaded.</summary>
    [Fact]
    public void InstructionsAndClose_SavedAndReloaded()
    {
        var file = Path.Combine(Path.GetTempPath(), $"interventions-{Guid.NewGuid()}.json");
        try
        {
            var service = new InterventionService(file);
            var item = service.Create(new CreateInterventionDto { Description = "Leak", Location = "King St", IssueType = "Water", Instructions = "Shut valve first" });
            service.CloseIntervention(item.Id, new CloseInterventionDto { CompletionReport = "Valve replaced", ActualCost = 120 });

            var reloaded = new InterventionService(file).GetById(item.Id)!;

            Assert.Equal("Shut valve first", reloaded.Instructions);
            Assert.Equal("Valve replaced", reloaded.CompletionReport);
            Assert.Equal(120m, reloaded.ActualCost);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
