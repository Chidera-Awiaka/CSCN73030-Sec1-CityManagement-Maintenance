namespace Maintenance.Api.Models;

/// <summary>
/// A weather alert received from E-services (ESV-03), the manager's assessment of it
/// (ESV-04) and the actions taken from it (ESV-05).
/// Receiving an alert never creates interventions or changes priorities by itself.
/// </summary>
public class WeatherAlert
{
    /// <summary>Maintenance's ID for the alert.</summary>
    public int Id { get; set; }

    /// <summary>The alert's ID in E-services (used to detect a repeated delivery).</summary>
    public string ExternalAlertId { get; set; } = "";

    /// <summary>Alert headline, e.g. "Freezing rain warning".</summary>
    public string Title { get; set; } = "";

    /// <summary>Alert content as supplied by E-services.</summary>
    public string Content { get; set; } = "";

    /// <summary>Affected area, when provided.</summary>
    public string? AffectedArea { get; set; }

    /// <summary>Severity (Minor, Moderate, Severe, Extreme), when provided.</summary>
    public string? Severity { get; set; }

    /// <summary>Start of the validity period (UTC), when provided.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>End of the validity period (UTC), when provided.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>When Maintenance received the alert (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Review state: Awaiting Review or Reviewed (ESV-04).</summary>
    public string ReviewStatus { get; set; } = WeatherAlertRules.AwaitingReview;

    /// <summary>All assessments recorded for the alert, oldest first (ESV-04).</summary>
    public List<WeatherAssessment> Assessments { get; set; } = new();

    /// <summary>All actions taken from the alert, oldest first (ESV-05).</summary>
    public List<WeatherAlertAction> Actions { get; set; } = new();

    /// <summary>Interventions linked to the alert (created or existing) (ESV-05).</summary>
    public List<int> LinkedInterventionIds { get; set; } = new();
}

/// <summary>A manager's assessment of a weather alert (ESV-04).</summary>
public class WeatherAssessment
{
    /// <summary>Who assessed the alert.</summary>
    public string Reviewer { get; set; } = "";

    /// <summary>When it was assessed (UTC).</summary>
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Decision: Action Required, Monitor or No Impact.</summary>
    public string Decision { get; set; } = "";

    /// <summary>Why the decision was taken.</summary>
    public string Rationale { get; set; } = "";
}

/// <summary>An action taken from a weather alert (ESV-05). Kept on the alert for traceability.</summary>
public class WeatherAlertAction
{
    /// <summary>CreateIntervention, LinkInterventions, NoActionRequired, Reschedule or PutOnHold.</summary>
    public string Type { get; set; } = "";

    /// <summary>Interventions affected by the action.</summary>
    public List<int> InterventionIds { get; set; } = new();

    /// <summary>Notes entered by the manager, or a summary of the change.</summary>
    public string? Notes { get; set; }

    /// <summary>Who took the action.</summary>
    public string Actor { get; set; } = "";

    /// <summary>When the action was taken (UTC).</summary>
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Allowed values for weather alerts (ESV-03, ESV-04, ESV-05).</summary>
public static class WeatherAlertRules
{
    /// <summary>Alert not yet assessed (or reopened).</summary>
    public const string AwaitingReview = "Awaiting Review";
    /// <summary>Alert assessed.</summary>
    public const string Reviewed = "Reviewed";
    /// <summary>Allowed review states.</summary>
    public static readonly string[] ReviewStatuses = { AwaitingReview, Reviewed };

    /// <summary>Allowed severities (when provided).</summary>
    public static readonly string[] Severities = { "Minor", "Moderate", "Severe", "Extreme" };

    /// <summary>Allowed assessment decisions.</summary>
    public static readonly string[] Decisions = { "Action Required", "Monitor", "No Impact" };

    /// <summary>Create a new intervention from the alert.</summary>
    public const string CreateIntervention = "CreateIntervention";
    /// <summary>Link existing interventions to the alert.</summary>
    public const string LinkInterventions = "LinkInterventions";
    /// <summary>Record that no action is required.</summary>
    public const string NoActionRequired = "NoActionRequired";
    /// <summary>Reschedule a linked intervention.</summary>
    public const string Reschedule = "Reschedule";
    /// <summary>Put a linked intervention on hold.</summary>
    public const string PutOnHold = "PutOnHold";
    /// <summary>Allowed action types.</summary>
    public static readonly string[] ActionTypes = { CreateIntervention, LinkInterventions, NoActionRequired, Reschedule, PutOnHold };

    /// <summary>Issue type used for interventions created from a weather alert.</summary>
    public const string WeatherIssueType = "Weather";
}
