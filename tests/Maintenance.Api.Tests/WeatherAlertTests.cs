using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Models;

namespace Maintenance.Api.Tests;

/// <summary>ESV-03 receive weather alerts, ESV-04 assessment, ESV-05 actions.</summary>
public sealed class WeatherAlertTests
{
    private readonly Sprint3TestWorld _w = new();

    private static IncomingWeatherAlertDto Alert(string id = "EC-2026-0042") => new()
    {
        ExternalAlertId = id,
        Title = "Freezing rain warning",
        Content = "Up to 10 mm of ice accretion expected.",
        AffectedArea = "Waterloo - Uptown",
        Severity = "severe",
        ValidFrom = new DateTime(2026, 11, 26, 6, 0, 0, DateTimeKind.Utc),
        ValidTo = new DateTime(2026, 11, 27, 6, 0, 0, DateTimeKind.Utc)
    };

    private WeatherAlert Received() => _w.Weather.Receive(Alert()).Value!;

    /// <summary>Test: ESV-03 stores external ID, content, area, severity and validity; alert is awaiting review.</summary>
    [Fact]
    public void Receive_StoresAllSuppliedFields()
    {
        var outcome = _w.Weather.Receive(Alert());

        Assert.True(outcome.IsSuccess);
        var a = outcome.Value!;
        Assert.Equal("EC-2026-0042", a.ExternalAlertId);
        Assert.Equal("Up to 10 mm of ice accretion expected.", a.Content);
        Assert.Equal("Waterloo - Uptown", a.AffectedArea);
        Assert.Equal("Severe", a.Severity);
        Assert.Equal(new DateTime(2026, 11, 27, 6, 0, 0, DateTimeKind.Utc), a.ValidTo);
        Assert.Equal(WeatherAlertRules.AwaitingReview, a.ReviewStatus);
    }

    /// <summary>Test: ESV-03 optional fields may be omitted.</summary>
    [Fact]
    public void Receive_OptionalFieldsOmitted_Accepted()
    {
        var a = _w.Weather.Receive(new IncomingWeatherAlertDto { ExternalAlertId = "X1", Title = "Wind advisory" }).Value!;

        Assert.Null(a.AffectedArea);
        Assert.Null(a.Severity);
        Assert.Equal("Wind advisory", a.Content);
    }

    /// <summary>Test: ESV-03 new alerts are visible in the alert list, newest first.</summary>
    [Fact]
    public void Receive_VisibleInListNewestFirst()
    {
        _w.Weather.Receive(Alert("A1"));
        _w.Weather.Receive(Alert("A2"));

        Assert.Equal(new[] { "A2", "A1" }, _w.Weather.GetAll().Select(a => a.ExternalAlertId));
        Assert.Equal(2, _w.Weather.GetAll(WeatherAlertRules.AwaitingReview).Count);
    }

    /// <summary>Test: ESV-03 does not create interventions or change priorities automatically.</summary>
    [Fact]
    public void Receive_DoesNotTouchInterventions()
    {
        var before = _w.Interventions.GetAll().Select(i => (i.Id, i.Priority, i.Status)).ToList();

        _w.Weather.Receive(Alert());

        Assert.Equal(before, _w.Interventions.GetAll().Select(i => (i.Id, i.Priority, i.Status)).ToList());
    }

    /// <summary>Test: ESV-03 / TEC-04 a repeated delivery does not create a duplicate.</summary>
    [Fact]
    public void Receive_SameIdTwice_NoDuplicate()
    {
        var first = _w.Weather.Receive(Alert());
        var second = _w.Weather.Receive(Alert());

        Assert.True(second.Duplicate);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(_w.Weather.GetAll());
        Assert.Single(_w.Exchanges.Query(direction: ExchangeRules.Inbound));
    }

    /// <summary>Test: ESV-03 invalid messages are rejected with errors.</summary>
    [Fact]
    public void Receive_Invalid_Errors()
    {
        var bad = new IncomingWeatherAlertDto
        {
            Severity = "Apocalyptic",
            ValidFrom = new DateTime(2026, 11, 27), ValidTo = new DateTime(2026, 11, 26)
        };

        var errors = _w.Weather.Receive(bad).Errors;

        Assert.Contains("External alert ID is required.", errors);
        Assert.Contains("Title is required.", errors);
        Assert.Contains("Severity must be Minor, Moderate, Severe or Extreme.", errors);
        Assert.Contains("Validity end cannot be before validity start.", errors);
    }

    /// <summary>Test: ESV-04 assessment records reviewer, date, decision, rationale and marks Reviewed.</summary>
    [Fact]
    public void Assess_RecordsAssessmentAndMarksReviewed()
    {
        var alert = Received();
        var before = DateTime.UtcNow;

        var a = _w.Weather.Assess(alert.Id, new WeatherAssessmentDto { Decision = "action required", Rationale = "Bridges will ice." }, "mgr-01").Value!;

        var assessment = Assert.Single(a.Assessments);
        Assert.Equal("mgr-01", assessment.Reviewer);
        Assert.Equal("Action Required", assessment.Decision);
        Assert.Equal("Bridges will ice.", assessment.Rationale);
        Assert.True(assessment.ReviewedAt >= before);
        Assert.Equal(WeatherAlertRules.Reviewed, a.ReviewStatus);
    }

    /// <summary>Test: ESV-04 decision and rationale are required; reviewer needed.</summary>
    [Fact]
    public void Assess_Invalid_Errors()
    {
        var alert = Received();

        var errors = _w.Weather.Assess(alert.Id, new WeatherAssessmentDto { Decision = "Maybe" }, RequestContext.UnknownActor).Errors;

        Assert.Contains("Decision must be Action Required, Monitor or No Impact.", errors);
        Assert.Contains("Rationale is required.", errors);
        Assert.Contains("Reviewer is required.", errors);
        Assert.Equal(OutcomeKind.NotFound, _w.Weather.Assess(999, new WeatherAssessmentDto { Decision = "Monitor", Rationale = "x" }, "m").Kind);
    }

    /// <summary>Test: ESV-04 alert can be set back to Awaiting Review, but not Reviewed without an assessment.</summary>
    [Fact]
    public void ReviewStatus_Transitions()
    {
        var alert = Received();

        Assert.Equal(OutcomeKind.Conflict, _w.Weather.SetReviewStatus(alert.Id, "Reviewed", "m").Kind);

        _w.Weather.Assess(alert.Id, new WeatherAssessmentDto { Decision = "Monitor", Rationale = "Watch it" }, "m");
        Assert.Equal(WeatherAlertRules.AwaitingReview, _w.Weather.SetReviewStatus(alert.Id, "awaiting review", "m").Value!.ReviewStatus);
        Assert.Equal(OutcomeKind.Invalid, _w.Weather.SetReviewStatus(alert.Id, "Done", "m").Kind);
    }

    /// <summary>Test: ESV-05 create an intervention from the alert; it is linked and the decision is kept.</summary>
    [Fact]
    public void Action_CreateIntervention_LinkedAndRecorded()
    {
        var alert = Received();

        var a = _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "CreateIntervention", Priority = "High" }, "mgr-01").Value!;

        var id = Assert.Single(a.LinkedInterventionIds);
        var created = _w.Interventions.GetById(id)!;
        Assert.Equal("Waterloo - Uptown", created.Location);
        Assert.Equal("Weather", created.IssueType);
        Assert.Equal("High", created.Priority);
        var action = Assert.Single(a.Actions);
        Assert.Equal("CreateIntervention", action.Type);
        Assert.Equal("mgr-01", action.Actor);
    }

    /// <summary>Test: ESV-05 link existing interventions; unknown IDs rejected.</summary>
    [Fact]
    public void Action_LinkInterventions()
    {
        var alert = Received();

        var a = _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "LinkInterventions", InterventionIds = new() { 1, 2 } }, "m").Value!;

        Assert.Equal(new List<int> { 1, 2 }, a.LinkedInterventionIds);
        Assert.Equal(OutcomeKind.NotFound, _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "LinkInterventions", InterventionIds = new() { 99 } }, "m").Kind);
    }

    /// <summary>Test: ESV-05 record that no action is required (notes required).</summary>
    [Fact]
    public void Action_NoActionRequired()
    {
        var alert = Received();

        Assert.Equal(OutcomeKind.Invalid, _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "NoActionRequired" }, "m").Kind);
        var a = _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "NoActionRequired", Notes = "No outdoor work planned." }, "m").Value!;

        Assert.Equal("No outdoor work planned.", Assert.Single(a.Actions).Notes);
        Assert.Empty(a.LinkedInterventionIds);
    }

    /// <summary>Test: ESV-05 reschedule linked work; unlinked work cannot be changed.</summary>
    [Fact]
    public void Action_Reschedule_LinkedOnly()
    {
        var alert = Received();
        var start = new DateTime(2026, 11, 28, 8, 0, 0);
        var dto = new WeatherAlertActionDto { Type = "Reschedule", InterventionId = 1, PlannedStartDate = start, PlannedEndDate = start.AddHours(8) };

        Assert.Equal(OutcomeKind.Conflict, _w.Weather.TakeAction(alert.Id, dto, "m").Kind);

        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "LinkInterventions", InterventionIds = new() { 1 } }, "m");
        Assert.True(_w.Weather.TakeAction(alert.Id, dto, "m").IsSuccess);
        Assert.Equal(start, _w.Interventions.GetById(1)!.PlannedStartDate);
        Assert.Equal("Scheduled", _w.Interventions.GetById(1)!.Status);
    }

    /// <summary>Test: ESV-05 put linked work on hold; completed work cannot be.</summary>
    [Fact]
    public void Action_PutOnHold()
    {
        var alert = Received();
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "LinkInterventions", InterventionIds = new() { 1, 2 } }, "m");
        _w.Interventions.CloseIntervention(2, new CloseInterventionDto { CompletionReport = "done" });

        Assert.True(_w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "PutOnHold", InterventionId = 1 }, "m").IsSuccess);
        Assert.Equal("On Hold", _w.Interventions.GetById(1)!.Status);
        Assert.Equal(OutcomeKind.Conflict, _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "PutOnHold", InterventionId = 2 }, "m").Kind);
    }

    /// <summary>Test: ESV-05 / TEC-05 every decision stays on the alert and in the audit history.</summary>
    [Fact]
    public void Decisions_KeptOnAlertAndAudited()
    {
        var alert = Received();
        _w.Weather.Assess(alert.Id, new WeatherAssessmentDto { Decision = "Action Required", Rationale = "Ice" }, "mgr-01");
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "CreateIntervention" }, "mgr-01");
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "NoActionRequired", Notes = "Rest is indoor" }, "mgr-02");

        var a = _w.Weather.GetById(alert.Id)!;
        Assert.Equal(2, a.Actions.Count);
        var audit = _w.Audit.Query("WeatherAlert", alert.Id.ToString());
        Assert.Equal(3, audit.Count);
        Assert.All(audit, e => Assert.False(string.IsNullOrEmpty(e.Actor)));
    }

    /// <summary>Test: ESV-05 invalid action type rejected.</summary>
    [Fact]
    public void Action_InvalidType()
    {
        var alert = Received();

        Assert.Equal(OutcomeKind.Invalid, _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "Panic" }, "m").Kind);
    }

    /// <summary>Test: TEC-05 status changes made from an alert are in the intervention's audit history with the actor.</summary>
    [Fact]
    public void WeatherStatusChanges_AuditedOnIntervention()
    {
        var alert = Received();
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "LinkInterventions", InterventionIds = new() { 1, 2 } }, "mgr-01");
        var start = new DateTime(2026, 11, 28, 8, 0, 0);
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "Reschedule", InterventionId = 1, PlannedStartDate = start, PlannedEndDate = start.AddHours(4) }, "mgr-01");
        _w.Weather.TakeAction(alert.Id, new WeatherAlertActionDto { Type = "PutOnHold", InterventionId = 2 }, "mgr-02");

        var one = _w.Audit.Query("Intervention", "1");
        Assert.Contains(one, e => e.Action == AuditActions.StatusChanged && e.Details.StartsWith("To Do -> Scheduled") && e.Actor == "mgr-01");
        Assert.Contains(one, e => e.Action == AuditActions.Scheduled);
        Assert.Contains(_w.Audit.Query("Intervention", "2"), e => e.Details.Contains("-> On Hold") && e.Actor == "mgr-02");
    }
}
