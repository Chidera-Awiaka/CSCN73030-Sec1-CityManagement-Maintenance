using Microsoft.AspNetCore.Http;

namespace Maintenance.Api.Infrastructure;

/// <summary>
/// Reads who is calling the API from request headers (TEC-05 actor / source).
/// </summary>
public static class RequestContext
{
    /// <summary>Header carrying the Maintenance user making the change.</summary>
    public const string UserHeader = "X-User-Id";

    /// <summary>Header carrying the external service that sent a message (E-services, Budget, Security, Public Transport...).</summary>
    public const string SourceServiceHeader = "X-Source-Service";

    /// <summary>Actor used when no user header is supplied.</summary>
    public const string UnknownActor = "unidentified user";

    /// <summary>The Maintenance user, or "unidentified user".</summary>
    public static string Actor(HttpRequest? request)
    {
        var value = request?.Headers[UserHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? UnknownActor : value.Trim();
    }

    /// <summary>The calling external service, or null when not supplied.</summary>
    public static string? SourceService(HttpRequest? request)
    {
        var value = request?.Headers[SourceServiceHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
