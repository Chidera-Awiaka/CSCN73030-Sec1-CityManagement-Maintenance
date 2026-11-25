using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Route changes and diversions received from Public Transport (TRP-03): external references
/// retained, affected routes and effective dates shown, and they can be linked to interventions.
/// Only the Public Transport service can send them; a repeated delivery is ignored (TEC-04).
/// </summary>
public class TransitDiversionService
{
    private readonly JsonListStore<RouteDiversion> _store;
    private readonly InterventionService _interventions;
    private readonly ExchangeService _exchanges;

    /// <summary>Creates the service.</summary>
    /// <param name="interventions">Intervention service.</param>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="filePath">JSON file used for storage, or null to keep data in memory (tests).</param>
    public TransitDiversionService(InterventionService interventions, ExchangeService exchanges, string? filePath = null)
    {
        _interventions = interventions;
        _exchanges = exchanges;
        _store = new JsonListStore<RouteDiversion>(filePath);
    }

    /// <summary>Lists diversions, newest effective date first, optionally those linked to an intervention.</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The matching records.</returns>
    public List<RouteDiversion> GetAll(int? interventionId = null)
    {
        lock (_store.Sync)
        {
            return _store.Items.Where(d => interventionId is null || d.LinkedInterventionIds.Contains(interventionId.Value))
                .OrderByDescending(d => d.EffectiveFrom).ThenByDescending(d => d.Id).ToList();
        }
    }

    /// <summary>Receives a route change / diversion (TRP-03).</summary>
    /// <param name="request">Route-change message sent by Public Transport.</param>
    /// <param name="sourceService">Value of the X-Source-Service header (the calling service).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<RouteDiversion> Receive(RouteDiversionDto request, string? sourceService)
    {
        if (!string.Equals(sourceService, ExternalServiceNames.PublicTransport, StringComparison.OrdinalIgnoreCase))
            return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.Forbidden, "Only the Public Transport service can send route changes (X-Source-Service: Public Transport).");
        if (request is null) return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.Invalid, "Request body is required.");

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ExternalReference)) errors.Add("External reference is required.");
        var routes = (request.AffectedRoutes ?? new List<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
        if (routes.Count == 0) errors.Add("At least one affected route is required.");
        if (string.IsNullOrWhiteSpace(request.Description)) errors.Add("Description is required.");
        if (request.EffectiveFrom is null) errors.Add("Effective-from date is required.");
        if (request.EffectiveFrom is not null && request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
            errors.Add("Effective-to cannot be before effective-from.");
        if (errors.Count > 0) return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.Invalid, errors);

        var reference = request.ExternalReference.Trim();
        var (_, duplicate) = _exchanges.RecordInbound(ExternalServiceNames.PublicTransport, "RouteDiversion", reference, JsonSerializer.Serialize(request));
        lock (_store.Sync)
        {
            var existing = _store.Items.FirstOrDefault(d => d.ExternalReference == reference);
            if (existing is not null) return ServiceOutcome<RouteDiversion>.Ok(existing, duplicate: true);

            var diversion = new RouteDiversion
            {
                Id = _store.NextId(d => d.Id),
                ExternalReference = reference,
                AffectedRoutes = routes,
                Description = request.Description.Trim(),
                EffectiveFrom = request.EffectiveFrom!.Value,
                EffectiveTo = request.EffectiveTo,
                ReceivedAt = DateTime.UtcNow
            };
            _store.Items.Add(diversion);
            _store.Save();
            return ServiceOutcome<RouteDiversion>.Ok(diversion);
        }
    }

    /// <summary>Links a diversion to an intervention (TRP-03).</summary>
    /// <param name="id">Route change ID.</param>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<RouteDiversion> Link(int id, int interventionId)
    {
        if (_interventions.GetById(interventionId) is null)
            return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.NotFound, "Intervention not found.");
        lock (_store.Sync)
        {
            var d = _store.Items.FirstOrDefault(x => x.Id == id);
            if (d is null) return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.NotFound, "Route change not found.");
            if (d.LinkedInterventionIds.Contains(interventionId))
                return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.Conflict, "Already linked to this intervention.");
            d.LinkedInterventionIds.Add(interventionId);
            _store.Save();
            return ServiceOutcome<RouteDiversion>.Ok(d);
        }
    }

    /// <summary>Removes a link (TRP-03).</summary>
    /// <param name="id">Route change ID.</param>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<RouteDiversion> Unlink(int id, int interventionId)
    {
        lock (_store.Sync)
        {
            var d = _store.Items.FirstOrDefault(x => x.Id == id);
            if (d is null) return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.NotFound, "Route change not found.");
            if (!d.LinkedInterventionIds.Remove(interventionId))
                return ServiceOutcome<RouteDiversion>.Fail(OutcomeKind.NotFound, "Not linked to this intervention.");
            _store.Save();
            return ServiceOutcome<RouteDiversion>.Ok(d);
        }
    }
}
