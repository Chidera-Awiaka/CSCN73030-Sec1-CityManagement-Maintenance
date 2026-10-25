namespace Maintenance.Api.Models;

/// <summary>
/// Allowed values shared by the API, the service and the tests.
/// </summary>
public static class InterventionRules
{
    /// <summary>Allowed priority values (MNT-03).</summary>
    public static readonly string[] Priorities = { "Low", "Medium", "High", "Critical" };

    /// <summary>Allowed operational statuses (MNT-07).</summary>
    public static readonly string[] Statuses =
        { "To Do", "Scheduled", "In Progress", "On Hold", "Completed", "Cancelled" };

    /// <summary>Status given to every newly created intervention (MNT-01).</summary>
    public const string InitialStatus = "To Do";

    /// <summary>Priority used when none is supplied on creation.</summary>
    public const string DefaultPriority = "Medium";
}
