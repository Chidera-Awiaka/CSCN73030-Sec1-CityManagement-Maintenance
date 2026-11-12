using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Result of an incident-report operation: what happened, the report
/// involved (when there is one) and any validation errors.
/// </summary>
/// <param name="Result">Outcome of the operation.</param>
/// <param name="Report">The report received or reviewed, or null.</param>
/// <param name="Errors">Validation or conflict messages; empty on success.</param>
public record ReviewOutcome(ReviewResult Result, IncidentReport? Report, List<string> Errors)
{
    /// <summary>Creates a successful outcome.</summary>
    public static ReviewOutcome Ok(IncidentReport report) =>
        new(ReviewResult.Success, report, new List<string>());

    /// <summary>Creates a failed outcome with one or more messages.</summary>
    public static ReviewOutcome Fail(ReviewResult result, params string[] errors) =>
        new(result, null, errors.ToList());
}

/// <summary>
/// Receives incident reports from E-services and lets a maintenance
/// agent accept or reject them. Accepting a report creates a new
/// intervention through <see cref="InterventionService"/> (MNT-01).
/// Reports are saved to a JSON file so they survive a restart.
///
/// Related requirements:
/// ESV-01 - Receive incident reports from E-services
/// ESV-02 - Accept or reject a report
/// </summary>
public class IncidentReportService
{
    private readonly List<IncidentReport> _reports;
    private readonly InterventionService _interventions;
    private readonly string? _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="interventions">Used to create an intervention when a report is accepted.</param>
    /// <param name="filePath">
    /// JSON file used for persistent storage.
    /// When null, data is kept in memory only (used by unit tests).
    /// </param>
    public IncidentReportService(InterventionService interventions, string? filePath = null)
    {
        _interventions = interventions;
        _filePath = filePath;
        _reports = Load() ?? new List<IncidentReport>();
    }

    /// <summary>
    /// Lists incident reports, newest first.
    /// </summary>
    /// <param name="status">Optional review state filter: Pending, Accepted or Rejected.</param>
    public List<IncidentReport> GetAll(string? status = null)
    {
        lock (_lock)
        {
            IEnumerable<IncidentReport> query = _reports;

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x =>
                    string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderByDescending(x => x.ReceivedAt).ToList();
        }
    }

    /// <summary>Finds an incident report by its Maintenance ID.</summary>
    /// <returns>The report, or null when it does not exist.</returns>
    public IncidentReport? GetById(int id)
    {
        lock (_lock)
        {
            return _reports.FirstOrDefault(x => x.Id == id);
        }
    }

    /// <summary>
    /// Checks an incoming report against the ESV-01 contract.
    /// </summary>
    /// <param name="request">Message received from E-services.</param>
    /// <returns>Validation errors; empty when the message is valid.</returns>
    public static List<string> Validate(IncomingIncidentReportDto? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.ExternalReportId))
            errors.Add("External report ID is required.");

        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("Description is required.");

        if (string.IsNullOrWhiteSpace(request.Location))
            errors.Add("Location is required.");

        if (string.IsNullOrWhiteSpace(request.IssueType))
            errors.Add("Issue type is required.");

        if (request.ReportedAt is not null && request.ReportedAt > DateTime.UtcNow.AddMinutes(5))
            errors.Add("Reported date cannot be in the future.");

        return errors;
    }

    /// <summary>
    /// Receives an incident report from E-services (ESV-01).
    /// The report is stored with status Pending so an agent can review it.
    /// The same E-services report ID cannot be received twice.
    /// </summary>
    /// <param name="request">Message received from E-services.</param>
    /// <returns>
    /// Success with the stored report, Invalid with the validation errors,
    /// or Conflict when the report was already received.
    /// </returns>
    public ReviewOutcome Receive(IncomingIncidentReportDto request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new ReviewOutcome(ReviewResult.Invalid, null, errors);
        }

        lock (_lock)
        {
            var externalId = request.ExternalReportId.Trim();

            if (_reports.Any(x =>
                    string.Equals(x.ExternalReportId, externalId, StringComparison.OrdinalIgnoreCase)))
            {
                return ReviewOutcome.Fail(
                    ReviewResult.Conflict,
                    $"Report '{externalId}' was already received.");
            }

            var report = new IncidentReport
            {
                Id = _reports.Count == 0 ? 1 : _reports.Max(x => x.Id) + 1,
                ExternalReportId = externalId,
                Description = request.Description.Trim(),
                Location = request.Location.Trim(),
                IssueType = request.IssueType.Trim(),
                ReportedAt = request.ReportedAt ?? DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Status = IncidentReportRules.PendingStatus
            };

            _reports.Add(report);
            Save();
            return ReviewOutcome.Ok(report);
        }
    }

    /// <summary>
    /// Accepts a pending report (ESV-02). A new intervention is created
    /// from the report's description, location and issue type, and its
    /// ID is stored on the report for traceability.
    /// </summary>
    /// <param name="id">Incident report ID.</param>
    /// <param name="request">Reviewer name and optional priority.</param>
    /// <returns>
    /// Success with the updated report, Invalid, NotFound,
    /// or Conflict when the report was already reviewed.
    /// </returns>
    public ReviewOutcome Accept(int id, AcceptIncidentReportDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request?.ReviewedBy))
            errors.Add("Reviewer name is required.");

        if (!string.IsNullOrWhiteSpace(request?.Priority)
            && !InterventionRules.Priorities.Contains(request.Priority.Trim()))
            errors.Add("Priority must be Low, Medium, High or Critical.");

        if (errors.Count > 0)
        {
            return new ReviewOutcome(ReviewResult.Invalid, null, errors);
        }

        lock (_lock)
        {
            var report = _reports.FirstOrDefault(x => x.Id == id);
            if (report is null)
            {
                return ReviewOutcome.Fail(ReviewResult.NotFound, "Incident report not found.");
            }

            if (report.Status != IncidentReportRules.PendingStatus)
            {
                return ReviewOutcome.Fail(
                    ReviewResult.Conflict,
                    $"Report was already {report.Status.ToLowerInvariant()}.");
            }

            var intervention = _interventions.Create(new CreateInterventionDto
            {
                Description = report.Description,
                Location = report.Location,
                IssueType = report.IssueType,
                Priority = request!.Priority?.Trim()
            });

            report.Status = IncidentReportRules.AcceptedStatus;
            report.ReviewedBy = request.ReviewedBy.Trim();
            report.ReviewedAt = DateTime.UtcNow;
            report.InterventionId = intervention.Id;

            Save();
            return ReviewOutcome.Ok(report);
        }
    }

    /// <summary>
    /// Rejects a pending report with a reason (ESV-02).
    /// No intervention is created.
    /// </summary>
    /// <param name="id">Incident report ID.</param>
    /// <param name="request">Reviewer name and rejection reason.</param>
    /// <returns>
    /// Success with the updated report, Invalid, NotFound,
    /// or Conflict when the report was already reviewed.
    /// </returns>
    public ReviewOutcome Reject(int id, RejectIncidentReportDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request?.ReviewedBy))
            errors.Add("Reviewer name is required.");

        if (string.IsNullOrWhiteSpace(request?.Reason))
            errors.Add("A rejection reason is required.");

        if (errors.Count > 0)
        {
            return new ReviewOutcome(ReviewResult.Invalid, null, errors);
        }

        lock (_lock)
        {
            var report = _reports.FirstOrDefault(x => x.Id == id);
            if (report is null)
            {
                return ReviewOutcome.Fail(ReviewResult.NotFound, "Incident report not found.");
            }

            if (report.Status != IncidentReportRules.PendingStatus)
            {
                return ReviewOutcome.Fail(
                    ReviewResult.Conflict,
                    $"Report was already {report.Status.ToLowerInvariant()}.");
            }

            report.Status = IncidentReportRules.RejectedStatus;
            report.ReviewedBy = request!.ReviewedBy.Trim();
            report.ReviewedAt = DateTime.UtcNow;
            report.RejectionReason = request.Reason.Trim();

            Save();
            return ReviewOutcome.Ok(report);
        }
    }

    /// <summary>Loads reports from the JSON file, or null when there is none.</summary>
    private List<IncidentReport>? Load()
    {
        if (_filePath is null || !File.Exists(_filePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<IncidentReport>>(
            File.ReadAllText(_filePath), JsonOptions);
    }

    /// <summary>Saves reports to the JSON file (no-op in memory-only mode).</summary>
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

        File.WriteAllText(_filePath, JsonSerializer.Serialize(_reports, JsonOptions));
    }
}
