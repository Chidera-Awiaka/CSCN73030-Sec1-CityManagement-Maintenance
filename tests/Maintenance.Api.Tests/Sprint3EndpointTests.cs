using Maintenance.Api.Controllers;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Maintenance.Api.Tests;

/// <summary>Endpoint-level tests for Sprint 3: HTTP codes, headers, BUD-04, audit and overlap warnings.</summary>
public sealed class Sprint3EndpointTests
{
    private readonly Sprint3TestWorld _w = new();

    private static ControllerContext Ctx(string? user = "mgr-01", string? source = null)
    {
        var http = new DefaultHttpContext();
        if (user is not null) http.Request.Headers[RequestContext.UserHeader] = user;
        if (source is not null) http.Request.Headers[RequestContext.SourceServiceHeader] = source;
        return new ControllerContext { HttpContext = http };
    }

    private static int? Code(IActionResult r) => (r as IStatusCodeActionResult)?.StatusCode;

    private InterventionsController Interventions(bool requireApproval = false) =>
        new(_w.Interventions, new MockPropertyRegistry(), _w.Audit, new BudgetPolicy { RequireApprovalToStart = requireApproval })
        { ControllerContext = Ctx() };

    /// <summary>Test: ESV-03 endpoint 201, duplicate 200, invalid 400; ESV-04/05 endpoints.</summary>
    [Fact]
    public void WeatherEndpoints()
    {
        var c = new WeatherAlertsController(_w.Weather) { ControllerContext = Ctx() };
        var dto = new IncomingWeatherAlertDto { ExternalAlertId = "W1", Title = "Snow squall" };

        Assert.Equal(201, Code(c.Receive(dto)));
        Assert.Equal(200, Code(c.Receive(dto)));
        Assert.Equal(400, Code(c.Receive(new IncomingWeatherAlertDto())));
        Assert.Equal(400, Code(c.GetAll("Unknown")));
        Assert.Equal(200, Code(c.Assess(1, new WeatherAssessmentDto { Decision = "Monitor", Rationale = "Light" })));
        Assert.Equal("mgr-01", _w.Weather.GetById(1)!.Assessments.Single().Reviewer);
        Assert.Equal(200, Code(c.TakeAction(1, new WeatherAlertActionDto { Type = "NoActionRequired", Notes = "Indoor work" })));
        Assert.Equal(404, Code(c.GetById(99)));
    }

    /// <summary>Test: BUD-04 enforced by the status endpoint when enabled (409 with reason); allowed after approval.</summary>
    [Fact]
    public void StatusEndpoint_EnforcesBudgetRule()
    {
        var blocked = Interventions(requireApproval: true).UpdateStatus(1, new UpdateStatusDto { Status = "In Progress" });
        Assert.Equal(409, Code(blocked));
        Assert.Equal("To Do", _w.Interventions.GetById(1)!.Status);

        _w.Budget.Estimate(1, new CostEstimateDto { RepairCost = 100 }, "m");
        var reference = _w.Budget.Submit(1, "m").Value!.BudgetRequestReference!;
        _w.Budget.ReceiveDecision(new BudgetDecisionDto { DecisionId = "D", RequestReference = reference, Decision = "Approved" }, "Budget");

        Assert.Equal(200, Code(Interventions(requireApproval: true).UpdateStatus(1, new UpdateStatusDto { Status = "In Progress" })));
        Assert.Equal(200, Code(Interventions().UpdateStatus(2, new UpdateStatusDto { Status = "In Progress" })));
    }

    /// <summary>Test: TEC-05 status, priority, assignment and schedule changes audited with actor.</summary>
    [Fact]
    public void InterventionChanges_Audited()
    {
        var c = Interventions();
        c.UpdateStatus(1, new UpdateStatusDto { Status = "On Hold" });
        c.UpdatePriority(1, new UpdatePriorityDto { Priority = "Critical" });
        c.AssignTechnician(1, new AssignTechnicianDto { TechnicianId = 101, TechnicianName = "Alex Technician" });
        c.ScheduleIntervention(1, new ScheduleInterventionDto { PlannedStartDate = new DateTime(2026, 12, 1), PlannedEndDate = new DateTime(2026, 12, 2) });

        var actions = _w.Audit.Query("Intervention", "1").Select(e => e.Action).ToList();
        Assert.Contains(AuditActions.StatusChanged, actions);
        Assert.Contains(AuditActions.PriorityChanged, actions);
        Assert.Contains(AuditActions.TechnicianAssigned, actions);
        Assert.Contains(AuditActions.Scheduled, actions);
        Assert.All(_w.Audit.Query("Intervention", "1"), e => Assert.Equal("mgr-01", e.Actor));
        Assert.Contains(_w.Audit.Query("Intervention", "1"), e => e.Details == "To Do -> On Hold");
    }

    /// <summary>Test: STF-02 overlap warning returned when scheduling.</summary>
    [Fact]
    public void Schedule_ReturnsOverlapWarning()
    {
        var c = Interventions();
        var start = new DateTime(2026, 12, 1, 8, 0, 0);
        c.AssignTechnician(1, new AssignTechnicianDto { TechnicianId = 101, TechnicianName = "Alex" });
        c.AssignTechnician(2, new AssignTechnicianDto { TechnicianId = 101, TechnicianName = "Alex" });
        c.ScheduleIntervention(1, new ScheduleInterventionDto { PlannedStartDate = start, PlannedEndDate = start.AddHours(8) });

        var ok = Assert.IsType<OkObjectResult>(c.ScheduleIntervention(2, new ScheduleInterventionDto { PlannedStartDate = start.AddHours(2), PlannedEndDate = start.AddHours(4) }));
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        Assert.Contains("Overlaps intervention #1", json);
        Assert.Contains("Maintenance only", json);
    }

    /// <summary>Test: inbound endpoints reject Maintenance users (403) and accept the right service.</summary>
    [Fact]
    public void InboundEndpoints_RequireSourceService()
    {
        _w.Budget.Estimate(1, new CostEstimateDto { RepairCost = 100 }, "m");
        var reference = _w.Budget.Submit(1, "m").Value!.BudgetRequestReference!;
        var decision = new BudgetDecisionDto { DecisionId = "D", RequestReference = reference, Decision = "Approved" };

        var asUser = new BudgetController(_w.Budget, new BudgetPolicy()) { ControllerContext = Ctx() };
        var asBudget = new BudgetController(_w.Budget, new BudgetPolicy()) { ControllerContext = Ctx(null, "Budget") };
        Assert.Equal(403, Code(asUser.ReceiveDecision(decision)));
        Assert.Equal(200, Code(asBudget.ReceiveDecision(decision)));

        var security = new SecurityController(_w.Security) { ControllerContext = Ctx() };
        var request = _w.Security.Request(1, new SecurityRequestDto { Reason = "x", PeriodStart = DateTime.UtcNow, PeriodEnd = DateTime.UtcNow.AddHours(1) }, "m").Value!;
        Assert.Equal(403, Code(security.Respond(new SecurityResponseDto { ResponseId = "R", RequestReference = request.SecurityReference!, Outcome = "Approved" })));
    }

    /// <summary>Test: budget submit returns 502 when Budget is down; exchange retry endpoint fixes it.</summary>
    [Fact]
    public void BudgetDown_502ThenRetryEndpoint()
    {
        var budget = new BudgetController(_w.Budget, new BudgetPolicy()) { ControllerContext = Ctx() };
        var ops = new OperationsController(_w.Exchanges, _w.Audit, _w.Hub, new MockStaffDirectory(), new MockTransitService(), new MockPropertyRegistry());
        budget.Estimate(1, new CostEstimateDto { RepairCost = 500, MaterialCost = 100 });
        ops.SetAvailability("Budget", new MockAvailabilityDto { Available = false });

        Assert.Equal(502, Code(budget.Submit(1)));

        ops.SetAvailability("Budget", new MockAvailabilityDto { Available = true });
        var failed = _w.Exchanges.Query(status: "Failed").Single();
        Assert.Equal(200, Code(ops.Retry(failed.Id)));
        Assert.Equal(BudgetRules.Pending, _w.Interventions.GetById(1)!.BudgetStatus);
        Assert.Equal(404, Code(ops.SetAvailability("Nope", new MockAvailabilityDto())));
    }

    /// <summary>Test: Staff 503 when unavailable; transit 503; utilities 404/200.</summary>
    [Fact]
    public void LookupEndpoints()
    {
        var staffDir = new MockStaffDirectory();
        var staff = new StaffController(staffDir, _w.Interventions);
        Assert.Equal(200, Code(staff.Personnel("Water")));
        staffDir.IsAvailable = false;
        Assert.Equal(503, Code(staff.Personnel(null)));
        Assert.Equal(404, Code(staff.Conflicts(999)));

        var transitSvc = new MockTransitService();
        var transit = new TransitController(transitSvc, _w.Diversions) { ControllerContext = Ctx(null, "Public Transport") };
        Assert.Equal(200, Code(transit.Routes("Erb")));
        transitSvc.IsAvailable = false;
        Assert.Equal(503, Code(transit.Routes(null)));

        var props = new PropertiesController(new MockPropertyRegistry());
        Assert.Equal(200, Code(props.Utilities("PR-1002")));
        Assert.Equal(404, Code(props.Utilities("PR-9999")));
    }

    /// <summary>Test: notice endpoints return 200 with delivery results, 404 for unknown intervention.</summary>
    [Fact]
    public void NoticeEndpoints()
    {
        var c = new NoticesController(_w.Notices) { ControllerContext = Ctx() };

        Assert.Equal(200, Code(c.RoadClosure(1, new RoadClosureDto { ClosureStatus = "Closed", PlannedStart = DateTime.UtcNow })));
        Assert.Equal(404, Code(c.WorkStatus(999, null)));
        Assert.Equal(400, Code(c.PropertyDisruption(1, new PropertyDisruptionDto())));
        Assert.Single(_w.Notices.GetAll(1));
    }

    /// <summary>Test: TEC-05 closing records the real previous status.</summary>
    [Fact]
    public void Close_AuditsPreviousStatus()
    {
        Interventions().CloseIntervention(1, new CloseInterventionDto { CompletionReport = "done" });

        Assert.Contains(_w.Audit.Query("Intervention", "1"), e => e.Action == AuditActions.StatusChanged && e.Details == "To Do -> Completed");
    }
}
