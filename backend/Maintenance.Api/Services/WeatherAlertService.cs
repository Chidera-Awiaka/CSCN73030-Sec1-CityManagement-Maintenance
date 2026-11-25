using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Weather alerts from E-services.
/// ESV-03 - Receive weather alerts (stored with external ID, content, area, severity, validity; never
///          creates interventions or changes priorities automatically).
/// ESV-04 - Record an assessment (Awaiting Review / Reviewed; reviewer, date, decision, rationale).
/// ESV-05 - Take action (create intervention, link interventions, no action required, reschedule or
///          put linked work on hold); every decision stays attached to the alert.
/// </summary>
public class WeatherAlertService
{
    private readonly JsonListStore<WeatherAlert> _store;
    private readonly InterventionService _interventions;
    private readonly AuditService _audit;
    private readonly ExchangeService _exchanges;

    // Actor of the action being processed; only used while holding the store lock in TakeAction.
    private string _currentActor = RequestContext.UnknownActor;

    /// <summary>Creates the service.</summary>
    /// <param name="interventions">Intervention service.</param>
    /// <param name="audit">Audit history (TEC-05).</param>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="filePath">JSON file used for storage, or null to keep data in memory (tests).</param>
    public WeatherAlertService(InterventionService interventions, AuditService audit, ExchangeService exchanges, string? filePath = null)
    {
        _interventions = interventions;
        _audit = audit;
        _exchanges = exchanges;
        _store = new JsonListStore<WeatherAlert>(filePath);
    }

    /// <summary>Lists alerts, newest first, optionally by review state (ESV-03).</summary>
    /// <param name="reviewStatus">Awaiting Review or Reviewed.</param>
    /// <returns>The matching records.</returns>
    public List<WeatherAlert> GetAll(string? reviewStatus = null)
    {
        lock (_store.Sync)
        {
            return _store.Items
                .Where(a => string.IsNullOrWhiteSpace(reviewStatus)
                    || string.Equals(a.ReviewStatus, reviewStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.ReceivedAt).ThenByDescending(a => a.Id)
                .ToList();
        }
    }

    /// <summary>Gets one alert.</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <returns>The result.</returns>
    public WeatherAlert? GetById(int id)
    {
        lock (_store.Sync) { return _store.Items.FirstOrDefault(a => a.Id == id); }
    }

    /// <summary>Checks an incoming alert against the ESV-03 contract.</summary>
    /// <param name="request">Alert message sent by E-services.</param>
    /// <returns>The result.</returns>
    public static List<string> Validate(IncomingWeatherAlertDto? request)
    {
        var errors = new List<string>();
        if (request is null) { errors.Add("Request body is required."); return errors; }
        if (string.IsNullOrWhiteSpace(request.ExternalAlertId)) errors.Add("External alert ID is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Title is required.");
        if (!string.IsNullOrWhiteSpace(request.Severity)
            && !WeatherAlertRules.Severities.Contains(request.Severity.Trim(), StringComparer.OrdinalIgnoreCase))
            errors.Add("Severity must be Minor, Moderate, Severe or Extreme.");
        if (request.ValidFrom is not null && request.ValidTo is not null && request.ValidTo < request.ValidFrom)
            errors.Add("Validity end cannot be before validity start.");
        return errors;
    }

    /// <summary>
    /// Receives an alert (ESV-03). A repeated delivery of the same E-services alert ID returns the
    /// existing alert with Duplicate = true and creates nothing new (TEC-04).
    /// </summary>
    /// <param name="request">Alert message sent by E-services.</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<WeatherAlert> Receive(IncomingWeatherAlertDto request)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Invalid, errors);

        var externalId = request.ExternalAlertId.Trim();
        lock (_store.Sync)
        {
            var existing = _store.Items.FirstOrDefault(a => a.ExternalAlertId == externalId);
            if (existing is not null) return ServiceOutcome<WeatherAlert>.Ok(existing, duplicate: true);

            var severity = string.IsNullOrWhiteSpace(request.Severity)
                ? null
                : WeatherAlertRules.Severities.First(s => s.Equals(request.Severity.Trim(), StringComparison.OrdinalIgnoreCase));

            var alert = new WeatherAlert
            {
                Id = _store.NextId(a => a.Id),
                ExternalAlertId = externalId,
                Title = request.Title.Trim(),
                Content = string.IsNullOrWhiteSpace(request.Content) ? request.Title.Trim() : request.Content.Trim(),
                AffectedArea = string.IsNullOrWhiteSpace(request.AffectedArea) ? null : request.AffectedArea.Trim(),
                Severity = severity,
                ValidFrom = request.ValidFrom,
                ValidTo = request.ValidTo,
                ReceivedAt = DateTime.UtcNow,
                ReviewStatus = WeatherAlertRules.AwaitingReview
            };
            _store.Items.Add(alert);
            _store.Save();
            _exchanges.RecordInbound(ExternalServiceNames.EServices, "WeatherAlert", externalId, JsonSerializer.Serialize(request));
            return ServiceOutcome<WeatherAlert>.Ok(alert);
        }
    }

    /// <summary>Records an assessment and marks the alert Reviewed (ESV-04).</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="request">Decision, rationale and optional reviewer.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<WeatherAlert> Assess(int id, WeatherAssessmentDto request, string actor)
    {
        var reviewer = string.IsNullOrWhiteSpace(request?.Reviewer) ? actor : request!.Reviewer!.Trim();
        var errors = new List<string>();
        if (request is null) errors.Add("Request body is required.");
        else
        {
            if (!WeatherAlertRules.Decisions.Contains(request.Decision?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                errors.Add("Decision must be Action Required, Monitor or No Impact.");
            if (string.IsNullOrWhiteSpace(request.Rationale)) errors.Add("Rationale is required.");
            if (reviewer == RequestContext.UnknownActor) errors.Add("Reviewer is required.");
        }
        if (errors.Count > 0) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Invalid, errors);

        lock (_store.Sync)
        {
            var alert = _store.Items.FirstOrDefault(a => a.Id == id);
            if (alert is null) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.NotFound, "Weather alert not found.");

            var decision = WeatherAlertRules.Decisions.First(d => d.Equals((request!.Decision ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            alert.Assessments.Add(new WeatherAssessment
            {
                Reviewer = reviewer,
                ReviewedAt = DateTime.UtcNow,
                Decision = decision,
                Rationale = request!.Rationale.Trim()
            });
            alert.ReviewStatus = WeatherAlertRules.Reviewed;
            _store.Save();
            _audit.Record("WeatherAlert", alert.Id, AuditActions.WeatherAssessment, reviewer, $"{decision}: {request.Rationale.Trim()}");
            return ServiceOutcome<WeatherAlert>.Ok(alert);
        }
    }

    /// <summary>Sets the review state back to Awaiting Review (or Reviewed when an assessment exists) (ESV-04).</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="reviewStatus">Awaiting Review or Reviewed.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<WeatherAlert> SetReviewStatus(int id, string reviewStatus, string actor)
    {
        var status = WeatherAlertRules.ReviewStatuses.FirstOrDefault(s => s.Equals(reviewStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Invalid, "Review status must be Awaiting Review or Reviewed.");

        lock (_store.Sync)
        {
            var alert = _store.Items.FirstOrDefault(a => a.Id == id);
            if (alert is null) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.NotFound, "Weather alert not found.");
            if (status == WeatherAlertRules.Reviewed && alert.Assessments.Count == 0)
                return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Conflict, "Record an assessment before marking the alert Reviewed.");

            var previous = alert.ReviewStatus;
            alert.ReviewStatus = status;
            _store.Save();
            _audit.Record("WeatherAlert", alert.Id, AuditActions.WeatherAssessment, actor, $"Review status {previous} -> {status}");
            return ServiceOutcome<WeatherAlert>.Ok(alert);
        }
    }

    /// <summary>Takes an action from the alert (ESV-05). The decision is kept on the alert.</summary>
    /// <param name="id">Weather alert ID.</param>
    /// <param name="request">Action type and its details.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<WeatherAlert> TakeAction(int id, WeatherAlertActionDto request, string actor)
    {
        if (request is null) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var type = WeatherAlertRules.ActionTypes.FirstOrDefault(t => t.Equals(request.Type?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (type is null)
            return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.Invalid,
                "Type must be CreateIntervention, LinkInterventions, NoActionRequired, Reschedule or PutOnHold.");

        lock (_store.Sync)
        {
            var alert = _store.Items.FirstOrDefault(a => a.Id == id);
            if (alert is null) return ServiceOutcome<WeatherAlert>.Fail(OutcomeKind.NotFound, "Weather alert not found.");

            _currentActor = actor;
            var result = type switch
            {
                WeatherAlertRules.CreateIntervention => CreateFrom(alert, request),
                WeatherAlertRules.LinkInterventions => Link(alert, request),
                WeatherAlertRules.NoActionRequired => NoAction(request),
                WeatherAlertRules.Reschedule => Reschedule(alert, request),
                _ => PutOnHold(alert, request)
            };
            if (result.Error is not null) return ServiceOutcome<WeatherAlert>.Fail(result.Kind, result.Error);

            alert.Actions.Add(new WeatherAlertAction
            {
                Type = type,
                InterventionIds = result.Ids,
                Notes = result.Notes,
                Actor = actor,
                TakenAt = DateTime.UtcNow
            });
            foreach (var iid in result.Ids.Where(i => !alert.LinkedInterventionIds.Contains(i)))
                alert.LinkedInterventionIds.Add(iid);
            _store.Save();

            _audit.Record("WeatherAlert", alert.Id, AuditActions.WeatherAction, actor,
                $"{type}{(result.Ids.Count > 0 ? " [" + string.Join(", ", result.Ids) + "]" : "")}: {result.Notes}");
            return ServiceOutcome<WeatherAlert>.Ok(alert);
        }
    }

    private record ActionResult(List<int> Ids, string? Notes, OutcomeKind Kind = OutcomeKind.Success, string? Error = null)
    {
        public static ActionResult Fail(OutcomeKind kind, string error) => new(new List<int>(), null, kind, error);
    }

    private ActionResult CreateFrom(WeatherAlert alert, WeatherAlertActionDto request)
    {
        var dto = new CreateInterventionDto
        {
            Description = string.IsNullOrWhiteSpace(request.Description) ? $"Weather response: {alert.Title}" : request.Description.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? alert.AffectedArea ?? "" : request.Location.Trim(),
            IssueType = WeatherAlertRules.WeatherIssueType,
            Priority = request.Priority,
            Instructions = request.Notes
        };
        var errors = InterventionService.Validate(dto);
        if (errors.Count > 0) return ActionResult.Fail(OutcomeKind.Invalid, string.Join(" ", errors));

        var created = _interventions.Create(dto);
        return new ActionResult(new List<int> { created.Id }, request.Notes ?? $"Intervention #{created.Id} created.");
    }

    private ActionResult Link(WeatherAlert alert, WeatherAlertActionDto request)
    {
        var ids = (request.InterventionIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0) return ActionResult.Fail(OutcomeKind.Invalid, "At least one intervention ID is required.");
        var missing = ids.Where(i => _interventions.GetById(i) is null).ToList();
        if (missing.Count > 0) return ActionResult.Fail(OutcomeKind.NotFound, $"Intervention(s) not found: {string.Join(", ", missing)}.");
        return new ActionResult(ids, request.Notes ?? "Existing interventions linked.");
    }

    private static ActionResult NoAction(WeatherAlertActionDto request) =>
        string.IsNullOrWhiteSpace(request.Notes)
            ? ActionResult.Fail(OutcomeKind.Invalid, "Notes are required when recording that no action is required.")
            : new ActionResult(new List<int>(), request.Notes.Trim());

    private ActionResult Reschedule(WeatherAlert alert, WeatherAlertActionDto request)
    {
        if (LinkedTarget(alert, request) is { } error) return error;
        if (request.PlannedStartDate is null || request.PlannedEndDate is null)
            return ActionResult.Fail(OutcomeKind.Invalid, "Planned start and end dates are required.");
        if (request.PlannedEndDate < request.PlannedStartDate)
            return ActionResult.Fail(OutcomeKind.Invalid, "Planned end date cannot be before the start date.");

        var target = _interventions.GetById(request.InterventionId!.Value)!;
        var before = target.PlannedStartDate;
        var previousStatus = target.Status;
        _interventions.ScheduleIntervention(target.Id, request.PlannedStartDate.Value, request.PlannedEndDate.Value);
        _audit.Record("Intervention", target.Id, AuditActions.Scheduled, _currentActor,
            $"{request.PlannedStartDate:yyyy-MM-dd HH:mm} - {request.PlannedEndDate:yyyy-MM-dd HH:mm} (weather alert #{alert.Id})");
        if (previousStatus != InterventionRules.ScheduledStatus)
            _audit.Record("Intervention", target.Id, AuditActions.StatusChanged, _currentActor,
                $"{previousStatus} -> {InterventionRules.ScheduledStatus} (weather alert #{alert.Id})");
        return new ActionResult(new List<int> { target.Id },
            request.Notes ?? $"Rescheduled from {before:yyyy-MM-dd HH:mm} to {request.PlannedStartDate:yyyy-MM-dd HH:mm}.");
    }

    private ActionResult PutOnHold(WeatherAlert alert, WeatherAlertActionDto request)
    {
        if (LinkedTarget(alert, request) is { } error) return error;
        var target = _interventions.GetById(request.InterventionId!.Value)!;
        if (target.Status is InterventionRules.CompletedStatus or InterventionRules.CancelledStatus)
            return ActionResult.Fail(OutcomeKind.Conflict, $"Intervention {target.Id} is {target.Status} and cannot be put on hold.");

        var previous = target.Status;
        _interventions.UpdateStatus(target.Id, "On Hold");
        _audit.Record("Intervention", target.Id, AuditActions.StatusChanged, _currentActor,
            $"{previous} -> On Hold (weather alert #{alert.Id})");
        return new ActionResult(new List<int> { target.Id }, request.Notes ?? $"Status {previous} -> On Hold.");
    }

    private ActionResult? LinkedTarget(WeatherAlert alert, WeatherAlertActionDto request)
    {
        if (request.InterventionId is null) return ActionResult.Fail(OutcomeKind.Invalid, "Intervention ID is required.");
        if (_interventions.GetById(request.InterventionId.Value) is null)
            return ActionResult.Fail(OutcomeKind.NotFound, "Intervention not found.");
        if (!alert.LinkedInterventionIds.Contains(request.InterventionId.Value))
            return ActionResult.Fail(OutcomeKind.Conflict, "Only interventions linked to this alert can be changed from it. Link it first.");
        return null;
    }
}
