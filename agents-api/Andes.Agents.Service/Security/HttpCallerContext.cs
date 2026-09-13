using System.Security.Claims;
using Andes.Agents.Common.Constants;
using Andes.Agents.Service.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Andes.Agents.Service.Security;

/// <summary>Identity of the authenticated caller of the current request.</summary>
public interface ICallerContext
{
    /// <summary>The caller's Entra object id.</summary>
    /// <exception cref="ForbiddenException">The token carries no object id.</exception>
    string UserId { get; }
}

/// <inheritdoc />
public sealed class HttpCallerContext(IHttpContextAccessor httpContextAccessor) : ICallerContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public string UserId
    {
        get
        {
            ClaimsPrincipal user = _httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("No HTTP request is in scope.");

            if (user.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("The request is not authenticated.");
            }

            // Which of the two arrives depends on whether inbound claim mapping is on.
            string? objectId = user.FindFirst(ClaimTypeNames.Oid)?.Value
                ?? user.FindFirst(ClaimTypeNames.ObjectIdentifier)?.Value;

            return string.IsNullOrWhiteSpace(objectId)
                ? throw new ForbiddenException("The token carries no object id, so its conversations cannot be attributed.")
                : objectId;
        }
    }
}
