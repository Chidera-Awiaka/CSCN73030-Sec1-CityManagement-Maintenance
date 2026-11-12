namespace Maintenance.Api.Models;

/// <summary>Outcome of linking or unlinking a property (REG-02).</summary>
public enum PropertyLinkResult
{
    /// <summary>The link was added or removed.</summary>
    Success,

    /// <summary>The intervention does not exist.</summary>
    InterventionNotFound,

    /// <summary>The property is already linked to the intervention (link request).</summary>
    AlreadyLinked,

    /// <summary>The property is not linked to the intervention (unlink request).</summary>
    NotLinked
}
