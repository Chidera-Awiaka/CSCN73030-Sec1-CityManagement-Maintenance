namespace Maintenance.Api.Integration;

/// <summary>A person supplied by the Staff service (STF-01).</summary>
public class StaffMember
{
    /// <summary>Personnel ID issued by Staff (used as the technician ID in MNT-05).</summary>
    public int PersonnelId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Specializations, e.g. Road, Water, Electrical.</summary>
    public List<string> Specializations { get; set; } = new();
}

/// <summary>Thrown when the Staff service cannot be reached.</summary>
public class StaffUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The message.</param>
    public StaffUnavailableException(string message) : base(message) { }
}

/// <summary>
/// Read-only access to personnel from the Staff service (STF-01). Employee creation and
/// editing stay in Staff; Maintenance only reads.
/// </summary>
public interface IStaffDirectory
{
    /// <summary>Lists personnel, optionally only those with a specialization.</summary>
    /// <param name="specialization">Optional specialization filter, e.g. Road.</param>
    /// <returns>Personnel ordered by name.</returns>
    /// <exception cref="StaffUnavailableException">Staff cannot be reached.</exception>
    IReadOnlyList<StaffMember> GetPersonnel(string? specialization = null);
}

/// <summary>
/// Mock Staff service (TEC-03). The IDs 101-103 match the technicians already used by MNT-05.
/// </summary>
public class MockStaffDirectory : IStaffDirectory
{
    private readonly List<StaffMember> _staff = new()
    {
        new StaffMember { PersonnelId = 101, Name = "Alex Technician", Specializations = { "Road", "Concrete" } },
        new StaffMember { PersonnelId = 102, Name = "Sam Technician", Specializations = { "Water", "Sewer" } },
        new StaffMember { PersonnelId = 103, Name = "Jordan Technician", Specializations = { "Electrical", "Streetlights" } },
        new StaffMember { PersonnelId = 104, Name = "Riley Technician", Specializations = { "Road", "Traffic Signals" } }
    };

    /// <summary>When false, calls fail as if Staff were down.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<StaffMember> GetPersonnel(string? specialization = null)
    {
        if (!IsAvailable) throw new StaffUnavailableException("The Staff service is currently unavailable.");

        return _staff
            .Where(s => string.IsNullOrWhiteSpace(specialization)
                || s.Specializations.Contains(specialization.Trim(), StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .ToList();
    }
}
