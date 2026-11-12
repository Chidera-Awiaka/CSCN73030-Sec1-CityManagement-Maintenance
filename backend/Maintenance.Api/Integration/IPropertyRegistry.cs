using Maintenance.Api.Models;

namespace Maintenance.Api.Integration;

/// <summary>
/// Thrown when the Property Registration service cannot be reached (REG-01).
/// The API turns this into HTTP 503 Service Unavailable.
/// </summary>
public class PropertyRegistryUnavailableException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Reason the registry is unavailable.</param>
    public PropertyRegistryUnavailableException(string message) : base(message) { }
}

/// <summary>
/// Integration boundary with the Property Registration service of the
/// City Management Enterprise System. Related requirements: REG-01, REG-02.
/// </summary>
/// <remarks>
/// Sprint 2 uses <see cref="MockPropertyRegistry"/> (allowed by REG-01 while the
/// Property Registration service is not available). When that service publishes its
/// REST API, a client implementing this interface replaces the mock in Program.cs;
/// the controllers and tests do not change.
/// </remarks>
public interface IPropertyRegistry
{
    /// <summary>
    /// Searches properties by Property ID and/or address (REG-01).
    /// </summary>
    /// <param name="propertyId">Exact Property ID (case-insensitive). Ignored when empty.</param>
    /// <param name="address">Text the address must contain (case-insensitive). Ignored when empty.</param>
    /// <returns>Matching properties; an empty list when nothing matches.</returns>
    /// <exception cref="PropertyRegistryUnavailableException">The registry cannot be reached.</exception>
    IReadOnlyList<Property> Search(string? propertyId, string? address);

    /// <summary>Gets one property by its Property ID (REG-02 validation).</summary>
    /// <param name="propertyId">Property ID to look up.</param>
    /// <returns>The property, or null when it does not exist.</returns>
    /// <exception cref="PropertyRegistryUnavailableException">The registry cannot be reached.</exception>
    Property? GetById(string propertyId);
}
