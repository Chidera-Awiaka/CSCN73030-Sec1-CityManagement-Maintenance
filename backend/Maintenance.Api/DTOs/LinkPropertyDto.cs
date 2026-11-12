namespace Maintenance.Api.DTOs;

/// <summary>Request body for linking a property to an intervention (REG-02).</summary>
public class LinkPropertyDto
{
    /// <summary>External Property ID from the Property Registry, e.g. "PR-1001".</summary>
    public string PropertyId { get; set; } = "";
}
