using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Application.Security;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests.Security;

public sealed class PermissionAuthorizationServiceTests
{
    [Fact]
    public void AnonymousContext_IsUnauthenticatedAndHasNoPermissions()
    {
        var context = SecurityContext.Anonymous;

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Empty(context.Permissions);
    }

    [Fact]
    public void SecurityContext_SnapshotsPermissions()
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.WorkflowDefinitionsRead
        };
        var context = new SecurityContext("user-1", true, permissions);

        permissions.Add(Permissions.WorkflowDefinitionsWrite);

        Assert.Contains(Permissions.WorkflowDefinitionsRead, context.Permissions);
        Assert.DoesNotContain(Permissions.WorkflowDefinitionsWrite, context.Permissions);
    }

    [Fact]
    public async Task AuthorizeAsync_MatchingPermission_ReturnsExplicitAllowedDecision()
    {
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead]),
            TenantContext.None);

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead),
            CancellationToken.None);

        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public async Task AuthorizeAsync_MissingOrDifferentPermission_ReturnsDeniedDecision()
    {
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead]),
            TenantContext.None);

        var missing = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsWrite),
            CancellationToken.None);
        var differentCase = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead.ToUpperInvariant()),
            CancellationToken.None);

        Assert.False(missing.IsAllowed);
        Assert.Equal(AuthorizationOutcome.Denied, missing.Outcome);
        Assert.False(differentCase.IsAllowed);
    }

    [Fact]
    public async Task AuthorizeAsync_MatchingPermissionAndOwnership_AllowsAccess()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead],
                tenantId.Value.ToString("D")),
            new TenantContext(tenantId));

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead, tenantId),
            CancellationToken.None);

        Assert.Same(AuthorizationDecision.Allow, decision);
    }

    [Fact]
    public async Task AuthorizeAsync_CrossTenantOwnership_DeniesAccess()
    {
        var userTenantId = new TenantId(Guid.NewGuid());
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead],
                userTenantId.Value.ToString("D")),
            new TenantContext(userTenantId));

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead, ownerTenantId),
            CancellationToken.None);

        Assert.Same(AuthorizationDecision.Deny, decision);
    }

    [Fact]
    public async Task AuthorizeAsync_HeaderTenantWithoutMatchingSecurityTenant_DeniesAccess()
    {
        var securityTenantId = new TenantId(Guid.NewGuid());
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead],
                securityTenantId.Value.ToString("D")),
            new TenantContext(ownerTenantId));

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead, ownerTenantId),
            CancellationToken.None);

        Assert.Same(AuthorizationDecision.Deny, decision);
    }

    [Fact]
    public async Task AuthorizeAsync_MissingRequestTenant_DeniesOwnedResourceAccess()
    {
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var service = CreateService(
            new SecurityContext(
                "user-1",
                true,
                [Permissions.WorkflowDefinitionsRead],
                ownerTenantId.Value.ToString("D")),
            TenantContext.None);

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead, ownerTenantId),
            CancellationToken.None);

        Assert.Same(AuthorizationDecision.Deny, decision);
    }

    [Fact]
    public async Task AuthorizeAsync_AnonymousContextIsDenied()
    {
        var service = CreateService(SecurityContext.Anonymous, TenantContext.None);

        var decision = await service.AuthorizeAsync(
            Request(Permissions.WorkflowDefinitionsRead),
            CancellationToken.None);

        Assert.Same(AuthorizationDecision.Deny, decision);
    }

    private static AuthorizationRequest Request(
        string permission,
        TenantId? ownerTenantId = null) =>
        new()
        {
            Permission = permission,
            OwnerTenantId = ownerTenantId
        };

    private static PermissionAuthorizationService CreateService(
        SecurityContext securityContext,
        ITenantContext tenantContext) =>
        new(new UserContextStub(securityContext), tenantContext);

    private sealed class UserContextStub(SecurityContext current) : IUserContext
    {
        public SecurityContext Current { get; } = current;
    }
}
