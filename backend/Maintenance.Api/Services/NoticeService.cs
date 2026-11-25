using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Sends notices about interventions to other services and records every delivery (TEC-04).
/// ESV-06 road closure / reopening to E-services; TRP-02 road closures and changes to Public Transport;
/// SEC-03 road closures and work-status updates to Security; REG-04 property-related service
/// disruptions to the Property Registry. Sending the same notice twice does not create a duplicate.
/// </summary>
public class NoticeService
{
    private readonly JsonListStore<OutboundNotice> _store;
    private readonly InterventionService _interventions;
    private readonly ExchangeService _exchanges;

    /// <summary>Creates the service.</summary>
    /// <param name="interventions">Intervention service.</param>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="filePath">JSON file used for storage, or null to keep data in memory (tests).</param>
    public NoticeService(InterventionService interventions, ExchangeService exchanges, string? filePath = null)
    {
        _interventions = interventions;
        _exchanges = exchanges;
        _store = new JsonListStore<OutboundNotice>(filePath);
    }

    /// <summary>Lists notices, newest first, with delivery status refreshed from the exchange log.</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <param name="kind">Optional notice kind: RoadClosure, WorkStatusUpdate or PropertyDisruption.</param>
    /// <returns>The matching records.</returns>
    public List<OutboundNotice> GetAll(int? interventionId = null, string? kind = null)
    {
        List<OutboundNotice> notices;
        lock (_store.Sync)
        {
            notices = _store.Items
                .Where(n => interventionId is null || n.InterventionId == interventionId)
                .Where(n => string.IsNullOrWhiteSpace(kind) || string.Equals(n.Kind, kind, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .ToList();
        }
        foreach (var notice in notices) Refresh(notice);
        return notices;
    }

    /// <summary>Sends a road closure / update / reopening (ESV-06, SEC-03, TRP-02).</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <param name="request">Closure status, location, dates and services to notify.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<OutboundNotice> SendRoadClosure(int interventionId, RoadClosureDto request, string actor)
    {
        if (request is null) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var intervention = _interventions.GetById(interventionId);
        if (intervention is null) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.NotFound, "Intervention not found.");

        var errors = new List<string>();
        var status = NoticeRules.ClosureStatuses.FirstOrDefault(s => s.Equals(request.ClosureStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null) errors.Add("Closure status must be Closed, Updated or Reopened.");
        var location = string.IsNullOrWhiteSpace(request.Location) ? intervention.Location : request.Location.Trim();
        if (string.IsNullOrWhiteSpace(location)) errors.Add("Location is required.");
        var start = request.PlannedStart ?? intervention.PlannedStartDate;
        var end = request.PlannedEnd ?? intervention.PlannedEndDate;
        if (start is null) errors.Add("Planned start date is required (schedule the intervention or supply it).");
        if (start is not null && end is not null && end < start) errors.Add("Planned end cannot be before planned start.");
        var services = Services(request.Services, NoticeRules.RoadClosureServices, errors);
        if (errors.Count > 0) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.Invalid, errors);

        var payload = new
        {
            interventionId,
            closureStatus = status,
            location,
            plannedStart = start,
            plannedEnd = end,
            interventionStatus = intervention.Status
        };
        var key = $"{interventionId}:{status}:{location}:{start:O}:{end:O}";
        var summary = $"{status} - {location}, {start:yyyy-MM-dd HH:mm}{(end is null ? "" : $" to {end:yyyy-MM-dd HH:mm}")}";
        return Send(NoticeRules.RoadClosure, "RoadClosureNotice", interventionId, key, summary, payload, services, actor);
    }

    /// <summary>Sends the intervention's current work status (SEC-03). Defaults to Security.</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <param name="request">Services to notify (optional).</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<OutboundNotice> SendWorkStatus(int interventionId, WorkStatusUpdateDto? request, string actor)
    {
        var intervention = _interventions.GetById(interventionId);
        if (intervention is null) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.NotFound, "Intervention not found.");

        var errors = new List<string>();
        var services = Services(request?.Services, new[] { ExternalServiceNames.Security }, errors);
        if (errors.Count > 0) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.Invalid, errors);

        var payload = new
        {
            interventionId,
            interventionStatus = intervention.Status,
            location = intervention.Location,
            plannedStart = intervention.PlannedStartDate,
            plannedEnd = intervention.PlannedEndDate
        };
        var key = $"{interventionId}:{intervention.Status}:{intervention.Location}:{intervention.PlannedStartDate:O}:{intervention.PlannedEndDate:O}";
        var summary = $"Status {intervention.Status} - {intervention.Location}";
        return Send(NoticeRules.WorkStatusUpdate, "WorkStatusUpdate", interventionId, key, summary, payload, services, actor);
    }

    /// <summary>
    /// Sends a property-related service disruption to the Property Registry (REG-04): property IDs,
    /// disruption type, applicable period and linked intervention; updates and restorations can be sent.
    /// </summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <param name="request">Disruption type, update type and period.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<OutboundNotice> SendPropertyDisruption(int interventionId, PropertyDisruptionDto request, string actor)
    {
        if (request is null) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var intervention = _interventions.GetById(interventionId);
        if (intervention is null) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.NotFound, "Intervention not found.");

        var errors = new List<string>();
        if (intervention.LinkedPropertyIds.Count == 0) errors.Add("Link at least one property to the intervention first (REG-02).");
        var type = NoticeRules.DisruptionTypes.FirstOrDefault(t => t.Equals(request.DisruptionType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (type is null) errors.Add("Disruption type must be Water, Electricity, Gas, Access or Other.");
        var update = string.IsNullOrWhiteSpace(request.UpdateType) ? "New"
            : NoticeRules.DisruptionUpdateTypes.FirstOrDefault(t => t.Equals(request.UpdateType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (update is null) errors.Add("Update type must be New, Update or Restored.");
        if (request.PeriodStart is null) errors.Add("Period start is required.");
        if (request.PeriodStart is not null && request.PeriodEnd is not null && request.PeriodEnd < request.PeriodStart)
            errors.Add("Period end cannot be before period start.");
        if (errors.Count > 0) return ServiceOutcome<OutboundNotice>.Fail(OutcomeKind.Invalid, errors);

        var payload = new
        {
            interventionId,
            propertyIds = intervention.LinkedPropertyIds,
            disruptionType = type,
            updateType = update,
            periodStart = request.PeriodStart,
            periodEnd = request.PeriodEnd,
            notes = request.Notes?.Trim()
        };
        var key = $"{interventionId}:{type}:{update}:{string.Join(",", intervention.LinkedPropertyIds)}:{request.PeriodStart:O}:{request.PeriodEnd:O}";
        var summary = $"{update} {type} disruption - {string.Join(", ", intervention.LinkedPropertyIds)}";
        return Send(NoticeRules.PropertyDisruption, "PropertyDisruption", interventionId, key, summary, payload,
            new List<string> { ExternalServiceNames.PropertyRegistration }, actor);
    }

    private static List<string> Services(List<string>? requested, IEnumerable<string> defaults, List<string> errors)
    {
        if (requested is null || requested.Count == 0) return defaults.ToList();
        var result = new List<string>();
        foreach (var name in requested.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = ExternalServiceNames.Outbound.FirstOrDefault(s => s.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) errors.Add($"Unknown service: {name}.");
            else result.Add(match);
        }
        if (result.Count == 0 && errors.Count == 0) errors.Add("At least one service is required.");
        return result;
    }

    private ServiceOutcome<OutboundNotice> Send(string kind, string operation, int interventionId, string key, string summary,
        object payload, List<string> services, string actor)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        lock (_store.Sync)
        {
            var existing = _store.Items.FirstOrDefault(n => n.Kind == kind && n.InterventionId == interventionId && n.PayloadJson == payloadJson
                && services.All(s => n.Deliveries.Any(d => d.Service == s)));
            if (existing is not null && existing.Deliveries.All(d => _exchanges.GetById(d.ExchangeId)?.Status == ExchangeRules.Succeeded))
            {
                Refresh(existing);
                return ServiceOutcome<OutboundNotice>.Ok(existing, duplicate: true);
            }
        }

        var deliveries = services.Select(service =>
        {
            var sent = _exchanges.Send(service, operation, $"{kind}:{key}", payloadJson, interventionId);
            return new NoticeDelivery
            {
                Service = service,
                ExchangeId = sent.Exchange.Id,
                Status = sent.Exchange.Status,
                Reference = sent.Exchange.ExternalReference,
                Error = sent.Exchange.LastError
            };
        }).ToList();

        lock (_store.Sync)
        {
            var notice = _store.Items.FirstOrDefault(n => n.Kind == kind && n.InterventionId == interventionId && n.PayloadJson == payloadJson);
            if (notice is null)
            {
                notice = new OutboundNotice
                {
                    Id = _store.NextId(n => n.Id),
                    Kind = kind,
                    InterventionId = interventionId,
                    Summary = summary,
                    PayloadJson = payloadJson,
                    SentBy = actor,
                    CreatedAt = DateTime.UtcNow
                };
                _store.Items.Add(notice);
            }
            foreach (var d in deliveries)
            {
                notice.Deliveries.RemoveAll(x => x.Service == d.Service);
                notice.Deliveries.Add(d);
            }
            _store.Save();
            return ServiceOutcome<OutboundNotice>.Ok(notice);
        }
    }

    private void Refresh(OutboundNotice notice)
    {
        foreach (var d in notice.Deliveries)
        {
            var exchange = _exchanges.GetById(d.ExchangeId);
            if (exchange is null) continue;
            d.Status = exchange.Status;
            d.Reference = exchange.ExternalReference;
            d.Error = exchange.LastError;
        }
    }
}
