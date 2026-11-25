namespace Maintenance.Api.DTOs;

/// <summary>Cost estimate entry (BUD-01).</summary>
public class CostEstimateDto
{
    /// <summary>Repair (labour) cost. Not negative.</summary>
    public decimal RepairCost { get; set; }
    /// <summary>Material cost. Not negative.</summary>
    public decimal MaterialCost { get; set; }
    /// <summary>Equipment cost. Not negative.</summary>
    public decimal EquipmentCost { get; set; }
}

/// <summary>Decision message sent by the Budget service (BUD-03).</summary>
public class BudgetDecisionDto
{
    /// <summary>Budget's ID for this decision message. Required.</summary>
    public string DecisionId { get; set; } = "";
    /// <summary>Reference returned when the request was submitted. Required.</summary>
    public string RequestReference { get; set; } = "";
    /// <summary>Approved or Rejected. Required.</summary>
    public string Decision { get; set; } = "";
    /// <summary>Approved amount. Optional.</summary>
    public decimal? ApprovedAmount { get; set; }
    /// <summary>Comments. Optional.</summary>
    public string? Comments { get; set; }
    /// <summary>Budget officer. Optional.</summary>
    public string? DecidedBy { get; set; }
}

/// <summary>Assistance request to Security (SEC-01).</summary>
public class SecurityRequestDto
{
    /// <summary>Why support is needed. Required.</summary>
    public string Reason { get; set; } = "";
    /// <summary>Location. Defaults to the intervention location.</summary>
    public string? Location { get; set; }
    /// <summary>Start of the period. Required.</summary>
    public DateTime? PeriodStart { get; set; }
    /// <summary>End of the period. Required.</summary>
    public DateTime? PeriodEnd { get; set; }
}

/// <summary>Response message sent by Security (SEC-02).</summary>
public class SecurityResponseDto
{
    /// <summary>Security's ID for this response. Required.</summary>
    public string ResponseId { get; set; } = "";
    /// <summary>Reference Security gave the request when it was sent. Required.</summary>
    public string RequestReference { get; set; } = "";
    /// <summary>Approved, Declined or Partially Approved. Required.</summary>
    public string Outcome { get; set; } = "";
    /// <summary>Comments. Optional.</summary>
    public string? Comments { get; set; }
    /// <summary>Security officer. Optional.</summary>
    public string? RespondedBy { get; set; }
}

/// <summary>Road closure notice (ESV-06, SEC-03, TRP-02).</summary>
public class RoadClosureDto
{
    /// <summary>Closed, Updated or Reopened. Required.</summary>
    public string ClosureStatus { get; set; } = "";
    /// <summary>Location. Defaults to the intervention location.</summary>
    public string? Location { get; set; }
    /// <summary>Planned start. Defaults to the intervention's planned start.</summary>
    public DateTime? PlannedStart { get; set; }
    /// <summary>Planned end. Defaults to the intervention's planned end.</summary>
    public DateTime? PlannedEnd { get; set; }
    /// <summary>Services to notify. Defaults to E-services, Security and Public Transport.</summary>
    public List<string>? Services { get; set; }
}

/// <summary>Work-status update (SEC-03).</summary>
public class WorkStatusUpdateDto
{
    /// <summary>Services to notify. Defaults to Security.</summary>
    public List<string>? Services { get; set; }
}

/// <summary>Property-related service disruption (REG-04).</summary>
public class PropertyDisruptionDto
{
    /// <summary>Water, Electricity, Gas, Access or Other. Required.</summary>
    public string DisruptionType { get; set; } = "";
    /// <summary>New, Update or Restored. Defaults to New.</summary>
    public string? UpdateType { get; set; }
    /// <summary>Start of the disruption. Required.</summary>
    public DateTime? PeriodStart { get; set; }
    /// <summary>End of the disruption (or restoration time). Optional.</summary>
    public DateTime? PeriodEnd { get; set; }
    /// <summary>Notes. Optional.</summary>
    public string? Notes { get; set; }
}

/// <summary>Route change / diversion sent by Public Transport (TRP-03).</summary>
public class RouteDiversionDto
{
    /// <summary>Public Transport's reference. Required.</summary>
    public string ExternalReference { get; set; } = "";
    /// <summary>Affected route IDs. At least one.</summary>
    public List<string>? AffectedRoutes { get; set; }
    /// <summary>What changes. Required.</summary>
    public string Description { get; set; } = "";
    /// <summary>Effective from. Required.</summary>
    public DateTime? EffectiveFrom { get; set; }
    /// <summary>Effective until. Optional.</summary>
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>Link between a received record and an intervention.</summary>
public class InterventionLinkDto
{
    /// <summary>Intervention ID.</summary>
    public int InterventionId { get; set; }
}

/// <summary>Mock availability switch (TEC-03).</summary>
public class MockAvailabilityDto
{
    /// <summary>True = service up, false = service down.</summary>
    public bool Available { get; set; } = true;
}
