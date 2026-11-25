using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Tests;

/// <summary>BUD-01 estimate, BUD-02 submit, BUD-03 decision, BUD-04 rule, BUD-05 comparison.</summary>
public sealed class BudgetTests
{
    private readonly Sprint3TestWorld _w = new();

    private static CostEstimateDto Estimate(decimal repair = 1200, decimal material = 350.5m, decimal equipment = 400) =>
        new() { RepairCost = repair, MaterialCost = material, EquipmentCost = equipment };

    private string SubmitAndGetReference(int id = 1)
    {
        _w.Budget.Estimate(id, Estimate(), "mgr-01");
        return _w.Budget.Submit(id, "mgr-01").Value!.BudgetRequestReference!;
    }

    private BudgetDecisionDto Decision(string reference, string decision = "Approved", string id = "DEC-1") =>
        new() { DecisionId = id, RequestReference = reference, Decision = decision, ApprovedAmount = 1950.5m, Comments = "OK", DecidedBy = "B. Officer" };

    /// <summary>Test: BUD-01 three costs entered separately; total calculated automatically.</summary>
    [Fact]
    public void Estimate_TotalCalculated()
    {
        var i = _w.Budget.Estimate(1, Estimate(), "mgr-01").Value!;

        Assert.Equal(1200m, i.CostEstimate!.RepairCost);
        Assert.Equal(350.5m, i.CostEstimate.MaterialCost);
        Assert.Equal(400m, i.CostEstimate.EquipmentCost);
        Assert.Equal(1950.5m, i.CostEstimate.Total);
        Assert.Equal("mgr-01", i.CostEstimate.EstimatedBy);
    }

    /// <summary>Test: BUD-01 negative or zero estimates rejected.</summary>
    [Fact]
    public void Estimate_Invalid()
    {
        var errors = _w.Budget.Estimate(1, Estimate(-1, 0, 0), "m").Errors;

        Assert.Contains("Repair cost cannot be negative.", errors);
        Assert.Contains("The estimate total must be greater than zero.", errors);
        Assert.Equal(OutcomeKind.NotFound, _w.Budget.Estimate(999, Estimate(), "m").Kind);
    }

    /// <summary>Test: BUD-02 request contains intervention ID, breakdown and total; reference kept; status Pending.</summary>
    [Fact]
    public void Submit_SendsBreakdownAndSetsPending()
    {
        _w.Budget.Estimate(1, Estimate(), "mgr-01");

        var i = _w.Budget.Submit(1, "mgr-01").Value!;

        Assert.Equal(BudgetRules.Pending, i.BudgetStatus);
        Assert.StartsWith("BUD-", i.BudgetRequestReference);
        var sent = Assert.Single(_w.Mock(ExternalServiceNames.Budget).Received);
        Assert.Contains("\"interventionId\":1", sent.PayloadJson);
        Assert.Contains("\"repair\":1200", sent.PayloadJson);
        Assert.Contains("\"total\":1950.5", sent.PayloadJson);
    }

    /// <summary>Test: BUD-02 cannot submit without an estimate.</summary>
    [Fact]
    public void Submit_NoEstimate_Invalid()
    {
        Assert.Equal(OutcomeKind.Invalid, _w.Budget.Submit(1, "m").Kind);
    }

    /// <summary>Test: BUD-02 / TEC-04 submitting twice does not create a duplicate request.</summary>
    [Fact]
    public void Submit_Twice_NoDuplicate()
    {
        _w.Budget.Estimate(1, Estimate(), "m");
        var first = _w.Budget.Submit(1, "m");
        var second = _w.Budget.Submit(1, "m");

        Assert.True(second.Duplicate);
        Assert.Equal(first.Value!.BudgetRequestReference, second.Value!.BudgetRequestReference);
        Assert.Single(_w.Mock(ExternalServiceNames.Budget).Received);
    }

    /// <summary>Test: BUD-02 / TEC-04 failure is visible; retry succeeds and sets Pending without a duplicate.</summary>
    [Fact]
    public void Submit_BudgetDown_FailedThenRetried()
    {
        _w.Budget.Estimate(1, Estimate(), "m");
        _w.Mock(ExternalServiceNames.Budget).IsAvailable = false;

        var failed = _w.Budget.Submit(1, "m");
        Assert.Equal(OutcomeKind.ExternalFailure, failed.Kind);
        Assert.Equal(BudgetRules.NotSubmitted, _w.Interventions.GetById(1)!.BudgetStatus);
        var exchange = Assert.Single(_w.Exchanges.Query(status: ExchangeRules.Failed));

        _w.Mock(ExternalServiceNames.Budget).IsAvailable = true;
        Assert.True(_w.Exchanges.Retry(exchange.Id)!.Succeeded);
        Assert.Equal(BudgetRules.Pending, _w.Interventions.GetById(1)!.BudgetStatus);
        Assert.True(_w.Exchanges.Retry(exchange.Id)!.AlreadySent);
        Assert.Single(_w.Mock(ExternalServiceNames.Budget).Received);
    }

    /// <summary>Test: BUD-03 decision linked to the request; details recorded; visible on the intervention; audited with source.</summary>
    [Fact]
    public void Decision_RecordedOnIntervention()
    {
        var reference = SubmitAndGetReference();

        var i = _w.Budget.ReceiveDecision(Decision(reference), "Budget").Value!;

        Assert.Equal(BudgetRules.Approved, i.BudgetStatus);
        Assert.Equal(1950.5m, i.BudgetDecision!.ApprovedAmount);
        Assert.Equal("B. Officer", i.BudgetDecision.DecidedBy);
        Assert.Equal("OK", i.BudgetDecision.Comments);
        var audit = _w.Audit.Query("Intervention", "1").First(e => e.Action == AuditActions.BudgetDecisionReceived);
        Assert.Equal("Budget", audit.Source);
    }

    /// <summary>Test: BUD-03 only Budget can send decisions; unknown reference 404; repeated delivery ignored.</summary>
    [Fact]
    public void Decision_SourceReferenceAndDuplicates()
    {
        var reference = SubmitAndGetReference();

        Assert.Equal(OutcomeKind.Forbidden, _w.Budget.ReceiveDecision(Decision(reference), null).Kind);
        Assert.Equal(OutcomeKind.Forbidden, _w.Budget.ReceiveDecision(Decision(reference), "Maintenance").Kind);
        Assert.Equal(OutcomeKind.NotFound, _w.Budget.ReceiveDecision(Decision("BUD-0"), "Budget").Kind);
        Assert.True(_w.Budget.ReceiveDecision(Decision(reference), "Budget").IsSuccess);
        Assert.True(_w.Budget.ReceiveDecision(Decision(reference), "Budget").Duplicate);
        Assert.Equal(OutcomeKind.Conflict, _w.Budget.ReceiveDecision(Decision(reference, "Rejected", "DEC-2"), "Budget").Kind);
    }

    /// <summary>Test: BUD-03 rejection requires a revised estimate before resubmitting.</summary>
    [Fact]
    public void Rejected_RequiresRevisedEstimate()
    {
        var reference = SubmitAndGetReference();
        _w.Budget.ReceiveDecision(Decision(reference, "Rejected"), "Budget");

        Assert.Equal(OutcomeKind.Conflict, _w.Budget.Submit(1, "m").Kind);
        _w.Budget.Estimate(1, Estimate(900, 200, 100), "m");
        var resubmitted = _w.Budget.Submit(1, "m").Value!;
        Assert.Equal(BudgetRules.Pending, resubmitted.BudgetStatus);
        Assert.NotEqual(reference, resubmitted.BudgetRequestReference);
    }

    /// <summary>Test: BUD-01 estimate locked while Pending.</summary>
    [Fact]
    public void Estimate_LockedWhilePending()
    {
        SubmitAndGetReference();

        Assert.Equal(OutcomeKind.Conflict, _w.Budget.Estimate(1, Estimate(), "m").Kind);
    }

    /// <summary>Test: BUD-04 when enabled, In Progress blocked unless Approved, with a clear reason; off by default.</summary>
    [Fact]
    public void Policy_BlocksStartUnlessApproved()
    {
        var off = new BudgetPolicy();
        var on = new BudgetPolicy { RequireApprovalToStart = true };
        var i = _w.Interventions.GetById(1)!;

        Assert.Null(off.BlockReason(i, "In Progress"));
        Assert.Contains("BUD-04", on.BlockReason(i, "In Progress"));
        Assert.Null(on.BlockReason(i, "On Hold"));

        var reference = SubmitAndGetReference();
        _w.Budget.ReceiveDecision(Decision(reference), "Budget");
        Assert.Null(on.BlockReason(_w.Interventions.GetById(1)!, "In Progress"));
    }

    /// <summary>Test: BUD-05 both amounts and difference shown when actual cost exists.</summary>
    [Fact]
    public void Compare_EstimatedVsActual()
    {
        _w.Budget.Estimate(1, Estimate(1000, 0, 0), "m");
        Assert.False(_w.Budget.Compare(1).Value!.Available);

        _w.Interventions.CloseIntervention(1, new CloseInterventionDto { CompletionReport = "done", ActualCost = 1150 });
        var c = _w.Budget.Compare(1).Value!;

        Assert.True(c.Available);
        Assert.Equal(1000m, c.EstimatedTotal);
        Assert.Equal(1150m, c.ActualCost);
        Assert.Equal(150m, c.Difference);
        Assert.Equal(15.0m, c.DifferencePercent);
    }

    /// <summary>Test: BUD-05 explains what is missing.</summary>
    [Fact]
    public void Compare_MissingData_Explained()
    {
        var c = _w.Budget.Compare(1).Value!;

        Assert.False(c.Available);
        Assert.Contains("no estimate and no actual cost", c.Message);
        Assert.Equal(OutcomeKind.NotFound, _w.Budget.Compare(999).Kind);
    }
}
