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
}