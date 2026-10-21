using System.Security.Claims;

namespace Maintenance.Security;

/// <summary>
/// Operations protected by the Maintenance authorization rules.
/// </summary>
public enum MaintenancePermission
{
    CreateIntervention,
    ViewAllInterventions,
    UpdatePriority
}

/// <summary>
/// Proposed role names to agree with the Registration team.
/// </summary>
public static class MaintenanceRoles
{
    public const string Agent = "Maintenance.Agent";
    public const string Manager = "Maintenance.Manager";
    public const string Technician = "Maintenance.Technician";
}

/// <summary>
/// Proposed service-access claim contract.
/// </summary>
public static class MaintenanceClaims
{
    public const string ServiceAccess = "service_access";
    public const string ServiceName = "Maintenance";
}

/// <summary>
/// Evaluates permissions for the Sprint 1 Maintenance operations.
/// Related requirement: TEC-01.
/// </summary>
/// <remarks>
/// The caller must supply a principal from trusted authentication.
/// This class does not authenticate users or validate tokens.
/// Claim names and role names must be agreed with Registration.
/// </remarks>
public sealed class MaintenanceAuthorization
{
    /// <summary>
    /// Determines whether a user can perform an operation.
    /// </summary>
    /// <param name="user">
    /// The principal supplied by the trusted authentication system.
    /// </param>
    /// <param name="permission">The requested operation.</param>
    /// <returns>
    /// True when an authenticated identity has service access and
    /// a role permitting the operation; otherwise, false.
    /// </returns>
    public bool Can(
        ClaimsPrincipal? user,
        MaintenancePermission permission)
    {
        if (user is null)
        {
            return false;
        }

        // Evaluate each identity independently to avoid combining
        // access from one identity with roles from another.
        return user.Identities.Any(identity =>
            identity.IsAuthenticated
            && identity.HasClaim(
                claim => claim.Type == ClaimTypes.NameIdentifier
                    && !string.IsNullOrWhiteSpace(claim.Value))
            && identity.HasClaim(
                MaintenanceClaims.ServiceAccess,
                MaintenanceClaims.ServiceName)
            && IsAllowed(identity, permission));
    }

    private static bool IsAllowed(
        ClaimsIdentity identity,
        MaintenancePermission permission)
    {
        bool isAgent = identity.HasClaim(
            identity.RoleClaimType,
            MaintenanceRoles.Agent);

        bool isManager = identity.HasClaim(
            identity.RoleClaimType,
            MaintenanceRoles.Manager);

        return permission switch
        {
            MaintenancePermission.CreateIntervention =>
                isAgent || isManager,

            MaintenancePermission.ViewAllInterventions =>
                isAgent || isManager,

            MaintenancePermission.UpdatePriority =>
                isManager,

            _ => false
        };
    }
}