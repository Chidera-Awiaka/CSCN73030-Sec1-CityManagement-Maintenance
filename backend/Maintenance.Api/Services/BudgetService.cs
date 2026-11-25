using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>Estimated vs actual cost (BUD-05).</summary>
/// <param name="InterventionId">Intervention.</param>
/// <param name="EstimatedTotal">Estimated total, when an estimate exists.</param>
/// <param name="ActualCost">Actual cost, when recorded (MNT-08).</param>
/// <param name="Difference">Actual - estimated, when both exist.</param>
/// <param name="DifferencePercent">Difference as a percentage of the estimate, when both exist.</param>
/// <param name="Available">True when both amounts exist.</param>
/// <param name="Message">Explanation when the comparison is not available.</param>
public record CostComparison(int InterventionId, decimal? EstimatedTotal, decimal? ActualCost, decimal? Difference,
    decimal? DifferencePercent, bool Available, string? Message);

/// <summary>
/// Budget integration.
/// BUD-01 estimate (repair, material, equipment; automatic total),
/// BUD-02 submit to Budget (intervention ID, breakdown, total; keeps the external reference; status Pending),
/// BUD-03 receive the decision (linked to the request; details recorded; visible on the intervention),
/// BUD-05 compare estimated and actual cost.
/// BUD-04 is enforced through <see cref="BudgetPolicy"/>.
/// </summary>
public class BudgetService
{
    private const string Operation = "BudgetRequest";
    private readonly InterventionService _interventions;
    private readonly ExchangeService _exchanges;
    private readonly AuditService _audit;

    /// <summary>Creates the service and listens for successful retries of budget requests.</summary>
    /// <param name="interventions">Intervention service.</param>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="audit">Audit history (TEC-05).</param>
    public BudgetService(InterventionService interventions, ExchangeService exchanges, AuditService audit)
    {
        _interventions = interventions;
        _exchanges = exchanges;
        _audit = audit;
        _exchanges.Succeeded += OnExchangeSucceeded;
    }

    /// <summary>Enters or replaces the cost estimate (BUD-01). Not allowed while Pending or Approved.</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Repair, material and equipment costs.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<Intervention> Estimate(int id, CostEstimateDto request, string actor)
    {
        if (request is null) return ServiceOutcome<Intervention>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var errors = new List<string>();
        if (request.RepairCost < 0) errors.Add("Repair cost cannot be negative.");
        if (request.MaterialCost < 0) errors.Add("Material cost cannot be negative.");
        if (request.EquipmentCost < 0) errors.Add("Equipment cost cannot be negative.");
        if (request.RepairCost + request.MaterialCost + request.EquipmentCost <= 0) errors.Add("The estimate total must be greater than zero.");
        if (errors.Count > 0) return ServiceOutcome<Intervention>.Fail(OutcomeKind.Invalid, errors);

        var intervention = _interventions.GetById(id);
        if (intervention is null) return ServiceOutcome<Intervention>.Fail(OutcomeKind.NotFound, "Intervention not found.");
        if (intervention.BudgetStatus is BudgetRules.Pending or BudgetRules.Approved)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Conflict,
                $"The estimate cannot be changed while the budget status is {intervention.BudgetStatus}.");

        var estimate = new CostEstimate
        {
            RepairCost = request.RepairCost,
            MaterialCost = request.MaterialCost,
            EquipmentCost = request.EquipmentCost,
            EstimatedBy = actor,
            EstimatedAt = DateTime.UtcNow
        };
        _interventions.Update(id, i => i.CostEstimate = estimate);
        _audit.Record("Intervention", id, AuditActions.CostEstimated, actor,
            $"Repair {estimate.RepairCost:0.00} + material {estimate.MaterialCost:0.00} + equipment {estimate.EquipmentCost:0.00} = {estimate.Total:0.00}");
        return ServiceOutcome<Intervention>.Ok(_interventions.GetById(id)!);
    }

    /// <summary>
    /// Submits the estimate to Budget (BUD-02). On success the status becomes Pending and the
    /// Budget reference is kept. Submitting the same estimate again does not create a duplicate
    /// request (TEC-04); a failed submission can be retried.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<Intervention> Submit(int id, string actor)
    {
        var intervention = _interventions.GetById(id);
        if (intervention is null) return ServiceOutcome<Intervention>.Fail(OutcomeKind.NotFound, "Intervention not found.");
        if (intervention.CostEstimate is null)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Invalid, "Enter a cost estimate before submitting it to Budget.");
        if (intervention.BudgetStatus == BudgetRules.Approved)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Conflict, "The budget is already approved.");
        if (intervention.BudgetStatus == BudgetRules.Rejected
            && intervention.BudgetDecision is not null
            && intervention.CostEstimate.EstimatedAt <= intervention.BudgetDecision.ReceivedAt)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Conflict,
                "The previous estimate was rejected. Enter a revised estimate before submitting again.");

        var e = intervention.CostEstimate;
        var payload = JsonSerializer.Serialize(new
        {
            interventionId = id,
            description = intervention.Description,
            costBreakdown = new { repair = e.RepairCost, material = e.MaterialCost, equipment = e.EquipmentCost },
            total = e.Total,
            requestedBy = actor
        });
        var key = $"budget-request:{id}:{e.EstimatedAt:O}";
        var sent = _exchanges.Send(ExternalServiceNames.Budget, Operation, key, payload, id);

        if (!sent.Succeeded)
        {
            return new ServiceOutcome<Intervention>(OutcomeKind.ExternalFailure, null, new List<string>
            {
                $"The Budget service did not accept the request: {sent.Exchange.LastError} " +
                $"It is recorded as exchange #{sent.Exchange.Id} and can be retried."
            });
        }

        ApplySubmitted(id, sent.Exchange.ExternalReference!);
        if (!sent.AlreadySent)
            _audit.Record("Intervention", id, AuditActions.BudgetSubmitted, actor, $"Estimate {e.Total:0.00} submitted, reference {sent.Exchange.ExternalReference}");
        return ServiceOutcome<Intervention>.Ok(_interventions.GetById(id)!, duplicate: sent.AlreadySent);
    }

    /// <summary>
    /// Receives a decision from Budget (BUD-03), linked by the request reference. Only the Budget
    /// service may send it. A repeated delivery is ignored (TEC-04).
    /// </summary>
    /// <param name="request">Decision message sent by Budget.</param>
    /// <param name="sourceService">Value of the X-Source-Service header (the calling service).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<Intervention> ReceiveDecision(BudgetDecisionDto request, string? sourceService)
    {
        if (!string.Equals(sourceService, ExternalServiceNames.Budget, StringComparison.OrdinalIgnoreCase))
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Forbidden, "Only the Budget service can send budget decisions (X-Source-Service: Budget).");

        var errors = new List<string>();
        if (request is null) return ServiceOutcome<Intervention>.Fail(OutcomeKind.Invalid, "Request body is required.");
        if (string.IsNullOrWhiteSpace(request.DecisionId)) errors.Add("Decision ID is required.");
        if (string.IsNullOrWhiteSpace(request.RequestReference)) errors.Add("Request reference is required.");
        var decision = BudgetRules.Decisions.FirstOrDefault(d => d.Equals(request.Decision?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (decision is null) errors.Add("Decision must be Approved or Rejected.");
        if (request.ApprovedAmount is < 0) errors.Add("Approved amount cannot be negative.");
        if (errors.Count > 0) return ServiceOutcome<Intervention>.Fail(OutcomeKind.Invalid, errors);

        var intervention = _interventions.GetAll().FirstOrDefault(i => i.BudgetRequestReference == request.RequestReference.Trim());
        if (intervention is null)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.NotFound, $"No budget request with reference {request.RequestReference}.");

        var (_, duplicate) = _exchanges.RecordInbound(ExternalServiceNames.Budget, "BudgetDecision",
            request.DecisionId.Trim(), JsonSerializer.Serialize(request), intervention.Id);
        if (duplicate) return ServiceOutcome<Intervention>.Ok(intervention, duplicate: true);

        if (intervention.BudgetStatus != BudgetRules.Pending)
            return ServiceOutcome<Intervention>.Fail(OutcomeKind.Conflict,
                $"Intervention {intervention.Id} is not waiting for a budget decision (status {intervention.BudgetStatus}).");

        _interventions.Update(intervention.Id, i =>
        {
            i.BudgetStatus = decision!;
            i.BudgetDecision = new BudgetDecision
            {
                Decision = decision!,
                ApprovedAmount = request.ApprovedAmount,
                Comments = request.Comments?.Trim(),
                DecidedBy = request.DecidedBy?.Trim(),
                DecisionId = request.DecisionId.Trim(),
                ReceivedAt = DateTime.UtcNow
            };
        });
        _audit.RecordFromService("Intervention", intervention.Id, AuditActions.BudgetDecisionReceived, ExternalServiceNames.Budget,
            $"{decision}{(request.ApprovedAmount is null ? "" : $" ({request.ApprovedAmount:0.00})")}" +
            $"{(string.IsNullOrWhiteSpace(request.DecidedBy) ? "" : $" by {request.DecidedBy}")}: {request.Comments}");
        return ServiceOutcome<Intervention>.Ok(_interventions.GetById(intervention.Id)!);
    }

    /// <summary>Compares estimated and actual cost (BUD-05).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<CostComparison> Compare(int id)
    {
        var i = _interventions.GetById(id);
        if (i is null) return ServiceOutcome<CostComparison>.Fail(OutcomeKind.NotFound, "Intervention not found.");

        var estimated = i.CostEstimate?.Total;
        var actual = i.ActualCost;
        if (estimated is null || actual is null)
        {
            var missing = estimated is null && actual is null ? "there is no estimate and no actual cost yet"
                : estimated is null ? "there is no estimate yet" : "there is no actual cost yet (it is recorded when the intervention is closed)";
            return ServiceOutcome<CostComparison>.Ok(new CostComparison(id, estimated, actual, null, null, false,
                $"Comparison not available: {missing}."));
        }

        var difference = actual.Value - estimated.Value;
        var percent = estimated.Value == 0 ? (decimal?)null : Math.Round(difference / estimated.Value * 100, 1);
        return ServiceOutcome<CostComparison>.Ok(new CostComparison(id, estimated, actual, difference, percent, true, null));
    }

    private void ApplySubmitted(int id, string reference) =>
        _interventions.Update(id, i =>
        {
            i.BudgetStatus = BudgetRules.Pending;
            i.BudgetRequestReference = reference;
            i.BudgetDecision = null;
        });

    private void OnExchangeSucceeded(ExternalExchange exchange)
    {
        if (exchange.Operation != Operation || exchange.InterventionId is null) return;
        var i = _interventions.GetById(exchange.InterventionId.Value);
        if (i is not null && i.BudgetStatus is BudgetRules.NotSubmitted or BudgetRules.Rejected
            && i.BudgetRequestReference != exchange.ExternalReference)
        {
            ApplySubmitted(i.Id, exchange.ExternalReference!);
            _audit.Record("Intervention", i.Id, AuditActions.BudgetSubmitted, "exchange retry",
                $"Estimate {i.CostEstimate?.Total:0.00} submitted on retry (exchange #{exchange.Id}), reference {exchange.ExternalReference}");
        }
    }
}
