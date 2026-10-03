using FlowForge.Abstractions.Observability;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Core.Domain.Enums;

namespace FlowForge.Application.Security;

/// <summary>
/// Evaluates permissions and workflow ownership against current operation contexts.
/// </summary>
public sealed class PermissionAuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;
    private readonly ITenantContext _tenantContext;
    private readonly IMetricsCollector _metrics;

    /// <summary>
    /// Initializes the authorization service.
    /// </summary>
    public PermissionAuthorizationService(
        IUserContext userContext,
        ITenantContext tenantContext,
        IMetricsCollector? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _userContext = userContext;
        _tenantContext = tenantContext;
        _metrics = metrics ?? NullMetricsCollector.Instance;
    }

    /// <inheritdoc />
    public Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Permission);
        cancellationToken.ThrowIfCancellationRequested();

        var context = _userContext.Current;
        if (!context.IsAuthenticated ||
            !context.Permissions.Contains(request.Permission))
        {
            return Task.FromResult(RecordDecision(AuthorizationDecision.Deny));
        }

        if (request.OwnerTenantId is not { } ownerTenantId)
        {
            return Task.FromResult(RecordDecision(AuthorizationDecision.Allow));
        }

        var requestTenantId = _tenantContext.TenantId;
        var securityTenantMatchesRequest =
            requestTenantId is { } tenantId &&
            Guid.TryParse(
                context.TenantId,
                out var securityTenantId) &&
            securityTenantId == tenantId.Value;

        if (!securityTenantMatchesRequest)
        {
            return Task.FromResult(RecordDecision(AuthorizationDecision.Deny));
        }

        if (requestTenantId == ownerTenantId)
        {
            return Task.FromResult(RecordDecision(AuthorizationDecision.Allow));
        }

        var sharingAllowsTenant =
            request.Sharing is { } sharing &&
            sharing.OwnerTenantId == ownerTenantId &&
            request.WorkflowDefinitionId == sharing.WorkflowDefinitionId &&
            string.Equals(
                request.DefinitionVersion,
                sharing.DefinitionVersion,
                StringComparison.Ordinal) &&
            sharing.Visibility == WorkflowVisibility.Shared &&
            sharing.SharedTenantIds.Contains(requestTenantId!.Value);

        return Task.FromResult(RecordDecision(
            sharingAllowsTenant
                ? AuthorizationDecision.Allow
                : AuthorizationDecision.Deny));
    }

    private AuthorizationDecision RecordDecision(AuthorizationDecision decision)
    {
        _metrics.IncrementCounter(
            decision.IsAllowed
                ? OperationalMetricNames.AuthorizationAllowed
                : OperationalMetricNames.AuthorizationDenied);
        return decision;
    }
}
