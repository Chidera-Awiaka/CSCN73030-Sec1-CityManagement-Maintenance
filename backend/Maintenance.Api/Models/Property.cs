namespace Maintenance.Api.Models;

/// <summary>
/// A property returned by the Property Registry.
/// Related requirements: REG-01 (search) and REG-02 (link to an intervention).
/// </summary>
/// <remarks>
/// The <see cref="PropertyId"/> is owned by the external Property Registration service.
/// The Maintenance service only stores this ID on an intervention; it never edits property data.
/// </remarks>
public class Property
{
    /// <summary>External Property ID issued by the Property Registry, e.g. "PR-1001" (REG-01).</summary>
    public string PropertyId { get; set; } = "";

    /// <summary>Civic address of the property (REG-01).</summary>
    public string Address { get; set; } = "";

    /// <summary>Type of property, e.g. Residential, Commercial, Municipal.</summary>
    public string PropertyType { get; set; } = "";

    /// <summary>City ward the property belongs to.</summary>
    public string Ward { get; set; } = "";
}
