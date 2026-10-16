using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize(Roles = Constants.Roles.Administrator)]
[Route("api/admin")]
[TranslateResultToActionResult]
public class AdminApiController(
    IAdminService adminService,
    IAuthProviderService authProviderService,
    IServiceProvider serviceProvider) : ControllerBase
{
    private UserManager<ApplicationUser>? GetUserManager() =>
        authProviderService.IsIdentity
            ? serviceProvider.GetService<UserManager<ApplicationUser>>()
            : null;

    [HttpGet("stats")]
    public Task<Result<StorageStats>> GetStats() => adminService.GetStorageStatsAsync();

    [HttpPost("orphan-check")]
    public Task<Result<OrphanCheckResult>> CheckOrphans() => adminService.CheckOrphansAsync();

    [HttpPost("rehash")]
    public Task<Result<MaintenanceResult>> Rehash(CancellationToken cancellationToken) => adminService.RehashAllAsync(cancellationToken);

    [HttpPost("reocr")]
    public Task<Result<MaintenanceResult>> ReRunOcr(CancellationToken cancellationToken) => adminService.ReRunOcrAsync(cancellationToken);

    /// <summary>
    /// Lists local accounts and their approval/role status. Only available when using the
    /// built-in Identity provider — Keycloak-authenticated users are managed in Keycloak itself.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        var users = userManager.Users.OrderBy(u => u.Email).ToList();

        var result = new List<object>();
        foreach (var user in users)
        {
            bool isPendingApproval = user.LockoutEnabled
                && user.LockoutEnd.HasValue
                && user.LockoutEnd > DateTimeOffset.UtcNow;

            var roles = await userManager.GetRolesAsync(user);

            result.Add(new
            {
                userId = user.Id,
                email = user.Email,
                userName = user.UserName,
                isPendingApproval,
                role = roles.FirstOrDefault() ?? string.Empty
            });
        }

        return Ok(result);
    }

    /// <summary>Approves a pending registration, clearing its lockout so the user can sign in.</summary>
    [HttpPost("users/{userId}/approve")]
    public async Task<IActionResult> ApproveUser(string userId)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        await userManager.SetLockoutEndDateAsync(user, null);

        return Ok(new { message = "User approved successfully." });
    }

    /// <summary>Locks an account (revoking access) until an admin approves it again.</summary>
    [HttpPost("users/{userId}/lock")]
    public async Task<IActionResult> LockUser(string userId)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentUserId)
        {
            return BadRequest("You cannot lock your own account.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Ok(new { message = "User locked successfully." });
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> ChangeUserRole(string userId, [FromBody] ChangeUserRoleRequest request)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentUserId)
        {
            return BadRequest("You cannot change your own role.");
        }

        string[] validRoles = [Constants.Roles.Administrator, Constants.Roles.User];
        if (!validRoles.Contains(request.Role))
        {
            return BadRequest($"Invalid role. Must be one of: {string.Join(", ", validRoles)}.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, request.Role);

        return Ok(new { role = request.Role });
    }
}