namespace PicStashly.Controllers;

[Authorize]
[Route("upload")]
public class UploadController : Controller
{
    private readonly IImageService imageService;

    public UploadController(IImageService imageService)
    {
        this.imageService = imageService;
    }

    [Route("")]
    public IActionResult Index() => View();

    /// <summary>
    /// PWA Web Share Target endpoint: mobile "Share" sends images here as a multipart POST.
    /// Saved images redirect to their detail page; near-duplicates redirect to the existing image.
    /// </summary>
    [HttpPost("share")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Share(List<IFormFile> images)
    {
        if (images is not { Count: > 0 })
        {
            return Redirect("/upload");
        }

        int? lastImageId = null;

        foreach (var file in images)
        {
            using var stream = file.OpenReadStream();
            var result = await imageService.IngestAsync(stream, file.FileName, sourceUrl: null, tagNames: null, categoryId: null, force: false);

            if (!result.IsSuccess)
            {
                continue;
            }

            lastImageId = result.Value.IsDuplicate
                ? result.Value.Duplicates.FirstOrDefault()?.Id
                : result.Value.Image?.Id;
        }

        return lastImageId is int id ? Redirect($"/image/{id}") : Redirect("/");
    }
}