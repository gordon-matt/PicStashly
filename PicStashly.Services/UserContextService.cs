using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace PicStashly.Services;

public class UserContextService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider) : IUserContextService
{
    public string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string GetRequiredUserId() =>
        GetCurrentUserId() ?? throw new InvalidOperationException("No authenticated user on the current request.");

    public bool IsAdmin() => User.IsInRole(Constants.Roles.Administrator);

    /// <summary>
    /// An MVC request has an <see cref="HttpContext"/>. A Blazor circuit does not, after the
    /// first render, so the signed-in principal is read from the circuit authentication state.
    /// </summary>
    private ClaimsPrincipal User
    {
        get
        {
            var httpUser = httpContextAccessor.HttpContext?.User;
            return httpUser?.Identity?.IsAuthenticated == true
                ? httpUser
                : authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
        }
    }
}