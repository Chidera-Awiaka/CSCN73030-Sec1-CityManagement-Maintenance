using System.Security.Claims;
using Maintenance.Security;
using Xunit;

namespace Maintenance.Security.Tests;

/// <summary>
/// Tests the Sprint 1 permission matrix.
/// Related requirement: TEC-06, partial scope.
/// </summary>
public sealed class MaintenanceAuthorizationTests
{
    private readonly MaintenanceAuthorization _authorization = new();

    [Theory]
    [InlineData(
        MaintenanceRoles.Agent,
        MaintenancePermission.CreateIntervention,
        true)]
    [InlineData(
        MaintenanceRoles.Agent,
        MaintenancePermission.ViewAllInterventions,
        true)]
    [InlineData(
        MaintenanceRoles.Agent,
        MaintenancePermission.UpdatePriority,
        false)]
    [InlineData(
        MaintenanceRoles.Manager,
        MaintenancePermission.CreateIntervention,
        true)]
    [InlineData(
        MaintenanceRoles.Manager,
        MaintenancePermission.ViewAllInterventions,
        true)]
    [InlineData(
        MaintenanceRoles.Manager,
        MaintenancePermission.UpdatePriority,
        true)]
    [InlineData(
        MaintenanceRoles.Technician,
        MaintenancePermission.CreateIntervention,
        false)]
    [InlineData(
        MaintenanceRoles.Technician,
        MaintenancePermission.ViewAllInterventions,
        false)]
    [InlineData(
        MaintenanceRoles.Technician,
        MaintenancePermission.UpdatePriority,
        false)]
    public void Can_MatchesPermissionMatrix(
        string role,
        MaintenancePermission permission,
        bool expected)
    {
        var user = CreateUser(role);

        bool result = _authorization.Can(user, permission);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Can_NullUser_ReturnsFalse()
    {
        Assert.False(_authorization.Can(
            null,
            MaintenancePermission.CreateIntervention));
    }

    [Fact]
    public void Can_UnauthenticatedIdentity_ReturnsFalse()
    {
        var user = CreateUser(
            MaintenanceRoles.Manager,
            authenticated: false);

        Assert.False(_authorization.Can(
            user,
            MaintenancePermission.UpdatePriority));
    }

    [Fact]
    public void Can_MissingServiceAccess_ReturnsFalse()
    {
        var user = CreateUser(
            MaintenanceRoles.Manager,
            hasServiceAccess: false);

        Assert.False(_authorization.Can(
            user,
            MaintenancePermission.UpdatePriority));
    }

    [Fact]
    public void Can_MissingUserIdentifier_ReturnsFalse()
    {
        var user = CreateUser(
            MaintenanceRoles.Manager,
            hasUserId: false);

        Assert.False(_authorization.Can(
            user,
            MaintenancePermission.UpdatePriority));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Admin")]
    [InlineData("Security.Manager")]
    public void Can_UnrecognizedRole_ReturnsFalse(string role)
    {
        var user = CreateUser(role);

        Assert.False(_authorization.Can(
            user,
            MaintenancePermission.UpdatePriority));
    }

    [Fact]
    public void Can_UnknownPermission_ReturnsFalse()
    {
        var user = CreateUser(MaintenanceRoles.Manager);

        Assert.False(_authorization.Can(
            user,
            (MaintenancePermission)999));
    }

    [Fact]
    public void Can_DoesNotCombineClaimsFromDifferentIdentities()
    {
        var serviceIdentity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(
                    MaintenanceClaims.ServiceAccess,
                    MaintenanceClaims.ServiceName)
            },
            "Test");

        var roleIdentity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-2"),
                new Claim(
                    ClaimTypes.Role,
                    MaintenanceRoles.Manager)
            },
            "Test");

        var user = new ClaimsPrincipal(
            new[] { serviceIdentity, roleIdentity });

        Assert.False(_authorization.Can(
            user,
            MaintenancePermission.UpdatePriority));
    }

    private static ClaimsPrincipal CreateUser(
        string role,
        bool authenticated = true,
        bool hasServiceAccess = true,
        bool hasUserId = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };

        if (hasUserId)
        {
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier,
                "test-user-1"));
        }

        if (hasServiceAccess)
        {
            claims.Add(new Claim(
                MaintenanceClaims.ServiceAccess,
                MaintenanceClaims.ServiceName));
        }

        var identity = new ClaimsIdentity(
            claims,
            authenticated ? "Test" : null);

        return new ClaimsPrincipal(identity);
    }
}