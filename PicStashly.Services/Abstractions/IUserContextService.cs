namespace PicStashly.Services.Abstractions;

/// <summary>
/// Provider-agnostic access to the current request's authenticated user, used to scope every
/// Category/Image/Tag query and mutation to the signed-in user. Backed by the
/// <c>ClaimTypes.NameIdentifier</c> claim, which is the local Identity user id when using
/// ASP.NET Core Identity, or the Keycloak "sub" claim when using Keycloak — either way it is
/// stable across sessions and unique per user.
/// </summary>
public interface IUserContextService
{
    /// <summary>The current user's id, or null if there is no authenticated user on this request.</summary>
    string? GetCurrentUserId();

    /// <summary>The current user's id. Throws if there is no authenticated user — use only behind [Authorize].</summary>
    string GetRequiredUserId();

    /// <summary>True when the current user is in the Administrator role.</summary>
    bool IsAdmin();
}
