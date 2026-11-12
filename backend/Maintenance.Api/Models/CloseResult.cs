namespace Maintenance.Api.Models;

/// <summary>Outcome of closing an intervention (MNT-08).</summary>
public enum CloseResult
{
    /// <summary>The intervention was closed and its status set to Completed.</summary>
    Closed,

    /// <summary>The intervention does not exist.</summary>
    NotFound,

    /// <summary>The intervention is already Completed or Cancelled and cannot be closed again.</summary>
    AlreadyClosed
}
