namespace Maintenance.Api.Models;

/// <summary>Cost estimate for an intervention (BUD-01). The total is calculated automatically.</summary>
public class CostEstimate
{
    /// <summary>Repair (labour) cost in dollars.</summary>
    public decimal RepairCost { get; set; }

    /// <summary>Material cost in dollars.</summary>
    public decimal MaterialCost { get; set; }

    /// <summary>Equipment cost in dollars.</summary>
    public decimal EquipmentCost { get; set; }

    /// <summary>Total = repair + material + equipment (calculated, BUD-01).</summary>
    public decimal Total => RepairCost + MaterialCost + EquipmentCost;

    /// <summary>Who entered the estimate.</summary>
    public string EstimatedBy { get; set; } = "";

    /// <summary>When the estimate was entered (UTC).</summary>
    public DateTime EstimatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Decision received from the Budget service (BUD-03).</summary>
public class BudgetDecision
{
    /// <summary>Approved or Rejected.</summary>
    public string Decision { get; set; } = "";

    /// <summary>Amount approved, when supplied.</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>Comments supplied by Budget.</summary>
    public string? Comments { get; set; }

    /// <summary>Budget officer who decided, when supplied.</summary>
    public string? DecidedBy { get; set; }

    /// <summary>Budget's ID for the decision message.</summary>
    public string DecisionId { get; set; } = "";

    /// <summary>When Maintenance received the decision (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Budget statuses and rules (BUD-02, BUD-03, BUD-04).</summary>
public static class BudgetRules
{
    /// <summary>No estimate submitted yet.</summary>
    public const string NotSubmitted = "Not Submitted";
    /// <summary>Submitted to Budget, waiting for a decision (BUD-02).</summary>
    public const string Pending = "Pending";
    /// <summary>Approved by Budget (BUD-03).</summary>
    public const string Approved = "Approved";
    /// <summary>Rejected by Budget (BUD-03).</summary>
    public const string Rejected = "Rejected";

    /// <summary>Decisions Budget can send.</summary>
    public static readonly string[] Decisions = { Approved, Rejected };

    /// <summary>Status that BUD-04 protects.</summary>
    public const string InProgressStatus = "In Progress";
}

/// <summary>
/// BUD-04 setting. The requirement is "pending confirmation", so the rule is off unless
/// "Budget:RequireApprovalToStart" is true in appsettings.json.
/// </summary>
public class BudgetPolicy
{
    /// <summary>When true, an intervention can only move to In Progress once its budget is Approved.</summary>
    public bool RequireApprovalToStart { get; set; }

    /// <summary>
    /// Returns the reason the status change is blocked, or null when it is allowed (BUD-04).
    /// </summary>
    /// <param name="intervention">The intervention.</param>
    /// <param name="newStatus">Requested status.</param>
    /// <returns>The value, or null.</returns>
    public string? BlockReason(Intervention intervention, string newStatus)
    {
        if (!RequireApprovalToStart
            || !string.Equals(newStatus, BudgetRules.InProgressStatus, StringComparison.OrdinalIgnoreCase)
            || intervention.BudgetStatus == BudgetRules.Approved)
        {
            return null;
        }

        return $"Work cannot start: the budget status is \"{intervention.BudgetStatus}\". " +
               "The cost estimate must be approved by the Budget service before the intervention can move to In Progress (BUD-04).";
    }
}
