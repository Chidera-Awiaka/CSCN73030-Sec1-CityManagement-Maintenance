namespace Maintenance.Api.Models;

/// <summary>
/// Outcome of receiving, accepting or rejecting an incident report
/// (ESV-01, ESV-02). The controller turns each value into an HTTP status.
/// </summary>
public enum ReviewResult
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>The request failed validation (HTTP 400).</summary>
    Invalid,

    /// <summary>No incident report has that ID (HTTP 404).</summary>
    NotFound,

    /// <summary>
    /// The report was already accepted or rejected, or the same
    /// E-services report was already received (HTTP 409).
    /// </summary>
    Conflict
}
