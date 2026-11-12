namespace Maintenance.Api.Models;

/// <summary>
/// Represents a maintenance intervention in the City Management system.
/// </summary>
public class Intervention
{
    public int Id { get; set; }

    public string Description { get; set; } = "";

    public string Location { get; set; } = "";

    public string IssueType { get; set; } = "";

    public string Priority { get; set; } =
        InterventionRules.DefaultPriority;

    public string Status { get; set; } =
        InterventionRules.InitialStatus;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    // MNT-04
    public DateTime? PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }

    // MNT-05
    public int? AssignedTechnicianId { get; set; }

    public string? AssignedTechnicianName { get; set; }
}