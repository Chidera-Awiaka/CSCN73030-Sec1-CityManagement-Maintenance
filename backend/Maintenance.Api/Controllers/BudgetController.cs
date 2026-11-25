using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Models;
using Maintenance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Controllers;

/// <summary>Budget endpoints (BUD-01, BUD-02, BUD-03, BUD-05). BUD-04 is enforced on the status endpoint.</summary>
[ApiController]
public class BudgetController : ControllerBase
{
    private readonly BudgetService _service;
    private readonly BudgetPolicy _policy;

    /// <summary>Creates the controller.</summary>
    /// <param name="service">Service that implements the operations.</param>
    /// <param name="policy">BUD-04 setting.</param>
    public BudgetController(BudgetService service, BudgetPolicy policy)
    {
        _service = service;
        _policy = policy;
    }

    /// <summary>Enters the cost estimate: repair, material and equipment; total calculated (BUD-01).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Repair, material and equipment costs.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPut("api/interventions/{id:int}/budget/estimate")]
    public IActionResult Estimate(int id, CostEstimateDto request) =>
        this.ToResult(_service.Estimate(id, request, RequestContext.Actor(Request)), Summary);

    /// <summary>Submits the estimate to Budget; status becomes Pending (BUD-02). 502 when Budget is unavailable (retry later).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/interventions/{id:int}/budget/submit")]
    public IActionResult Submit(int id)
    {
        var outcome = _service.Submit(id, RequestContext.Actor(Request));
        return this.ToResult(outcome, i => Ok(new { alreadySubmitted = outcome.Duplicate, budget = SummaryOf(i) }));
    }

    /// <summary>Budget summary for an intervention: estimate, status, reference, decision, comparison.</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/interventions/{id:int}/budget")]
    public IActionResult Get(int id)
    {
        var comparison = _service.Compare(id);
        return this.ToResult(comparison, c => Ok(new
        {
            comparison = c,
            requireApprovalToStart = _policy.RequireApprovalToStart
        }));
    }

    /// <summary>Estimated vs actual cost and the difference (BUD-05).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/interventions/{id:int}/budget/comparison")]
    public IActionResult Compare(int id) => this.ToResult(_service.Compare(id));

    /// <summary>
    /// Receives a decision from the Budget service (BUD-03). Requires header X-Source-Service: Budget.
    /// 200 with the updated budget; { duplicate: true } when already received.
    /// </summary>
    /// <param name="request">Decision message sent by Budget.</param>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpPost("api/budget/decisions")]
    public IActionResult ReceiveDecision(BudgetDecisionDto request)
    {
        var outcome = _service.ReceiveDecision(request, RequestContext.SourceService(Request));
        return this.ToResult(outcome, i => Ok(new { duplicate = outcome.Duplicate, budget = SummaryOf(i) }));
    }

    /// <summary>Budget policy (BUD-04 setting).</summary>
    /// <returns>The HTTP result (status codes described above).</returns>
    [HttpGet("api/budget/policy")]
    public IActionResult Policy() => Ok(new { requireApprovalToStart = _policy.RequireApprovalToStart });

    private IActionResult Summary(Intervention i) => Ok(SummaryOf(i));

    private static object SummaryOf(Intervention i) => new
    {
        interventionId = i.Id,
        costEstimate = i.CostEstimate is null ? null : new
        {
            i.CostEstimate.RepairCost,
            i.CostEstimate.MaterialCost,
            i.CostEstimate.EquipmentCost,
            i.CostEstimate.Total,
            i.CostEstimate.EstimatedBy,
            i.CostEstimate.EstimatedAt
        },
        budgetStatus = i.BudgetStatus,
        budgetRequestReference = i.BudgetRequestReference,
        budgetDecision = i.BudgetDecision
    };
}
