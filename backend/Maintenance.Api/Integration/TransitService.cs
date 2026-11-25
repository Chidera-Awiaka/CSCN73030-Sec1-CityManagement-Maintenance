namespace Maintenance.Api.Integration;

/// <summary>A route and its schedule supplied by Public Transport (TRP-01).</summary>
public class TransitRoute
{
    /// <summary>Route ID in Public Transport, e.g. "7".</summary>
    public string RouteId { get; set; } = "";

    /// <summary>Route name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Streets the route uses.</summary>
    public List<string> Streets { get; set; } = new();

    /// <summary>Schedule summary, e.g. "Every 10 min, 05:30-01:00".</summary>
    public string Schedule { get; set; } = "";

    /// <summary>When Public Transport last updated this route (UTC), when supplied.</summary>
    public DateTime? LastUpdated { get; set; }
}

/// <summary>Thrown when Public Transport cannot be reached.</summary>
public class TransitUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The message.</param>
    public TransitUnavailableException(string message) : base(message) { }
}

/// <summary>Query access to Public Transport routes and schedules (TRP-01).</summary>
public interface ITransitService
{
    /// <summary>Routes, optionally only those using a street that contains the given text.</summary>
    /// <param name="street">Optional text the street name must contain.</param>
    /// <returns>Matching routes with their schedules.</returns>
    /// <exception cref="TransitUnavailableException">Public Transport cannot be reached.</exception>
    IReadOnlyList<TransitRoute> GetRoutes(string? street = null);
}

/// <summary>Mock Public Transport service (TEC-03).</summary>
public class MockTransitService : ITransitService
{
    private static readonly DateTime Updated = new(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);

    private readonly List<TransitRoute> _routes = new()
    {
        new TransitRoute { RouteId = "7", Name = "Mainline", Streets = { "King Street North", "King Street South", "Victoria Street" }, Schedule = "Every 10 min, 05:30-01:00", LastUpdated = Updated },
        new TransitRoute { RouteId = "12", Name = "Erb-Bridgeport", Streets = { "Erb Street West", "Erb Street East" }, Schedule = "Every 15 min, 06:00-23:30", LastUpdated = Updated },
        new TransitRoute { RouteId = "204", Name = "iXpress Highland-Victoria", Streets = { "Victoria Street", "Highland Road" }, Schedule = "Every 12 min, 06:00-22:00", LastUpdated = null },
        new TransitRoute { RouteId = "301", Name = "ION Light Rail", Streets = { "King Street North", "Charles Street" }, Schedule = "Every 8 min, 05:00-01:30", LastUpdated = Updated }
    };

    /// <summary>When false, calls fail as if Public Transport were down.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<TransitRoute> GetRoutes(string? street = null)
    {
        if (!IsAvailable) throw new TransitUnavailableException("The Public Transport service is currently unavailable.");

        return _routes
            .Where(r => string.IsNullOrWhiteSpace(street)
                || r.Streets.Any(s => s.Contains(street.Trim(), StringComparison.OrdinalIgnoreCase)
                    || street.Trim().Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
