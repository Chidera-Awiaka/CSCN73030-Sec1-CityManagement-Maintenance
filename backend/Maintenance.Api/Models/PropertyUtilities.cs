namespace Maintenance.Api.Models;

/// <summary>
/// Utility information supplied by the Property Registry for one property (REG-03).
/// A null utility means the registry did not supply it.
/// </summary>
public class PropertyUtilities
{
    /// <summary>Property ID.</summary>
    public string PropertyId { get; set; } = "";

    /// <summary>Water service, or null when not supplied.</summary>
    public UtilityInfo? Water { get; set; }

    /// <summary>Electricity service, or null when not supplied.</summary>
    public UtilityInfo? Electricity { get; set; }

    /// <summary>Gas service, or null when not supplied.</summary>
    public UtilityInfo? Gas { get; set; }

    /// <summary>Names of the utilities the registry did not supply (REG-03: missing data explicitly identified).</summary>
    public List<string> MissingUtilities =>
        new[] { ("Water", Water), ("Electricity", Electricity), ("Gas", Gas) }
            .Where(x => x.Item2 is null)
            .Select(x => x.Item1)
            .ToList();
}

/// <summary>One utility service at a property (REG-03).</summary>
public class UtilityInfo
{
    /// <summary>Utility provider.</summary>
    public string Provider { get; set; } = "";

    /// <summary>Where the shut-off is, when supplied.</summary>
    public string? ShutoffLocation { get; set; }

    /// <summary>Extra notes, when supplied.</summary>
    public string? Notes { get; set; }
}
