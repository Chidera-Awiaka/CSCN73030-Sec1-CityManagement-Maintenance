namespace Maintenance.Api.Models;

/// <summary>
/// An incident report received from the E-services team
/// (for example a citizen reporting a pothole or a broken streetlight).
///
/// Related requirements:
/// ESV-01 - Receive incident reports from E-services
/// ESV-02 - Accept or reject a report
/// </summary>
public class IncidentReport
{
    /// <summary>Maintenance's own ID for the report.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The report's ID in the E-services system.
    /// Used to detect a report that was sent twice.
    /// </summary>
    public string ExternalReportId { get; set; } = "";

    /// <summary>What the citizen reported.</summary>
    public string Description { get; set; } = "";

    /// <summary>Address or location of the problem.</summary>
    public string Location { get; set; } = "";

    /// <summary>Type of issue, e.g. Road, Water, Electrical.</summary>
    public string IssueType { get; set; } = "";

    /// <summary>When the citizen submitted the report in E-services (UTC).</summary>
    public DateTime ReportedAt { get; set; }

    /// <summary>When Maintenance received the report (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Review state: Pending, Accepted or Rejected.</summary>
    public string Status { get; set; } = IncidentReportRules.PendingStatus;

    /// <summary>Maintenance agent who accepted or rejected the report.</summary>
    public string? ReviewedBy { get; set; }

    /// <summary>When the report was accepted or rejected (UTC).</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reason given when the report was rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// ID of the intervention created when the report was accepted.
    /// Null while pending or when rejected.
    /// </summary>
    public int? InterventionId { get; set; }
}

/// <summary>
/// Allowed review states for incident reports (ESV-01, ESV-02).
/// </summary>
public static class IncidentReportRules
{
    /// <summary>Report received and waiting for an agent to review it.</summary>
    public const string PendingStatus = "Pending";

    /// <summary>Report accepted; an intervention was created from it.</summary>
    public const string AcceptedStatus = "Accepted";

    /// <summary>Report rejected with a reason.</summary>
    public const string RejectedStatus = "Rejected";

    /// <summary>All allowed review states.</summary>
    public static readonly string[] Statuses =
        { PendingStatus, AcceptedStatus, RejectedStatus };
}
