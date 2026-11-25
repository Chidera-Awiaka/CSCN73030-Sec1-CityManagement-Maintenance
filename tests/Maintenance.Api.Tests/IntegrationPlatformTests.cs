using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Tests;

/// <summary>TEC-03 mocks, TEC-04 exchanges and retries, TEC-05 audit, STF-01, STF-02, TRP-01, TRP-03, REG-03.</summary>
public sealed class IntegrationPlatformTests
{
    private readonly Sprint3TestWorld _w = new();

    /// <summary>Test: TEC-04 outbound success recorded; same key never sent twice.</summary>
    [Fact]
    public void Exchange_IdempotentSend()
    {
        var first = _w.Exchanges.Send("Budget", "Op", "k1", "{}", 1);
        var again = _w.Exchanges.Send("Budget", "Op", "k1", "{}", 1);

        Assert.True(first.Succeeded);
        Assert.True(again.AlreadySent);
        Assert.Equal(first.Exchange.Id, again.Exchange.Id);
        Assert.Single(_w.Mock("Budget").Received);
    }

    /// <summary>Test: TEC-04 failed exchanges are visible with error and attempts; retry succeeds.</summary>
    [Fact]
    public void Exchange_FailureVisibleAndRetry()
    {
        _w.Mock("Security").IsAvailable = false;
        var failed = _w.Exchanges.Send("Security", "Op", "k", "{}", 1).Exchange;

        var visible = Assert.Single(_w.Exchanges.Query(status: "Failed"));
        Assert.Equal(1, visible.Attempts);
        Assert.Contains("unavailable", visible.LastError);

        _w.Exchanges.Retry(failed.Id);
        Assert.Equal(2, _w.Exchanges.GetById(failed.Id)!.Attempts);
        _w.Mock("Security").IsAvailable = true;
        var ok = _w.Exchanges.Retry(failed.Id)!;
        Assert.True(ok.Succeeded);
        Assert.Equal(3, ok.Exchange.Attempts);
        Assert.Null(_w.Exchanges.Retry(999));
    }

    /// <summary>Test: TEC-04 inbound duplicates detected.</summary>
    [Fact]
    public void Exchange_InboundDuplicate()
    {
        Assert.False(_w.Exchanges.RecordInbound("Budget", "Dec", "D1", "{}").Duplicate);
        Assert.True(_w.Exchanges.RecordInbound("Budget", "Dec", "D1", "{}").Duplicate);
        Assert.False(_w.Exchanges.RecordInbound("Security", "Dec", "D1", "{}").Duplicate);
    }

    /// <summary>Test: TEC-04 exchanges persist across restarts.</summary>
    [Fact]
    public void Exchange_Persisted()
    {
        var file = Path.Combine(Path.GetTempPath(), $"exchanges-{Guid.NewGuid()}.json");
        try
        {
            new ExchangeService(ExternalServiceHub.WithMocks(), file).Send("Budget", "Op", "k", "{}", 1);
            Assert.Single(new ExchangeService(ExternalServiceHub.WithMocks(), file).Query());
        }
        finally { File.Delete(file); }
    }

    /// <summary>Test: TEC-05 audit keeps date and actor for users, source for services.</summary>
    [Fact]
    public void Audit_ActorAndSource()
    {
        var before = DateTime.UtcNow;
        _w.Audit.Record("Intervention", 1, AuditActions.StatusChanged, "mgr-01", "To Do -> In Progress");
        _w.Audit.RecordFromService("Intervention", 1, AuditActions.BudgetDecisionReceived, "Budget", "Approved");

        var entries = _w.Audit.Query("intervention", "1");
        Assert.Equal(2, entries.Count);
        Assert.Equal("Budget", entries[0].Source);
        Assert.Null(entries[0].Actor);
        Assert.Equal("mgr-01", entries[1].Actor);
        Assert.Equal("Maintenance", entries[1].Source);
        Assert.All(entries, e => Assert.True(e.Timestamp >= before));
        Assert.Empty(_w.Audit.Query("Intervention", "2"));
    }

    /// <summary>Test: TEC-03 mock returns references and can be switched off.</summary>
    [Fact]
    public void Mock_ReferencesAndAvailability()
    {
        var mock = new MockExternalService("Budget", "BUD");
        Assert.Equal("BUD-1001", mock.Send("Op", "{}").Reference);
        mock.IsAvailable = false;
        Assert.False(mock.Send("Op", "{}").Success);
        Assert.Single(mock.Received);
    }

    /// <summary>Test: STF-01 personnel IDs, names and specializations; filter by specialization; unavailable handled.</summary>
    [Fact]
    public void Staff_ListAndFilter()
    {
        var staff = new MockStaffDirectory();

        Assert.Equal(4, staff.GetPersonnel().Count);
        var road = staff.GetPersonnel("road");
        Assert.Equal(new[] { 101, 104 }, road.Select(p => p.PersonnelId).OrderBy(x => x));
        Assert.All(road, p => Assert.Contains("Road", p.Specializations));
        staff.IsAvailable = false;
        Assert.Throws<StaffUnavailableException>(() => staff.GetPersonnel());
    }

    /// <summary>Test: STF-02 overlapping assignments for the same technician are found; others are not.</summary>
    [Fact]
    public void Conflicts_Overlaps()
    {
        var a = _w.NewIntervention();
        var b = _w.NewIntervention();
        var c = _w.NewIntervention();
        var start = new DateTime(2026, 12, 1, 8, 0, 0);
        foreach (var i in new[] { a, b, c }) _w.Interventions.AssignTechnician(i.Id, 101, "Alex Technician");
        _w.Interventions.ScheduleIntervention(a.Id, start, start.AddHours(8));
        _w.Interventions.ScheduleIntervention(b.Id, start.AddHours(4), start.AddHours(10));
        _w.Interventions.ScheduleIntervention(c.Id, start.AddHours(8), start.AddHours(9));

        var conflicts = AssignmentConflicts.For(_w.Interventions.GetAll(), _w.Interventions.GetById(a.Id)!);

        Assert.Equal(b.Id, Assert.Single(conflicts).InterventionId);
        Assert.Contains("Maintenance only", AssignmentConflicts.Scope);
    }

    /// <summary>Test: STF-02 closed work, other technicians and unscheduled work are not conflicts.</summary>
    [Fact]
    public void Conflicts_Ignored()
    {
        var a = _w.NewIntervention();
        var b = _w.NewIntervention();
        var start = new DateTime(2026, 12, 1, 8, 0, 0);
        _w.Interventions.AssignTechnician(a.Id, 101, "Alex");
        _w.Interventions.AssignTechnician(b.Id, 102, "Sam");
        _w.Interventions.ScheduleIntervention(a.Id, start, start.AddHours(8));
        _w.Interventions.ScheduleIntervention(b.Id, start, start.AddHours(8));

        Assert.Empty(AssignmentConflicts.For(_w.Interventions.GetAll(), _w.Interventions.GetById(a.Id)!));
        Assert.Empty(AssignmentConflicts.For(_w.Interventions.GetAll(), _w.Interventions.GetById(1)!));
    }

    /// <summary>Test: TRP-01 routes have source data and update time when supplied; filter by street; unavailable handled.</summary>
    [Fact]
    public void Transit_Routes()
    {
        var transit = new MockTransitService();

        var king = transit.GetRoutes("King Street");
        Assert.Equal(new[] { "7", "301" }, king.Select(r => r.RouteId));
        Assert.Contains(transit.GetRoutes(), r => r.LastUpdated is null);
        transit.IsAvailable = false;
        Assert.Throws<TransitUnavailableException>(() => transit.GetRoutes());
    }

    /// <summary>Test: TRP-03 diversion keeps external reference, routes, dates; linkable; duplicate ignored; only Public Transport.</summary>
    [Fact]
    public void Diversions_ReceiveAndLink()
    {
        var dto = new RouteDiversionDto
        {
            ExternalReference = "GRT-DIV-88", AffectedRoutes = new() { "7", "301" }, Description = "Detour via Regina St",
            EffectiveFrom = new DateTime(2026, 11, 30, 5, 0, 0), EffectiveTo = new DateTime(2026, 12, 2, 23, 0, 0)
        };

        Assert.Equal(OutcomeKind.Forbidden, _w.Diversions.Receive(dto, "Maintenance").Kind);
        var d = _w.Diversions.Receive(dto, "Public Transport").Value!;
        Assert.Equal("GRT-DIV-88", d.ExternalReference);
        Assert.Equal(new List<string> { "7", "301" }, d.AffectedRoutes);
        Assert.True(_w.Diversions.Receive(dto, "Public Transport").Duplicate);

        Assert.True(_w.Diversions.Link(d.Id, 1).IsSuccess);
        Assert.Equal(OutcomeKind.Conflict, _w.Diversions.Link(d.Id, 1).Kind);
        Assert.Equal(OutcomeKind.NotFound, _w.Diversions.Link(d.Id, 999).Kind);
        Assert.Single(_w.Diversions.GetAll(1));
        Assert.True(_w.Diversions.Unlink(d.Id, 1).IsSuccess);
        Assert.Empty(_w.Diversions.GetAll(1));
    }

    /// <summary>Test: TRP-03 validation.</summary>
    [Fact]
    public void Diversions_Invalid()
    {
        var errors = _w.Diversions.Receive(new RouteDiversionDto(), "Public Transport").Errors;

        Assert.Contains("External reference is required.", errors);
        Assert.Contains("At least one affected route is required.", errors);
        Assert.Contains("Effective-from date is required.", errors);
    }

    /// <summary>Test: REG-03 water, electricity, gas shown; missing explicitly identified.</summary>
    [Fact]
    public void Utilities_MissingIdentified()
    {
        var registry = new MockPropertyRegistry();

        Assert.Empty(registry.GetUtilities("PR-1001")!.MissingUtilities);
        Assert.Equal(new List<string> { "Gas" }, registry.GetUtilities("PR-1002")!.MissingUtilities);
        Assert.Equal(new List<string> { "Water", "Electricity", "Gas" }, registry.GetUtilities("PR-1005")!.MissingUtilities);
        Assert.Null(registry.GetUtilities("PR-9999"));
    }
}
