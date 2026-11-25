namespace Maintenance.Api.Models;

/// <summary>Assistance request sent to Security (SEC-01) and its response (SEC-02).</summary>
public class SecurityRequest
{
    /// <summary>Maintenance's ID for the request.</summary>
    public int Id { get; set; }

    /// <summary>Intervention that needs support.</summary>
    public int InterventionId { get; set; }

    /// <summary>Why support is needed.</summary>
    public string Reason { get; set; } = "";

    /// <summary>Where.</summary>
    public string Location { get; set; } = "";

    /// <summary>Start of the period (UTC).</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>End of the period (UTC).</summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>Who created the request.</summary>
    public string RequestedBy { get; set; } = "";

    /// <summary>When it was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Exchange used to send it (TEC-04).</summary>
    public int ExchangeId { get; set; }

    /// <summary>Transmission status: Succeeded or Failed (SEC-01: visible).</summary>
    public string TransmissionStatus { get; set; } = ExchangeRules.Failed;

    /// <summary>Last transmission error, when it failed.</summary>
    public string? TransmissionError { get; set; }

    /// <summary>Security's reference for the request (returned when it was accepted).</summary>
    public string? SecurityReference { get; set; }

    /// <summary>Security's response, or null while waiting (SEC-02).</summary>
    public SecurityResponse? Response { get; set; }
}

/// <summary>Response from Security (SEC-02). Only Security can create it.</summary>
public class SecurityResponse
{
    /// <summary>Approved, Declined or Partially Approved.</summary>
    public string Outcome { get; set; } = "";

    /// <summary>Comments supplied by Security.</summary>
    public string? Comments { get; set; }

    /// <summary>Security officer, when supplied.</summary>
    public string? RespondedBy { get; set; }

    /// <summary>Security's ID for the response message.</summary>
    public string ResponseId { get; set; } = "";

    /// <summary>When Maintenance received it (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Allowed Security outcomes (SEC-02).</summary>
public static class SecurityRules
{
    /// <summary>Outcomes Security can send.</summary>
    public static readonly string[] Outcomes = { "Approved", "Declined", "Partially Approved" };
}
