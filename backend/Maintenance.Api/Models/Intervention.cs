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

    /// <summary>
    /// External Property IDs linked to this intervention (REG-02).
    /// Only the ID is stored; property details stay in the Property Registry.
    /// </summary>
    public List<string> LinkedPropertyIds { get; set; } = new();

    /// <summary>Work instructions for the technician (MNT-06). Optional.</summary>
    public string? Instructions { get; set; }

    /// <summary>Report of the work performed, required to close the intervention (MNT-08).</summary>
    public string? CompletionReport { get; set; }

    /// <summary>Date and time (UTC) the work was actually completed (MNT-08).</summary>
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>Actual cost of the work in dollars (MNT-08). Optional.</summary>
    public decimal? ActualCost { get; set; }
}