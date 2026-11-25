using Maintenance.Api.Models;

namespace Maintenance.Api.Integration;

/// <summary>
/// In-memory stand-in for the Property Registration service (REG-01, REG-02).
/// Holds a fixed list of Waterloo-region sample properties.
/// </summary>
public class MockPropertyRegistry : IPropertyRegistry
{
    private readonly List<Property> _properties;

    /// <summary>
    /// When false, every call throws <see cref="PropertyRegistryUnavailableException"/>.
    /// Used to test and demonstrate the "service unavailable" case of REG-01.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Creates the registry with the default sample data.</summary>
    public MockPropertyRegistry() : this(SampleData()) { }

    /// <summary>Creates the registry with the supplied properties (used by tests).</summary>
    /// <param name="properties">Properties the registry will return.</param>
    public MockPropertyRegistry(IEnumerable<Property> properties)
    {
        _properties = properties.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<Property> Search(string? propertyId, string? address)
    {
        EnsureAvailable();

        IEnumerable<Property> query = _properties;

        if (!string.IsNullOrWhiteSpace(propertyId))
        {
            query = query.Where(p =>
                string.Equals(p.PropertyId, propertyId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            query = query.Where(p =>
                p.Address.Contains(address.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(p => p.PropertyId).ToList();
    }

    /// <inheritdoc />
    public Property? GetById(string propertyId)
    {
        EnsureAvailable();

        if (string.IsNullOrWhiteSpace(propertyId))
        {
            return null;
        }

        return _properties.FirstOrDefault(p =>
            string.Equals(p.PropertyId, propertyId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public PropertyUtilities? GetUtilities(string propertyId)
    {
        var property = GetById(propertyId);
        if (property is null) return null;

        // Sample data: some utilities are intentionally missing to show the "not supplied" state.
        return property.PropertyId switch
        {
            "PR-1001" => new PropertyUtilities
            {
                PropertyId = property.PropertyId,
                Water = new UtilityInfo { Provider = "Region of Waterloo Water", ShutoffLocation = "Curb box, north side", Notes = "200 mm main" },
                Electricity = new UtilityInfo { Provider = "Enova Power", ShutoffLocation = "Basement panel room" },
                Gas = new UtilityInfo { Provider = "Enbridge Gas", ShutoffLocation = "East wall meter" }
            },
            "PR-1002" => new PropertyUtilities
            {
                PropertyId = property.PropertyId,
                Water = new UtilityInfo { Provider = "Region of Waterloo Water", ShutoffLocation = "Front yard" },
                Electricity = new UtilityInfo { Provider = "Enova Power" }
            },
            "PR-1004" => new PropertyUtilities
            {
                PropertyId = property.PropertyId,
                Electricity = new UtilityInfo { Provider = "Enova Power", ShutoffLocation = "Utility room B1" }
            },
            _ => new PropertyUtilities { PropertyId = property.PropertyId }
        };
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new PropertyRegistryUnavailableException(
                "The Property Registration service is currently unavailable.");
        }
    }

    private static List<Property> SampleData() => new()
    {
        new Property { PropertyId = "PR-1001", Address = "100 King Street North, Waterloo", PropertyType = "Commercial", Ward = "Ward 7" },
        new Property { PropertyId = "PR-1002", Address = "25 King Street South, Waterloo", PropertyType = "Residential", Ward = "Ward 7" },
        new Property { PropertyId = "PR-1003", Address = "45 Victoria Street, Kitchener", PropertyType = "Residential", Ward = "Ward 10" },
        new Property { PropertyId = "PR-1004", Address = "200 Erb Street West, Waterloo", PropertyType = "Municipal", Ward = "Ward 6" },
        new Property { PropertyId = "PR-1005", Address = "299 Doon Valley Drive, Kitchener", PropertyType = "Institutional", Ward = "Ward 4" }
    };
}
