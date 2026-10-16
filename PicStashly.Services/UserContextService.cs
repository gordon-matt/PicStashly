using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PicStashly.Services;

public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
{
    public string? GetCurrentUserId() =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string GetRequiredUserId() =>
        GetCurrentUserId() ?? throw new InvalidOperationException("No authenticated user on the current request.");

    public bool IsAdmin() =>
        httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.Administrator) == true;
}