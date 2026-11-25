using Maintenance.Api.Infrastructure;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>Result of sending (or retrying) an outbound exchange.</summary>
/// <param name="Exchange">The recorded exchange.</param>
/// <param name="AlreadySent">True when the same message had already succeeded, so nothing was re-sent.</param>
public record SendOutcome(ExternalExchange Exchange, bool AlreadySent)
{
    /// <summary>True when the exchange is in the Succeeded state.</summary>
    public bool Succeeded => Exchange.Status == ExchangeRules.Succeeded;
}

/// <summary>
/// Tracks every exchange with external services and supports retries (TEC-04).
/// - Outbound: each message has an idempotency key; a message that already succeeded is
///   never sent again, so retries and double-clicks do not create duplicate requests or updates.
/// - Failed outbound messages stay visible with their error and can be retried.
/// - Inbound: each message is recorded under the sender's external ID so a repeated
///   delivery is detected and not processed twice.
/// </summary>
public class ExchangeService
{
    private readonly JsonListStore<ExternalExchange> _store;
    private readonly ExternalServiceHub _hub;

    /// <summary>
    /// Raised when an outbound exchange succeeds (first send or retry), so the service that
    /// owns the business record (e.g. Budget, Security) can store the external reference.
    /// </summary>
    public event Action<ExternalExchange>? Succeeded;

    /// <summary>Creates the service.</summary>
    /// <param name="hub">Clients for the external services.</param>
    /// <param name="filePath">JSON file, or null for memory only.</param>
    public ExchangeService(ExternalServiceHub hub, string? filePath = null)
    {
        _hub = hub;
        _store = new JsonListStore<ExternalExchange>(filePath);
    }

    /// <summary>
    /// Sends a message unless the same message (same service, operation and key) already
    /// succeeded. A previously failed exchange with the same key is re-attempted in place.
    /// </summary>
    /// <param name="service">External service name.</param>
    /// <param name="operation">Contract operation.</param>
    /// <param name="key">Idempotency key.</param>
    /// <param name="payloadJson">Message body.</param>
    /// <param name="interventionId">Related intervention, if any.</param>
    /// <returns>The exchange and whether it had already been sent.</returns>
    public SendOutcome Send(string service, string operation, string key, string payloadJson, int? interventionId)
    {
        lock (_store.Sync)
        {
            var existing = Find(service, ExchangeRules.Outbound, operation, key);
            if (existing is { Status: ExchangeRules.Succeeded })
            {
                return new SendOutcome(existing, true);
            }

            var exchange = existing ?? new ExternalExchange
            {
                Id = _store.NextId(x => x.Id),
                Service = service,
                Direction = ExchangeRules.Outbound,
                Operation = operation,
                Key = key,
                InterventionId = interventionId,
                PayloadJson = payloadJson,
                CreatedAt = DateTime.UtcNow
            };
            if (existing is null) _store.Items.Add(exchange);

            Attempt(exchange);
            _store.Save();
            return new SendOutcome(exchange, false);
        }
    }

    /// <summary>Retries a failed outbound exchange with its stored message.</summary>
    /// <param name="id">Exchange ID.</param>
    /// <returns>The outcome, or null when the exchange does not exist or is inbound.</returns>
    public SendOutcome? Retry(int id)
    {
        lock (_store.Sync)
        {
            var exchange = _store.Items.FirstOrDefault(x => x.Id == id);
            if (exchange is null || exchange.Direction != ExchangeRules.Outbound) return null;
            if (exchange.Status == ExchangeRules.Succeeded) return new SendOutcome(exchange, true);

            Attempt(exchange);
            _store.Save();
            return new SendOutcome(exchange, false);
        }
    }

    /// <summary>
    /// Records an inbound message. Returns the existing record and duplicate = true when
    /// the same service already delivered a message with this external ID.
    /// </summary>
    /// <param name="service">Sending service.</param>
    /// <param name="operation">Contract operation.</param>
    /// <param name="externalId">Sender's ID for the message.</param>
    /// <param name="payloadJson">Message body.</param>
    /// <param name="interventionId">Related intervention, if any.</param>
    /// <returns>The exchange and whether it was a duplicate delivery.</returns>
    public (ExternalExchange Exchange, bool Duplicate) RecordInbound(
        string service, string operation, string externalId, string payloadJson, int? interventionId = null)
    {
        lock (_store.Sync)
        {
            var existing = Find(service, ExchangeRules.Inbound, operation, externalId);
            if (existing is not null) return (existing, true);

            var exchange = new ExternalExchange
            {
                Id = _store.NextId(x => x.Id),
                Service = service,
                Direction = ExchangeRules.Inbound,
                Operation = operation,
                Key = externalId,
                InterventionId = interventionId,
                PayloadJson = payloadJson,
                Status = ExchangeRules.Received,
                Attempts = 1,
                CreatedAt = DateTime.UtcNow,
                LastAttemptAt = DateTime.UtcNow
            };
            _store.Items.Add(exchange);
            _store.Save();
            return (exchange, false);
        }
    }

    /// <summary>Lists exchanges, newest first, with optional filters.</summary>
    /// <param name="service">External service name, see ExternalServiceNames.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="direction">Optional direction filter: Inbound or Outbound.</param>
    /// <param name="interventionId">Intervention ID.</param>
    /// <returns>The matching records.</returns>
    public List<ExternalExchange> Query(string? service = null, string? status = null, string? direction = null, int? interventionId = null)
    {
        lock (_store.Sync)
        {
            return _store.Items
                .Where(x => string.IsNullOrWhiteSpace(service) || string.Equals(x.Service, service, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(status) || string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(direction) || string.Equals(x.Direction, direction, StringComparison.OrdinalIgnoreCase))
                .Where(x => interventionId is null || x.InterventionId == interventionId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .ToList();
        }
    }

    /// <summary>Gets one exchange.</summary>
    /// <param name="id">Exchange ID.</param>
    /// <returns>The result.</returns>
    public ExternalExchange? GetById(int id)
    {
        lock (_store.Sync) { return _store.Items.FirstOrDefault(x => x.Id == id); }
    }

    private ExternalExchange? Find(string service, string direction, string operation, string key) =>
        _store.Items.FirstOrDefault(x =>
            string.Equals(x.Service, service, StringComparison.OrdinalIgnoreCase)
            && x.Direction == direction
            && x.Operation == operation
            && x.Key == key);

    private void Attempt(ExternalExchange exchange)
    {
        exchange.Attempts++;
        exchange.LastAttemptAt = DateTime.UtcNow;

        var client = _hub.Get(exchange.Service);
        var result = client is null
            ? new ExternalCallResult(false, null, $"No client configured for {exchange.Service}.")
            : SafeSend(client, exchange);

        exchange.Status = result.Success ? ExchangeRules.Succeeded : ExchangeRules.Failed;
        exchange.ExternalReference = result.Success ? result.Reference : exchange.ExternalReference;
        exchange.LastError = result.Success ? null : result.Error;

        if (result.Success) Succeeded?.Invoke(exchange);
    }

    private static ExternalCallResult SafeSend(IExternalServiceClient client, ExternalExchange exchange)
    {
        try
        {
            return client.Send(exchange.Operation, exchange.PayloadJson);
        }
        catch (Exception ex)
        {
            return new ExternalCallResult(false, null, ex.Message);
        }
    }
}
