namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize(Roles = Constants.Roles.Administrator)]
[Route("api/admin")]
[TranslateResultToActionResult]
public class AdminApiController : ControllerBase
{
    private readonly IAdminService adminService;

    public AdminApiController(IAdminService adminService)
    {
        this.adminService = adminService;
    }

    [HttpGet("stats")]
    public Task<Result<StorageStats>> GetStats() => adminService.GetStorageStatsAsync();

    [HttpPost("orphan-check")]
    public Task<Result<OrphanCheckResult>> CheckOrphans() => adminService.CheckOrphansAsync();

    [HttpPost("rehash")]
    public Task<Result<MaintenanceResult>> Rehash(CancellationToken cancellationToken) =>
        adminService.RehashAllAsync(cancellationToken);

    [HttpPost("reocr")]
    public Task<Result<MaintenanceResult>> ReRunOcr(CancellationToken cancellationToken) =>
        adminService.ReRunOcrAsync(cancellationToken);
}
