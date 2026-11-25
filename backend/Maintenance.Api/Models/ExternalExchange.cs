namespace Maintenance.Api.Models;

/// <summary>
/// One message exchanged with an external service (TEC-04). Every outbound send and
/// every inbound message is recorded, so failed exchanges are visible and can be retried.
/// </summary>
public class ExternalExchange
{
    /// <summary>Exchange ID.</summary>
    public int Id { get; set; }

    /// <summary>External service, e.g. Budget.</summary>
    public string Service { get; set; } = "";

    /// <summary>Outbound (Maintenance sent it) or Inbound (Maintenance received it).</summary>
    public string Direction { get; set; } = ExchangeRules.Outbound;

    /// <summary>Operation from the contract, e.g. BudgetRequest, RoadClosureNotice.</summary>
    public string Operation { get; set; } = "";

    /// <summary>
    /// Idempotency key. Outbound: built by Maintenance from the business data, so a
    /// repeated send or retry does not create a second message. Inbound: the sender's
    /// external ID, so a repeated delivery is detected.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>Intervention the exchange relates to, when there is one.</summary>
    public int? InterventionId { get; set; }

    /// <summary>Message body (JSON).</summary>
    public string PayloadJson { get; set; } = "";

    /// <summary>Succeeded, Failed or Received.</summary>
    public string Status { get; set; } = ExchangeRules.Failed;

    /// <summary>Number of send attempts (outbound).</summary>
    public int Attempts { get; set; }

    /// <summary>Last error message, when the last attempt failed.</summary>
    public string? LastError { get; set; }

    /// <summary>Reference returned by the external service on success.</summary>
    public string? ExternalReference { get; set; }

    /// <summary>When the exchange was first recorded (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the last attempt was made (UTC).</summary>
    public DateTime? LastAttemptAt { get; set; }
}

/// <summary>Allowed values for <see cref="ExternalExchange"/> (TEC-04).</summary>
public static class ExchangeRules
{
    /// <summary>Message sent by Maintenance.</summary>
    public const string Outbound = "Outbound";
    /// <summary>Message received by Maintenance.</summary>
    public const string Inbound = "Inbound";
    /// <summary>Outbound message accepted by the external service.</summary>
    public const string Succeeded = "Succeeded";
    /// <summary>Outbound message not delivered; can be retried.</summary>
    public const string Failed = "Failed";
    /// <summary>Inbound message received and processed.</summary>
    public const string Received = "Received";
}
