using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Business logic for maintenance interventions.
/// Interventions are kept in memory and saved to a JSON file so they
/// survive a restart of the service.
/// Related requirements: MNT-01, MNT-02, MNT-03, MNT-07.
/// </summary>
public class InterventionService
{
    private readonly List<Intervention> _interventions;
    private readonly string? _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="filePath">
    /// JSON file used for persistent storage. When null, data is kept in memory only
    /// (used by unit tests). When the file does not exist yet, sample data is used.
    /// </param>
    public InterventionService(string? filePath = null)
    {
        _filePath = filePath;
        _interventions = Load() ?? SeedData();
    }

    /// <summary>Returns every intervention.</summary>
    public List<Intervention> GetAll()
    {
        lock (_lock)
        {
            return _interventions.ToList();
        }
    }

    /// <summary>Finds an intervention by its ID.</summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>The intervention, or null when it does not exist.</returns>
    public Intervention? GetById(int id)
    {
        lock (_lock)
        {
            return _interventions.FirstOrDefault(x => x.Id == id);
        }
    }

    /// <summary>
    /// Returns interventions matching the supplied filters (MNT-02).
    /// Any filter left empty is ignored. Matching is case-insensitive;
    /// location matches when it contains the supplied text.
    /// Results are sorted by priority (Critical first), then newest first.
    /// </summary>
    /// <param name="priority">Exact priority, e.g. "High".</param>
    /// <param name="location">Part of the location, e.g. "King".</param>
    /// <param name="status">Exact status, e.g. "To Do".</param>
    public List<Intervention> Filter(string? priority, string? location, string? status)
    {
        lock (_lock)
        {
            IEnumerable<Intervention> query = _interventions;

            if (!string.IsNullOrWhiteSpace(priority))
            {
                query = query.Where(x =>
                    string.Equals(x.Priority, priority.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(x =>
                    x.Location.Contains(location.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x =>
                    string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(x => Array.IndexOf(InterventionRules.Priorities, x.Priority))
                .ThenByDescending(x => x.CreatedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Checks a create request against the MNT-01 rules.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <returns>A list of error messages; empty when the request is valid.</returns>
    public static List<string> Validate(CreateInterventionDto? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("Description is required.");

        if (string.IsNullOrWhiteSpace(request.Location))
            errors.Add("Location is required.");

        if (string.IsNullOrWhiteSpace(request.IssueType))
            errors.Add("Issue type is required.");

        if (!string.IsNullOrWhiteSpace(request.Priority)
            && !InterventionRules.Priorities.Contains(request.Priority.Trim()))
            errors.Add("Priority must be Low, Medium, High or Critical.");

        return errors;
    }

    /// <summary>
    /// Creates a new intervention (MNT-01).
    /// A unique ID is generated and the status is always set to "To Do".
    /// </summary>
    /// <param name="request">A request that has passed <see cref="Validate"/>.</param>
    /// <returns>The created intervention.</returns>
    /// <exception cref="ArgumentException">Thrown when the request is invalid.</exception>
    public Intervention Create(CreateInterventionDto request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        lock (_lock)
        {
            var intervention = new Intervention
            {
                Id = _interventions.Count == 0 ? 1 : _interventions.Max(x => x.Id) + 1,
                Description = request.Description.Trim(),
                Location = request.Location.Trim(),
                IssueType = request.IssueType.Trim(),
                Priority = string.IsNullOrWhiteSpace(request.Priority)
                    ? InterventionRules.DefaultPriority
                    : request.Priority.Trim(),
                Status = InterventionRules.InitialStatus,
                CreatedAt = DateTime.UtcNow
            };

            _interventions.Add(intervention);
            Save();
            return intervention;
        }
    }

    /// <summary>Updates the priority of an intervention (MNT-03).</summary>
    /// <returns>False when the intervention does not exist.</returns>
    public bool UpdatePriority(int id, string priority)
    {
        lock (_lock)
        {
            var intervention = _interventions.FirstOrDefault(x => x.Id == id);
            if (intervention == null)
            {
                return false;
            }

            intervention.Priority = priority;
            Save();
            return true;
        }
    }

    /// <summary>Updates the operational status of an intervention (MNT-07).</summary>
    /// <returns>False when the intervention does not exist.</returns>
    public bool UpdateStatus(int id, string status)
    {
        lock (_lock)
        {
            var intervention = _interventions.FirstOrDefault(x => x.Id == id);
            if (intervention == null)
            {
                return false;
            }

            intervention.Status = status;
            Save();
            return true;
        }
    }

    private List<Intervention>? Load()
    {
        if (_filePath is null || !File.Exists(_filePath))
        {
            return null;
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<Intervention>>(json, JsonOptions);
    }

    private void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(_interventions, JsonOptions));
    }

    private static List<Intervention> SeedData() => new()
    {
        new Intervention
        {
            Id = 1,
            Description = "Water main break",
            Location = "King Street",
            IssueType = "Water",
            Priority = "High",
            Status = "To Do"
        },
        new Intervention
        {
            Id = 2,
            Description = "Road damage",
            Location = "Victoria Street",
            IssueType = "Road",
            Priority = "Medium",
            Status = "Scheduled"
        }
    };
}
