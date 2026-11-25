namespace Maintenance.Api.Models;

/// <summary>
/// A notice Maintenance sends to one or more external services about an intervention:
/// road closures (ESV-06, SEC-03, TRP-02), work-status updates (SEC-03) and
/// property-related service disruptions (REG-04).
/// </summary>
public class OutboundNotice
{
    /// <summary>Notice ID.</summary>
    public int Id { get; set; }

    /// <summary>RoadClosure, WorkStatusUpdate or PropertyDisruption.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Related intervention.</summary>
    public int InterventionId { get; set; }

    /// <summary>One-line summary, e.g. "Closed - King Street North, Nov 26 08:00 to Nov 26 17:00".</summary>
    public string Summary { get; set; } = "";

    /// <summary>Message body sent to every service (JSON).</summary>
    public string PayloadJson { get; set; } = "";

    /// <summary>Who sent it.</summary>
    public string SentBy { get; set; } = "";

    /// <summary>When it was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Delivery to each service; status is refreshed from the exchange log (TEC-04).</summary>
    public List<NoticeDelivery> Deliveries { get; set; } = new();
}

/// <summary>Delivery of a notice to one service.</summary>
public class NoticeDelivery
{
    /// <summary>Service name.</summary>
    public string Service { get; set; } = "";

    /// <summary>Exchange ID (use it to retry a failed delivery).</summary>
    public int ExchangeId { get; set; }

    /// <summary>Succeeded or Failed.</summary>
    public string Status { get; set; } = "";

    /// <summary>Reference returned by the service.</summary>
    public string? Reference { get; set; }

    /// <summary>Error, when it failed.</summary>
    public string? Error { get; set; }
}

/// <summary>Allowed values for notices.</summary>
public static class NoticeRules
{
    /// <summary>Road closure notice.</summary>
    public const string RoadClosure = "RoadClosure";
    /// <summary>Work-status update.</summary>
    public const string WorkStatusUpdate = "WorkStatusUpdate";
    /// <summary>Property-related service disruption.</summary>
    public const string PropertyDisruption = "PropertyDisruption";

    /// <summary>Closure states: Closed, Updated (changed dates/location) or Reopened.</summary>
    public static readonly string[] ClosureStatuses = { "Closed", "Updated", "Reopened" };

    /// <summary>Services that receive road closures by default.</summary>
    public static readonly string[] RoadClosureServices = { "E-services", "Security", "Public Transport" };

    /// <summary>Disruption types (REG-04).</summary>
    public static readonly string[] DisruptionTypes = { "Water", "Electricity", "Gas", "Access", "Other" };

    /// <summary>Disruption update types (REG-04): new, update, restoration.</summary>
    public static readonly string[] DisruptionUpdateTypes = { "New", "Update", "Restored" };
}
