using Maintenance.Api.Integration;

namespace Maintenance.Api.Tests;

/// <summary>
/// Unit tests for the Property Registry search (REG-01).
/// Test cases map to TC-REG-01 in the Release Note.
/// </summary>
public sealed class PropertyRegistryTests
{
    private readonly MockPropertyRegistry _registry = new();

    /// <summary>Test: Search : By Property Id : Returns That Property.</summary>
[Fact]
    public void Search_ByPropertyId_ReturnsThatProperty()
    {
        var results = _registry.Search("PR-1003", null);

        var property = Assert.Single(results);
        Assert.Equal("PR-1003", property.PropertyId);
        Assert.Equal("45 Victoria Street, Kitchener", property.Address);
    }

    /// <summary>Test: Search : By Property Id : Is Case Insensitive And Trimmed.</summary>
[Fact]
    public void Search_ByPropertyId_IsCaseInsensitiveAndTrimmed()
    {
        var results = _registry.Search("  pr-1001 ", null);

        Assert.Equal("PR-1001", Assert.Single(results).PropertyId);
    }

    /// <summary>Test: Search : By Address Text : Returns All Matching.</summary>
[Fact]
    public void Search_ByAddressText_ReturnsAllMatching()
    {
        var results = _registry.Search(null, "king street");

        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.Contains("King Street", p.Address));
    }

    /// <summary>Test: Search : By Id And Address : Must Match Both.</summary>
[Fact]
    public void Search_ByIdAndAddress_MustMatchBoth()
    {
        Assert.Single(_registry.Search("PR-1001", "King"));
        Assert.Empty(_registry.Search("PR-1001", "Victoria"));
    }

    /// <summary>Test: Search : No Match : Returns Empty List.</summary>
[Fact]
    public void Search_NoMatch_ReturnsEmptyList()
    {
        Assert.Empty(_registry.Search(null, "Nowhere Road"));
        Assert.Empty(_registry.Search("PR-9999", null));
    }

    /// <summary>Test: Get By Id : Unknown Or Blank : Returns Null.</summary>
[Fact]
    public void GetById_UnknownOrBlank_ReturnsNull()
    {
        Assert.Null(_registry.GetById("PR-9999"));
        Assert.Null(_registry.GetById("  "));
    }

    /// <summary>Test: Search : Registry Unavailable : Throws.</summary>
[Fact]
    public void Search_RegistryUnavailable_Throws()
    {
        _registry.IsAvailable = false;

        Assert.Throws<PropertyRegistryUnavailableException>(() => _registry.Search("PR-1001", null));
        Assert.Throws<PropertyRegistryUnavailableException>(() => _registry.GetById("PR-1001"));
    }
}
