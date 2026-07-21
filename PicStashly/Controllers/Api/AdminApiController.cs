namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize(Roles = Constants.Roles.Administrator)]
[Route("api/admin")]
public class AdminApiController : ControllerBase
{
    private readonly IAdminService adminService;

    public AdminApiController(IAdminService adminService)
    {
        this.adminService = adminService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await adminService.GetStorageStatsAsync();
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("orphan-check")]
    public async Task<IActionResult> CheckOrphans()
    {
        var result = await adminService.CheckOrphansAsync();
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("rehash")]
    public async Task<IActionResult> Rehash(CancellationToken cancellationToken)
    {
        var result = await adminService.RehashAllAsync(cancellationToken);
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("reocr")]
    public async Task<IActionResult> ReRunOcr(CancellationToken cancellationToken)
    {
        var result = await adminService.ReRunOcrAsync(cancellationToken);
        return result.ToActionResult(this, value => Ok(value));
    }
}
