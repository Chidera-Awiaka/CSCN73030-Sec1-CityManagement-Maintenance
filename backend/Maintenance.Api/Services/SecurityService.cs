using System.Text.Json;
using Maintenance.Api.DTOs;
using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Security integration.
/// SEC-01 send an assistance request (reason, location, period, intervention ID; transmission status visible).
/// SEC-02 receive Security's response (linked to the request; outcome and comments visible). Only the
///        Security service can send a response, so Maintenance users cannot approve on Security's behalf.
/// </summary>
public class SecurityService
{
    private const string Operation = "AssistanceRequest";
    private readonly JsonListStore<SecurityRequest> _store;
    private readonly InterventionService _interventions;
    private readonly ExchangeService _exchanges;
    private readonly AuditService _audit;

    /// <summary>Creates the service.</summary>
    /// <param name="interventions">Intervention service.</param>
    /// <param name="exchanges">Exchange log (TEC-04).</param>
    /// <param name="audit">Audit history (TEC-05).</param>
    /// <param name="filePath">JSON file used for storage, or null to keep data in memory (tests).</param>
    public SecurityService(InterventionService interventions, ExchangeService exchanges, AuditService audit, string? filePath = null)
    {
        _interventions = interventions;
        _exchanges = exchanges;
        _audit = audit;
        _store = new JsonListStore<SecurityRequest>(filePath);
        _exchanges.Succeeded += OnExchangeSucceeded;
    }

    /// <summary>Lists requests, newest first, optionally for one intervention.</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The matching records.</returns>
    public List<SecurityRequest> GetAll(int? interventionId = null)
    {
        lock (_store.Sync)
        {
            return _store.Items.Where(r => interventionId is null || r.InterventionId == interventionId)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).ToList();
        }
    }

    /// <summary>Creates and sends an assistance request (SEC-01). A failed send is kept and can be retried.</summary>
    /// <param name="interventionId">Intervention ID.</param>
    /// <param name="request">Reason, location and period.</param>
    /// <param name="actor">Maintenance user making the change (recorded in the audit history).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<SecurityRequest> Request(int interventionId, SecurityRequestDto request, string actor)
    {
        if (request is null) return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var intervention = _interventions.GetById(interventionId);
        if (intervention is null) return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.NotFound, "Intervention not found.");

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Reason)) errors.Add("Reason is required.");
        if (request.PeriodStart is null) errors.Add("Period start is required.");
        if (request.PeriodEnd is null) errors.Add("Period end is required.");
        if (request.PeriodStart is not null && request.PeriodEnd is not null && request.PeriodEnd < request.PeriodStart)
            errors.Add("Period end cannot be before period start.");
        var location = string.IsNullOrWhiteSpace(request.Location) ? intervention.Location : request.Location.Trim();
        if (string.IsNullOrWhiteSpace(location)) errors.Add("Location is required.");
        if (errors.Count > 0) return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Invalid, errors);

        SecurityRequest record;
        lock (_store.Sync)
        {
            record = new SecurityRequest
            {
                Id = _store.NextId(r => r.Id),
                InterventionId = interventionId,
                Reason = request.Reason.Trim(),
                Location = location,
                PeriodStart = request.PeriodStart!.Value,
                PeriodEnd = request.PeriodEnd!.Value,
                RequestedBy = actor,
                CreatedAt = DateTime.UtcNow
            };
            _store.Items.Add(record);
            _store.Save();
        }

        var payload = JsonSerializer.Serialize(new
        {
            maintenanceRequestId = record.Id,
            interventionId,
            reason = record.Reason,
            location = record.Location,
            periodStart = record.PeriodStart,
            periodEnd = record.PeriodEnd,
            requestedBy = actor
        });
        var sent = _exchanges.Send(ExternalServiceNames.Security, Operation, $"security-request:{record.Id}", payload, interventionId);

        lock (_store.Sync)
        {
            record.ExchangeId = sent.Exchange.Id;
            record.TransmissionStatus = sent.Exchange.Status;
            record.TransmissionError = sent.Exchange.LastError;
            record.SecurityReference = sent.Exchange.ExternalReference;
            _store.Save();
        }
        return ServiceOutcome<SecurityRequest>.Ok(record);
    }

    /// <summary>
    /// Receives Security's response (SEC-02). Rejected (403) unless the caller is the Security
    /// service; a repeated delivery is ignored (TEC-04).
    /// </summary>
    /// <param name="response">Response message from Security.</param>
    /// <param name="sourceService">Value of the X-Source-Service header (the calling service).</param>
    /// <returns>Success with the record, or the failure kind and messages.</returns>
    public ServiceOutcome<SecurityRequest> ReceiveResponse(SecurityResponseDto response, string? sourceService)
    {
        if (!string.Equals(sourceService, ExternalServiceNames.Security, StringComparison.OrdinalIgnoreCase))
            return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Forbidden,
                "Only the Security service can respond to assistance requests (X-Source-Service: Security). Maintenance users cannot approve requests on Security's behalf.");

        if (response is null) return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Invalid, "Request body is required.");
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(response.ResponseId)) errors.Add("Response ID is required.");
        if (string.IsNullOrWhiteSpace(response.RequestReference)) errors.Add("Request reference is required.");
        var outcome = SecurityRules.Outcomes.FirstOrDefault(o => o.Equals(response.Outcome?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (outcome is null) errors.Add("Outcome must be Approved, Declined or Partially Approved.");
        if (errors.Count > 0) return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Invalid, errors);

        SecurityRequest? record;
        lock (_store.Sync)
        {
            record = _store.Items.FirstOrDefault(r => r.SecurityReference == response.RequestReference.Trim());
        }
        if (record is null)
            return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.NotFound, $"No assistance request with reference {response.RequestReference}.");

        // Recorded outside this service's lock (the exchange log raises events that take it).
        var (_, duplicate) = _exchanges.RecordInbound(ExternalServiceNames.Security, "AssistanceResponse",
            response.ResponseId.Trim(), JsonSerializer.Serialize(response), record.InterventionId);
        if (duplicate) return ServiceOutcome<SecurityRequest>.Ok(record, duplicate: true);

        lock (_store.Sync)
        {
            if (record.Response is not null)
                return ServiceOutcome<SecurityRequest>.Fail(OutcomeKind.Conflict, "Security has already responded to this request.");

            record.Response = new SecurityResponse
            {
                Outcome = outcome!,
                Comments = response.Comments?.Trim(),
                RespondedBy = response.RespondedBy?.Trim(),
                ResponseId = response.ResponseId.Trim(),
                ReceivedAt = DateTime.UtcNow
            };
            _store.Save();
        }
        _audit.RecordFromService("SecurityRequest", record.Id, AuditActions.SecurityResponseReceived, ExternalServiceNames.Security,
            $"{outcome} for intervention {record.InterventionId}: {response.Comments}");
        return ServiceOutcome<SecurityRequest>.Ok(record);
    }

    private void OnExchangeSucceeded(ExternalExchange exchange)
    {
        if (exchange.Operation != Operation) return;
        lock (_store.Sync)
        {
            var record = _store.Items.FirstOrDefault(r => r.ExchangeId == exchange.Id);
            if (record is null) return;
            record.TransmissionStatus = exchange.Status;
            record.TransmissionError = null;
            record.SecurityReference = exchange.ExternalReference;
            _store.Save();
        }
    }
}
