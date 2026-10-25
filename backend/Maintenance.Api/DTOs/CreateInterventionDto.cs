namespace Maintenance.Api.DTOs;

/// <summary>
/// Request body for creating a new intervention.
/// Related requirement: MNT-01.
/// </summary>
public class CreateInterventionDto
{
    /// <summary>Description of the problem. Required.</summary>
    public string Description { get; set; } = "";

    /// <summary>Address or location. Required.</summary>
    public string Location { get; set; } = "";

    /// <summary>Type of issue. Required.</summary>
    public string IssueType { get; set; } = "";

    /// <summary>Optional starting priority. Defaults to "Medium" when omitted.</summary>
    public string? Priority { get; set; }
}
