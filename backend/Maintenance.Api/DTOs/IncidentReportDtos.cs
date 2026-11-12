namespace Maintenance.Api.DTOs;

/// <summary>
/// Message sent by E-services to Maintenance for a new incident report.
/// This is the integration contract for ESV-01:
/// POST /api/incident-reports
/// </summary>
public class IncomingIncidentReportDto
{
    /// <summary>Report ID in the E-services system. Required and unique.</summary>
    public string ExternalReportId { get; set; } = "";

    /// <summary>What was reported. Required.</summary>
    public string Description { get; set; } = "";

    /// <summary>Address or location. Required.</summary>
    public string Location { get; set; } = "";

    /// <summary>Type of issue. Required.</summary>
    public string IssueType { get; set; } = "";

    /// <summary>When the citizen submitted the report. Defaults to the time received.</summary>
    public DateTime? ReportedAt { get; set; }
}

/// <summary>
/// Request body for accepting an incident report (ESV-02).
/// POST /api/incident-reports/{id}/accept
/// </summary>
public class AcceptIncidentReportDto
{
    /// <summary>Name of the maintenance agent accepting the report. Required.</summary>
    public string ReviewedBy { get; set; } = "";

    /// <summary>Priority for the new intervention. Defaults to Medium.</summary>
    public string? Priority { get; set; }
}

/// <summary>
/// Request body for rejecting an incident report (ESV-02).
/// POST /api/incident-reports/{id}/reject
/// </summary>
public class RejectIncidentReportDto
{
    /// <summary>Name of the maintenance agent rejecting the report. Required.</summary>
    public string ReviewedBy { get; set; } = "";

    /// <summary>Why the report was rejected, e.g. duplicate or not a city asset. Required.</summary>
    public string Reason { get; set; } = "";
}
