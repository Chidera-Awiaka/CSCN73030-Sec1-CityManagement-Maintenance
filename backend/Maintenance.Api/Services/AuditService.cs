using Maintenance.Api.Infrastructure;
using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

/// <summary>
/// Records and queries the audit history (TEC-05). Status changes, weather-response
/// decisions, assignments and received budget decisions keep their date and their
/// actor (a Maintenance user) or source (an external service).
/// </summary>
public class AuditService
{
    private readonly JsonListStore<AuditEntry> _store;

    /// <summary>Creates the service.</summary>
    /// <param name="filePath">JSON file, or null for memory only (tests).</param>
    public AuditService(string? filePath = null)
    {
        _store = new JsonListStore<AuditEntry>(filePath);
    }

    /// <summary>Adds an entry made by a Maintenance user.</summary>
    /// <param name="entityType">Kind of record, e.g. Intervention.</param>
    /// <param name="entityId">Record ID.</param>
    /// <param name="action">What happened (see <see cref="AuditActions"/>).</param>
    /// <param name="actor">User who made the change.</param>
    /// <param name="details">Readable details.</param>
    /// <returns>The stored entry.</returns>
    public AuditEntry Record(string entityType, object entityId, string action, string actor, string details) =>
        Add(entityType, entityId, action, actor, AuditSources.Maintenance, details);

    /// <summary>Adds an entry caused by an external service (no Maintenance user involved).</summary>
    /// <param name="entityType">Kind of record.</param>
    /// <param name="entityId">Record ID.</param>
    /// <param name="action">What happened.</param>
    /// <param name="source">External service, e.g. Budget.</param>
    /// <param name="details">Readable details.</param>
    /// <returns>The stored entry.</returns>
    public AuditEntry RecordFromService(string entityType, object entityId, string action, string source, string details) =>
        Add(entityType, entityId, action, null, source, details);

    /// <summary>Lists entries, newest first, optionally for one record.</summary>
    /// <param name="entityType">Optional record kind.</param>
    /// <param name="entityId">Optional record ID.</param>
    /// <returns>Matching entries.</returns>
    public List<AuditEntry> Query(string? entityType = null, string? entityId = null)
    {
        lock (_store.Sync)
        {
            return _store.Items
                .Where(x => string.IsNullOrWhiteSpace(entityType)
                    || string.Equals(x.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(entityId) || x.EntityId == entityId)
                .OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
                .ToList();
        }
    }

    private AuditEntry Add(string entityType, object entityId, string action, string? actor, string source, string details)
    {
        lock (_store.Sync)
        {
            var entry = new AuditEntry
            {
                Id = _store.NextId(x => x.Id),
                Timestamp = DateTime.UtcNow,
                EntityType = entityType,
                EntityId = entityId.ToString() ?? "",
                Action = action,
                Actor = actor,
                Source = source,
                Details = details
            };
            _store.Items.Add(entry);
            _store.Save();
            return entry;
        }
    }
}
