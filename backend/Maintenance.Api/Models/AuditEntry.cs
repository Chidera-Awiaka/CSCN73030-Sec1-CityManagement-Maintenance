namespace Maintenance.Api.Models;

/// <summary>
/// One line of the audit history (TEC-05): what changed, when, and who or which
/// external service made the change.
/// </summary>
public class AuditEntry
{
    /// <summary>Audit entry ID.</summary>
    public int Id { get; set; }

    /// <summary>When the change happened (UTC).</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Kind of record changed, e.g. Intervention, WeatherAlert, Budget.</summary>
    public string EntityType { get; set; } = "";

    /// <summary>ID of the record changed.</summary>
    public string EntityId { get; set; } = "";

    /// <summary>What happened, e.g. StatusChanged, TechnicianAssigned, WeatherAssessment, BudgetDecisionReceived.</summary>
    public string Action { get; set; } = "";

    /// <summary>Maintenance user who made the change, or null when an external service made it.</summary>
    public string? Actor { get; set; }

    /// <summary>External service that caused the change (e.g. Budget), or "Maintenance" for internal changes.</summary>
    public string Source { get; set; } = AuditSources.Maintenance;

    /// <summary>Human-readable details, e.g. "To Do -> In Progress".</summary>
    public string Details { get; set; } = "";
}

/// <summary>Audit actions and sources (TEC-05).</summary>
public static class AuditSources
{
    /// <summary>Change made inside Maintenance by a user.</summary>
    public const string Maintenance = "Maintenance";
}

/// <summary>Audit action names (TEC-05).</summary>
public static class AuditActions
{
    /// <summary>Intervention status changed.</summary>
    public const string StatusChanged = "StatusChanged";
    /// <summary>Intervention priority changed.</summary>
    public const string PriorityChanged = "PriorityChanged";
    /// <summary>Technician assigned.</summary>
    public const string TechnicianAssigned = "TechnicianAssigned";
    /// <summary>Intervention scheduled or rescheduled.</summary>
    public const string Scheduled = "Scheduled";
    /// <summary>Intervention closed.</summary>
    public const string Closed = "Closed";
    /// <summary>Weather alert assessed.</summary>
    public const string WeatherAssessment = "WeatherAssessment";
    /// <summary>Action taken from a weather alert.</summary>
    public const string WeatherAction = "WeatherAction";
    /// <summary>Cost estimate entered.</summary>
    public const string CostEstimated = "CostEstimated";
    /// <summary>Estimate submitted to Budget.</summary>
    public const string BudgetSubmitted = "BudgetSubmitted";
    /// <summary>Budget decision received.</summary>
    public const string BudgetDecisionReceived = "BudgetDecisionReceived";
    /// <summary>Security response received.</summary>
    public const string SecurityResponseReceived = "SecurityResponseReceived";
}
