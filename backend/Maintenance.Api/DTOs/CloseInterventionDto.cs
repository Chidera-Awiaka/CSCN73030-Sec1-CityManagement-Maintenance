namespace Maintenance.Api.DTOs;

/// <summary>
/// Request body for closing an intervention (MNT-08).
/// </summary>
public class CloseInterventionDto
{
    /// <summary>Report describing the work performed. Required.</summary>
    public string CompletionReport { get; set; } = "";

    /// <summary>
    /// Actual completion date (UTC). Optional: defaults to now.
    /// Cannot be in the future or before the intervention was created.
    /// </summary>
    public DateTime? ActualCompletionDate { get; set; }

    /// <summary>Actual cost of the work, in dollars. Optional; cannot be negative.</summary>
    public decimal? ActualCost { get; set; }
}
