namespace Maintenance.Api.Models;

/// <summary>Route change or diversion received from Public Transport (TRP-03).</summary>
public class RouteDiversion
{
    /// <summary>Maintenance's ID.</summary>
    public int Id { get; set; }

    /// <summary>Public Transport's reference (retained, used to detect repeats).</summary>
    public string ExternalReference { get; set; } = "";

    /// <summary>Affected route IDs.</summary>
    public List<string> AffectedRoutes { get; set; } = new();

    /// <summary>What changes.</summary>
    public string Description { get; set; } = "";

    /// <summary>Effective from (UTC).</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Effective until (UTC), when supplied.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>When Maintenance received it (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Interventions this change relates to.</summary>
    public List<int> LinkedInterventionIds { get; set; } = new();
}
