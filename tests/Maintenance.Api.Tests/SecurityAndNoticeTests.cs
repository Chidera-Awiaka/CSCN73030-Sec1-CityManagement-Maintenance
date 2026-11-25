using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Tests;

/// <summary>SEC-01, SEC-02, SEC-03, ESV-06, TRP-02, REG-04.</summary>
public sealed class SecurityAndNoticeTests
{
    private readonly Sprint3TestWorld _w = new();
    private static readonly DateTime Start = new(2026, 11, 30, 8, 0, 0, DateTimeKind.Utc);

    private SecurityRequestDto Help() => new() { Reason = "Traffic control for lane closure", PeriodStart = Start, PeriodEnd = Start.AddHours(9) };

    /// <summary>Test: SEC-01 request contains reason, location, period and intervention ID; transmission status visible.</summary>
    [Fact]
    public void SecurityRequest_SentWithAllFields()
    {
        var r = _w.Security.Request(1, Help(), "mgr-01").Value!;

        Assert.Equal(ExchangeRules.Succeeded, r.TransmissionStatus);
        Assert.StartsWith("SEC-", r.SecurityReference);
        Assert.Equal("King Street", r.Location);
        var sent = Assert.Single(_w.Mock(ExternalServiceNames.Security).Received).PayloadJson;
        Assert.Contains("\"interventionId\":1", sent);
        Assert.Contains("Traffic control", sent);
        Assert.Contains("periodStart", sent);
    }

    /// <summary>Test: SEC-01 validation.</summary>
    [Fact]
    public void SecurityRequest_Invalid()
    {
        var errors = _w.Security.Request(1, new SecurityRequestDto { PeriodStart = Start, PeriodEnd = Start.AddHours(-1) }, "m").Errors;

        Assert.Contains("Reason is required.", errors);
        Assert.Contains("Period end cannot be before period start.", errors);
        Assert.Equal(OutcomeKind.NotFound, _w.Security.Request(999, Help(), "m").Kind);
    }

    /// <summary>Test: SEC-01 / TEC-04 failed transmission visible, then retried successfully.</summary>
    [Fact]
    public void SecurityRequest_FailedThenRetried()
    {
        _w.Mock(ExternalServiceNames.Security).IsAvailable = false;
        var r = _w.Security.Request(1, Help(), "m").Value!;
        Assert.Equal(ExchangeRules.Failed, r.TransmissionStatus);
        Assert.Contains("unavailable", r.TransmissionError);

        _w.Mock(ExternalServiceNames.Security).IsAvailable = true;
        _w.Exchanges.Retry(r.ExchangeId);

        var after = Assert.Single(_w.Security.GetAll(1));
        Assert.Equal(ExchangeRules.Succeeded, after.TransmissionStatus);
        Assert.NotNull(after.SecurityReference);
    }

    /// <summary>Test: SEC-02 response linked to the request; outcome and comments visible; duplicate ignored.</summary>
    [Fact]
    public void SecurityResponse_Linked()
    {
        var r = _w.Security.Request(1, Help(), "m").Value!;
        var response = new SecurityResponseDto { ResponseId = "RESP-1", RequestReference = r.SecurityReference!, Outcome = "partially approved", Comments = "One officer 08:00-12:00", RespondedBy = "Sgt. Lee" };

        var linked = _w.Security.ReceiveResponse(response, "Security").Value!;

        Assert.Equal("Partially Approved", linked.Response!.Outcome);
        Assert.Equal("One officer 08:00-12:00", linked.Response.Comments);
        Assert.True(_w.Security.ReceiveResponse(response, "Security").Duplicate);
        Assert.Equal(OutcomeKind.Conflict, _w.Security.ReceiveResponse(new SecurityResponseDto { ResponseId = "RESP-2", RequestReference = r.SecurityReference!, Outcome = "Declined" }, "Security").Kind);
    }

    /// <summary>Test: SEC-02 Maintenance users cannot approve on Security's behalf.</summary>
    [Fact]
    public void SecurityResponse_OnlySecurity()
    {
        var r = _w.Security.Request(1, Help(), "m").Value!;
        var response = new SecurityResponseDto { ResponseId = "R", RequestReference = r.SecurityReference!, Outcome = "Approved" };

        Assert.Equal(OutcomeKind.Forbidden, _w.Security.ReceiveResponse(response, null).Kind);
        Assert.Equal(OutcomeKind.Forbidden, _w.Security.ReceiveResponse(response, "Maintenance").Kind);
        Assert.Null(_w.Security.GetAll(1).Single().Response);
    }

    /// <summary>Test: ESV-06 / SEC-03 / TRP-02 road closure sent to all three services with location, dates, status and ID.</summary>
    [Fact]
    public void RoadClosure_SentToThreeServices()
    {
        _w.Interventions.ScheduleIntervention(1, Start, Start.AddHours(9));

        var n = _w.Notices.SendRoadClosure(1, new RoadClosureDto { ClosureStatus = "closed" }, "mgr-01").Value!;

        Assert.Equal(3, n.Deliveries.Count);
        Assert.All(n.Deliveries, d => Assert.Equal(ExchangeRules.Succeeded, d.Status));
        foreach (var service in new[] { "E-services", "Security", "Public Transport" })
        {
            var payload = Assert.Single(_w.Mock(service).Received).PayloadJson;
            Assert.Contains("\"closureStatus\":\"Closed\"", payload);
            Assert.Contains("\"location\":\"King Street\"", payload);
            Assert.Contains("\"interventionStatus\":\"Scheduled\"", payload);
            Assert.Contains("\"interventionId\":1", payload);
            Assert.Contains("plannedEnd", payload);
        }
    }

    /// <summary>Test: ESV-06 / TRP-02 reopening can be sent; resending the same notice is not duplicated.</summary>
    [Fact]
    public void RoadClosure_ReopenAndNoDuplicate()
    {
        var dto = new RoadClosureDto { ClosureStatus = "Closed", PlannedStart = Start, PlannedEnd = Start.AddHours(4), Services = new() { "Public Transport" } };
        _w.Notices.SendRoadClosure(1, dto, "m");
        Assert.True(_w.Notices.SendRoadClosure(1, dto, "m").Duplicate);

        _w.Notices.SendRoadClosure(1, new RoadClosureDto { ClosureStatus = "Reopened", PlannedStart = Start, PlannedEnd = Start.AddHours(4), Services = new() { "Public Transport" } }, "m");

        Assert.Equal(2, _w.Mock("Public Transport").Received.Count);
        Assert.Contains("Reopened", _w.Mock("Public Transport").Received[1].PayloadJson);
    }

    /// <summary>Test: ESV-06 success or failure recorded per service.</summary>
    [Fact]
    public void RoadClosure_PartialFailureRecorded()
    {
        _w.Mock("E-services").IsAvailable = false;

        var n = _w.Notices.SendRoadClosure(1, new RoadClosureDto { ClosureStatus = "Closed", PlannedStart = Start }, "m").Value!;

        Assert.Equal(ExchangeRules.Failed, n.Deliveries.Single(d => d.Service == "E-services").Status);
        Assert.Equal(ExchangeRules.Succeeded, n.Deliveries.Single(d => d.Service == "Security").Status);
    }

    /// <summary>Test: road closure validation.</summary>
    [Fact]
    public void RoadClosure_Invalid()
    {
        var errors = _w.Notices.SendRoadClosure(1, new RoadClosureDto { ClosureStatus = "Maybe", Services = new() { "Weather Network" } }, "m").Errors;

        Assert.Contains("Closure status must be Closed, Updated or Reopened.", errors);
        Assert.Contains("Planned start date is required (schedule the intervention or supply it).", errors);
        Assert.Contains("Unknown service: Weather Network.", errors);
    }

    /// <summary>Test: SEC-03 work-status update to Security with location, dates and status.</summary>
    [Fact]
    public void WorkStatus_SentToSecurity()
    {
        _w.Interventions.ScheduleIntervention(1, Start, Start.AddHours(2));
        _w.Interventions.UpdateStatus(1, "In Progress");

        var n = _w.Notices.SendWorkStatus(1, null, "m").Value!;

        Assert.Equal("Security", Assert.Single(n.Deliveries).Service);
        var payload = Assert.Single(_w.Mock("Security").Received).PayloadJson;
        Assert.Contains("\"interventionStatus\":\"In Progress\"", payload);
        Assert.Contains("plannedStart", payload);
    }

    /// <summary>Test: REG-04 disruption includes property IDs, type, period, intervention; updates and restoration can be sent.</summary>
    [Fact]
    public void PropertyDisruption_NewAndRestored()
    {
        Assert.Contains("Link at least one property to the intervention first (REG-02).",
            _w.Notices.SendPropertyDisruption(1, new PropertyDisruptionDto { DisruptionType = "Water", PeriodStart = Start }, "m").Errors);

        _w.Interventions.LinkProperty(1, "PR-1001");
        _w.Interventions.LinkProperty(1, "PR-1002");
        _w.Notices.SendPropertyDisruption(1, new PropertyDisruptionDto { DisruptionType = "water", PeriodStart = Start, PeriodEnd = Start.AddHours(6) }, "m");
        _w.Notices.SendPropertyDisruption(1, new PropertyDisruptionDto { DisruptionType = "Water", UpdateType = "Restored", PeriodStart = Start, PeriodEnd = Start.AddHours(5) }, "m");

        var received = _w.Mock(ExternalServiceNames.PropertyRegistration).Received;
        Assert.Equal(2, received.Count);
        Assert.Contains("\"propertyIds\":[\"PR-1001\",\"PR-1002\"]", received[0].PayloadJson);
        Assert.Contains("\"disruptionType\":\"Water\"", received[0].PayloadJson);
        Assert.Contains("\"interventionId\":1", received[0].PayloadJson);
        Assert.Contains("\"updateType\":\"Restored\"", received[1].PayloadJson);
    }
}
