using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Business logic for maintenance interventions.
/// Interventions are kept in memory and saved to a JSON file so they
/// survive a restart of the service.
///
/// Related requirements:
/// MNT-01 - Create an intervention
/// MNT-02 - View and filter interventions
/// MNT-03 - Prioritize interventions
/// MNT-04 - Schedule an intervention
/// MNT-05 - Assign technicians or teams
/// MNT-07 - Update intervention operational status
/// </summary>
public class InterventionService
{
    private readonly List<Intervention> _interventions;
    private readonly string? _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="filePath">
    /// JSON file used for persistent storage.
    /// When null, data is kept in memory only.
    /// When the file does not exist, sample data is used.
    /// </param>
    public InterventionService(string? filePath = null)
    {
        _filePath = filePath;
        _interventions = Load() ?? SeedData();
    }

    /// <summary>
    /// Returns every intervention.
    /// </summary>
    public List<Intervention> GetAll()
    {
        lock (_lock)
        {
            return _interventions.ToList();
        }
    }

    /// <summary>
    /// Finds an intervention by ID.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <returns>
    /// The matching intervention, or null when it does not exist.
    /// </returns>
    public Intervention? GetById(int id)
    {
        lock (_lock)
        {
            return _interventions.FirstOrDefault(x => x.Id == id);
        }
    }

    /// <summary>
    /// Returns interventions matching the supplied filters (MNT-02).
    /// Any empty filter is ignored.
    ///
    /// Matching is case-insensitive.
    /// Location matches when it contains the supplied text.
    ///
    /// Results are sorted by priority and then creation date.
    /// </summary>
    /// <param name="priority">Priority such as High.</param>
    /// <param name="location">Part of the location.</param>
    /// <param name="status">Operational status.</param>
    public List<Intervention> Filter(
        string? priority,
        string? location,
        string? status)
    {
        lock (_lock)
        {
            IEnumerable<Intervention> query = _interventions;

            if (!string.IsNullOrWhiteSpace(priority))
            {
                query = query.Where(x =>
                    string.Equals(
                        x.Priority,
                        priority.Trim(),
                        StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(x =>
                    x.Location.Contains(
                        location.Trim(),
                        StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x =>
                    string.Equals(
                        x.Status,
                        status.Trim(),
                        StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(x =>
                    Array.IndexOf(
                        InterventionRules.Priorities,
                        x.Priority))
                .ThenByDescending(x => x.CreatedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Checks a create request against the MNT-01 rules.
    /// </summary>
    /// <param name="request">Create request.</param>
    /// <returns>
    /// List of validation errors.
    /// Empty when the request is valid.
    /// </returns>
    public static List<string> Validate(
        CreateInterventionDto? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Location))
        {
            errors.Add("Location is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            errors.Add("Issue type is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Priority)
            && !InterventionRules.Priorities.Contains(
                request.Priority.Trim()))
        {
            errors.Add(
                "Priority must be Low, Medium, High or Critical.");
        }

        return errors;
    }

    /// <summary>
    /// Creates a new intervention (MNT-01).
    /// A unique ID is generated.
    /// Initial status is always To Do.
    /// </summary>
    /// <param name="request">
    /// Request containing intervention information.
    /// </param>
    /// <returns>The created intervention.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when validation fails.
    /// </exception>
    public Intervention Create(
        CreateInterventionDto request)
    {
        var errors = Validate(request);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(" ", errors));
        }

        lock (_lock)
        {
            var intervention = new Intervention
            {
                Id = _interventions.Count == 0
                    ? 1
                    : _interventions.Max(x => x.Id) + 1,

                Description = request.Description.Trim(),

                Location = request.Location.Trim(),

                IssueType = request.IssueType.Trim(),

                Priority =
                    string.IsNullOrWhiteSpace(request.Priority)
                        ? InterventionRules.DefaultPriority
                        : request.Priority.Trim(),

                Status = InterventionRules.InitialStatus,

                CreatedAt = DateTime.UtcNow,

                Instructions =
                    string.IsNullOrWhiteSpace(request.Instructions)
                        ? null
                        : request.Instructions.Trim()
            };

            _interventions.Add(intervention);

            Save();

            return intervention;
        }
    }

    /// <summary>
    /// Updates the priority of an intervention (MNT-03).
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="priority">New priority.</param>
    /// <returns>
    /// False when the intervention does not exist.
    /// </returns>
    public bool UpdatePriority(
        int id,
        string priority)
    {
        lock (_lock)
        {
            var intervention =
                _interventions.FirstOrDefault(
                    x => x.Id == id);

            if (intervention == null)
            {
                return false;
            }

            intervention.Priority = priority;

            Save();

            return true;
        }
    }

    /// <summary>
    /// Updates the operational status of an intervention (MNT-07).
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="status">New operational status.</param>
    /// <returns>
    /// False when the intervention does not exist.
    /// </returns>
    public bool UpdateStatus(
        int id,
        string status)
    {
        lock (_lock)
        {
            var intervention =
                _interventions.FirstOrDefault(
                    x => x.Id == id);

            if (intervention == null)
            {
                return false;
            }

            intervention.Status = status;

            Save();

            return true;
        }
    }

    /// <summary>
    /// Schedules an intervention (MNT-04).
    /// Stores the planned start/end dates and changes
    /// the intervention status to Scheduled.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="startDate">Planned start date.</param>
    /// <param name="endDate">Planned end date.</param>
    /// <returns>
    /// False when the intervention does not exist.
    /// </returns>
    public bool ScheduleIntervention(
        int id,
        DateTime startDate,
        DateTime endDate)
    {
        lock (_lock)
        {
            var intervention =
                _interventions.FirstOrDefault(
                    x => x.Id == id);

            if (intervention == null)
            {
                return false;
            }

            intervention.PlannedStartDate = startDate;

            intervention.PlannedEndDate = endDate;

            intervention.Status =
                InterventionRules.ScheduledStatus;

            Save();

            return true;
        }
    }

    /// <summary>
    /// Assigns a technician to an intervention (MNT-05).
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="technicianId">
    /// Technician ID supplied by the Staff service.
    /// </param>
    /// <param name="technicianName">
    /// Technician display name.
    /// </param>
    /// <returns>
    /// False when the intervention does not exist.
    /// </returns>
    public bool AssignTechnician(
        int id,
        int technicianId,
        string technicianName)
    {
        lock (_lock)
        {
            var intervention =
                _interventions.FirstOrDefault(
                    x => x.Id == id);

            if (intervention == null)
            {
                return false;
            }

            intervention.AssignedTechnicianId =
                technicianId;

            intervention.AssignedTechnicianName =
                technicianName.Trim();

            Save();

            return true;
        }
    }

    /// <summary>
    /// Loads interventions from JSON storage.
    /// </summary>
    private List<Intervention>? Load()
    {
        if (_filePath is null
            || !File.Exists(_filePath))
        {
            return null;
        }

        var json =
            File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<
            List<Intervention>>(
                json,
                JsonOptions);
    }

    /// <summary>
    /// Links an external property to an intervention (REG-02).
    /// The caller must first confirm the property exists in the Property Registry.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="propertyId">External Property ID, stored as supplied by the registry.</param>
    /// <returns>Success, InterventionNotFound or AlreadyLinked.</returns>
    public PropertyLinkResult LinkProperty(int id, string propertyId)
    {
        lock (_lock)
        {
            var intervention = _interventions.FirstOrDefault(x => x.Id == id);
            if (intervention == null)
            {
                return PropertyLinkResult.InterventionNotFound;
            }

            if (intervention.LinkedPropertyIds.Contains(propertyId, StringComparer.OrdinalIgnoreCase))
            {
                return PropertyLinkResult.AlreadyLinked;
            }

            intervention.LinkedPropertyIds.Add(propertyId);
            Save();
            return PropertyLinkResult.Success;
        }
    }

    /// <summary>Removes a property link from an intervention (REG-02).</summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="propertyId">External Property ID to remove.</param>
    /// <returns>Success, InterventionNotFound or NotLinked.</returns>
    public PropertyLinkResult UnlinkProperty(int id, string propertyId)
    {
        lock (_lock)
        {
            var intervention = _interventions.FirstOrDefault(x => x.Id == id);
            if (intervention == null)
            {
                return PropertyLinkResult.InterventionNotFound;
            }

            int removed = intervention.LinkedPropertyIds.RemoveAll(x =>
                string.Equals(x, propertyId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return PropertyLinkResult.NotLinked;
            }

            Save();
            return PropertyLinkResult.Success;
        }
    }

    /// <summary>
    /// Returns the interventions assigned to one technician (MNT-06).
    /// Only that technician's assignments are returned, earliest planned start first
    /// (unscheduled work last), then highest priority.
    /// </summary>
    /// <param name="technicianId">Technician ID (as assigned in MNT-05).</param>
    /// <param name="includeClosed">When false, Completed and Cancelled interventions are hidden.</param>
    /// <returns>The technician's interventions; empty when none are assigned.</returns>
    public List<Intervention> GetAssignedTo(int technicianId, bool includeClosed = false)
    {
        lock (_lock)
        {
            return _interventions
                .Where(x => x.AssignedTechnicianId == technicianId)
                .Where(x => includeClosed
                    || (x.Status != InterventionRules.CompletedStatus
                        && x.Status != InterventionRules.CancelledStatus))
                .OrderBy(x => x.PlannedStartDate ?? DateTime.MaxValue)
                .ThenByDescending(x => Array.IndexOf(InterventionRules.Priorities, x.Priority))
                .ToList();
        }
    }

    /// <summary>
    /// Checks a close request against the MNT-08 rules.
    /// </summary>
    /// <param name="request">Close request.</param>
    /// <param name="createdAt">When the intervention was created.</param>
    /// <param name="now">Current time (UTC); a parameter so tests can fix it.</param>
    /// <returns>List of validation errors; empty when valid.</returns>
    public static List<string> ValidateClose(CloseInterventionDto? request, DateTime createdAt, DateTime now)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.CompletionReport))
        {
            errors.Add("A completion report is required.");
        }

        if (request.ActualCompletionDate is { } done)
        {
            if (done > now.AddMinutes(5))
            {
                errors.Add("Actual completion date cannot be in the future.");
            }

            if (done < createdAt)
            {
                errors.Add("Actual completion date cannot be before the intervention was created.");
            }
        }

        if (request.ActualCost is < 0)
        {
            errors.Add("Actual cost cannot be negative.");
        }

        return errors;
    }

    /// <summary>
    /// Closes an intervention (MNT-08): records the completion report, the actual
    /// completion date (now when not supplied) and the optional actual cost, and sets
    /// the status to Completed. Call <see cref="ValidateClose"/> first.
    /// </summary>
    /// <param name="id">Intervention ID.</param>
    /// <param name="request">Validated close request.</param>
    /// <returns>Closed, NotFound or AlreadyClosed.</returns>
    public CloseResult CloseIntervention(int id, CloseInterventionDto request)
    {
        lock (_lock)
        {
            var intervention = _interventions.FirstOrDefault(x => x.Id == id);
            if (intervention == null)
            {
                return CloseResult.NotFound;
            }

            if (intervention.Status == InterventionRules.CompletedStatus
                || intervention.Status == InterventionRules.CancelledStatus)
            {
                return CloseResult.AlreadyClosed;
            }

            intervention.CompletionReport = request.CompletionReport.Trim();
            intervention.ActualCompletionDate = request.ActualCompletionDate ?? DateTime.UtcNow;
            intervention.ActualCost = request.ActualCost;
            intervention.Status = InterventionRules.CompletedStatus;

            Save();
            return CloseResult.Closed;
        }
    }

    /// <summary>
    /// Saves interventions to JSON storage.
    /// </summary>
    private void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        var folder =
            Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json =
            JsonSerializer.Serialize(
                _interventions,
                JsonOptions);

        File.WriteAllText(
            _filePath,
            json);
    }

    /// <summary>
    /// Creates initial sample data when no JSON data exists.
    /// </summary>
    private static List<Intervention> SeedData()
        => new()
        {
            new Intervention
            {
                Id = 1,
                Description = "Water main break",
                Location = "King Street",
                IssueType = "Water",
                Priority = "High",
                Status = InterventionRules.InitialStatus,
                CreatedAt = DateTime.UtcNow
            },

            new Intervention
            {
                Id = 2,
                Description = "Road damage",
                Location = "Victoria Street",
                IssueType = "Road",
                Priority = "Medium",
                Status = InterventionRules.ScheduledStatus,

                PlannedStartDate =
                    DateTime.UtcNow.AddDays(1),

                PlannedEndDate =
                    DateTime.UtcNow
                        .AddDays(1)
                        .AddHours(4),

                CreatedAt = DateTime.UtcNow
            }
        };
}