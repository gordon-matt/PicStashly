namespace PicStashly.Controllers.Api;

/// <summary>Bookmarklet capture endpoint: accepts an image URL + page URL, downloads and ingests the image.</summary>
[ApiController]
[Authorize]
[Route("api/capture")]
public class CaptureApiController : ControllerBase
{
    private readonly IImageService imageService;

    public CaptureApiController(IImageService imageService)
    {
        this.imageService = imageService;
    }

    [HttpPost]
    public async Task<IActionResult> Capture([FromBody] CaptureRequest request, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        var result = await imageService.IngestFromUrlAsync(request, force, cancellationToken);
        return result.ToActionResult(this, value => Ok(value));
    }
}
