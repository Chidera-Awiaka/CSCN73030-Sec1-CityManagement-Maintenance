namespace Maintenance.Api.Models;

/// <summary>
/// Allowed values shared by the API, the service and the tests.
/// </summary>
public static class InterventionRules
{
    public static readonly string[] Priorities =
    {
        "Low",
        "Medium",
        "High",
        "Critical"
    };

    public static readonly string[] Statuses =
    {
        "To Do",
        "Scheduled",
        "In Progress",
        "On Hold",
        "Completed",
        "Cancelled"
    };

    public const string InitialStatus = "To Do";

    public const string DefaultPriority = "Medium";

    public const string ScheduledStatus = "Scheduled";

    public const int MinimumTechnicianId = 1;

    /// <summary>Status given to an intervention when it is closed (MNT-08).</summary>
    public const string CompletedStatus = "Completed";

    /// <summary>Status of an intervention that was cancelled; it cannot be closed (MNT-08).</summary>
    public const string CancelledStatus = "Cancelled";
}