using Microsoft.AspNetCore.Authorization;

namespace FlowForge.Api.Security;

/// <summary>
/// Requires one FlowForge permission from the current security context.
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
