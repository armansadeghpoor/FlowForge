using FlowForge.Abstractions.Security;
using FlowForge.Application.Security;

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
    public async Task AuthorizeAsync_UsesAuthenticatedContextAndOrdinalPermissions()
    {
        var context = new SecurityContext(
            "user-1",
            true,
            [Permissions.WorkflowDefinitionsRead]);
        var service = new PermissionAuthorizationService(new UserContextStub(context));

        var granted = await service.AuthorizeAsync(
            Permissions.WorkflowDefinitionsRead,
            CancellationToken.None);
        var differentCase = await service.AuthorizeAsync(
            Permissions.WorkflowDefinitionsRead.ToUpperInvariant(),
            CancellationToken.None);

        Assert.True(granted);
        Assert.False(differentCase);
    }

    [Fact]
    public async Task AuthorizeAsync_AnonymousContextIsDenied()
    {
        var service = new PermissionAuthorizationService(
            new UserContextStub(SecurityContext.Anonymous));

        var granted = await service.AuthorizeAsync(
            Permissions.WorkflowDefinitionsRead,
            CancellationToken.None);

        Assert.False(granted);
    }

    private sealed class UserContextStub(SecurityContext current) : IUserContext
    {
        public SecurityContext Current { get; } = current;
    }
}
