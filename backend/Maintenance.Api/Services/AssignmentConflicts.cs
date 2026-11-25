using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>An overlapping assignment (STF-02).</summary>
/// <param name="InterventionId">The other intervention.</param>
/// <param name="Description">Its description.</param>
/// <param name="PlannedStartDate">Its planned start.</param>
/// <param name="PlannedEndDate">Its planned end.</param>
public record AssignmentConflict(int InterventionId, string Description, DateTime PlannedStartDate, DateTime PlannedEndDate);

/// <summary>
/// Finds overlapping assignments recorded in Maintenance (STF-02). It only looks at Maintenance's
/// own assignments; it does not claim organization-wide availability.
/// </summary>
public static class AssignmentConflicts
{
    /// <summary>Note shown with every result (STF-02).</summary>
    public const string Scope =
        "Checked against assignments recorded in Maintenance only. Organization-wide availability is not known unless the Staff service supplies it.";

    /// <summary>
    /// Returns the other open interventions assigned to the same technician whose planned period
    /// overlaps this intervention's planned period.
    /// </summary>
    /// <param name="all">All interventions.</param>
    /// <param name="target">The intervention to check.</param>
    /// <returns>Overlaps; empty when there are none or when dates / technician are missing.</returns>
    public static List<AssignmentConflict> For(IEnumerable<Intervention> all, Intervention target)
    {
        if (target.AssignedTechnicianId is null || target.PlannedStartDate is null || target.PlannedEndDate is null)
            return new List<AssignmentConflict>();

        var start = target.PlannedStartDate.Value;
        var end = target.PlannedEndDate.Value;
        return all
            .Where(i => i.Id != target.Id
                && i.AssignedTechnicianId == target.AssignedTechnicianId
                && i.Status is not (InterventionRules.CompletedStatus or InterventionRules.CancelledStatus)
                && i.PlannedStartDate is not null && i.PlannedEndDate is not null
                && i.PlannedStartDate < end && start < i.PlannedEndDate)
            .OrderBy(i => i.PlannedStartDate)
            .Select(i => new AssignmentConflict(i.Id, i.Description, i.PlannedStartDate!.Value, i.PlannedEndDate!.Value))
            .ToList();
    }
}
