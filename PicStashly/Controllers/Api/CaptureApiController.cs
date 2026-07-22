namespace PicStashly.Controllers.Api;

/// <summary>Bookmarklet capture endpoint: accepts an image URL + page URL, downloads and ingests the image.</summary>
[ApiController]
[Authorize]
[Route("api/capture")]
[TranslateResultToActionResult]
public class CaptureApiController : ControllerBase
{
    private readonly IImageService imageService;

    public CaptureApiController(IImageService imageService)
    {
        this.imageService = imageService;
    }

    [HttpPost]
    public Task<Result<IngestResult>> Capture(
        [FromBody] CaptureRequest request,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default) => imageService.IngestFromUrlAsync(request, force, cancellationToken);
}