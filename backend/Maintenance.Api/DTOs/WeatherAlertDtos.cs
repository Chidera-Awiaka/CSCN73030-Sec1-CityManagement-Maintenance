namespace Maintenance.Api.DTOs;

/// <summary>Weather alert message sent by E-services (ESV-03). See docs/integration-contracts.md.</summary>
public class IncomingWeatherAlertDto
{
    /// <summary>Alert ID in E-services. Required.</summary>
    public string ExternalAlertId { get; set; } = "";
    /// <summary>Headline. Required.</summary>
    public string Title { get; set; } = "";
    /// <summary>Alert content. Optional (Title is used when empty).</summary>
    public string? Content { get; set; }
    /// <summary>Affected area. Optional.</summary>
    public string? AffectedArea { get; set; }
    /// <summary>Severity: Minor, Moderate, Severe or Extreme. Optional.</summary>
    public string? Severity { get; set; }
    /// <summary>Start of validity (UTC). Optional.</summary>
    public DateTime? ValidFrom { get; set; }
    /// <summary>End of validity (UTC). Optional.</summary>
    public DateTime? ValidTo { get; set; }
}

/// <summary>Assessment of a weather alert (ESV-04).</summary>
public class WeatherAssessmentDto
{
    /// <summary>Reviewer. Optional when the X-User-Id header is sent.</summary>
    public string? Reviewer { get; set; }
    /// <summary>Action Required, Monitor or No Impact. Required.</summary>
    public string Decision { get; set; } = "";
    /// <summary>Why. Required.</summary>
    public string Rationale { get; set; } = "";
}

/// <summary>Change of review state (ESV-04): back to "Awaiting Review" or to "Reviewed".</summary>
public class WeatherReviewStatusDto
{
    /// <summary>Awaiting Review or Reviewed.</summary>
    public string ReviewStatus { get; set; } = "";
}

/// <summary>An action taken from a weather alert (ESV-05).</summary>
public class WeatherAlertActionDto
{
    /// <summary>CreateIntervention, LinkInterventions, NoActionRequired, Reschedule or PutOnHold. Required.</summary>
    public string Type { get; set; } = "";
    /// <summary>LinkInterventions: the interventions to link.</summary>
    public List<int>? InterventionIds { get; set; }
    /// <summary>Reschedule / PutOnHold: the linked intervention.</summary>
    public int? InterventionId { get; set; }
    /// <summary>CreateIntervention: description (defaults to the alert title).</summary>
    public string? Description { get; set; }
    /// <summary>CreateIntervention: location (defaults to the affected area).</summary>
    public string? Location { get; set; }
    /// <summary>CreateIntervention: priority (defaults to Medium).</summary>
    public string? Priority { get; set; }
    /// <summary>Reschedule: new planned start.</summary>
    public DateTime? PlannedStartDate { get; set; }
    /// <summary>Reschedule: new planned end.</summary>
    public DateTime? PlannedEndDate { get; set; }
    /// <summary>Notes. Required for NoActionRequired.</summary>
    public string? Notes { get; set; }
}
