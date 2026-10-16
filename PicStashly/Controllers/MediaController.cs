using Microsoft.AspNetCore.StaticFiles;

namespace PicStashly.Controllers;

/// <summary>
/// Serves stored images/thumbnails from disk under <c>/media</c>. Unlike the old static-files
/// middleware, this checks that the requesting user owns the file (or is an Administrator)
/// before returning it, since each user's images now live under their own on-disk folder.
/// </summary>
[Authorize]
[Route(Constants.MediaRequestPath)]
public class MediaController(IFileStorageService fileStorage, IUserContextService userContext) : Controller
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".bmp"] = "image/bmp",
        });

    [HttpGet("{**relativePath}")]
    public IActionResult Get(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return NotFound();
        }

        // Expected shape: "images|thumbnails/{userId}/{categoryFolder}/{fileName}".
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || segments.Any(s => s is "." or ".."))
        {
            return NotFound();
        }

        bool isImagesOrThumbnails = segments[0] is Constants.FileStorage.Images or Constants.FileStorage.Thumbnails;
        if (!isImagesOrThumbnails)
        {
            return NotFound();
        }

        string ownerUserId = segments[1];
        string? currentUserId = userContext.GetCurrentUserId();
        if (!string.Equals(ownerUserId, currentUserId, StringComparison.Ordinal) && !userContext.IsAdmin())
        {
            return NotFound();
        }

        string fullPath = fileStorage.GetFullPath(relativePath);
        return !System.IO.File.Exists(fullPath)
            ? NotFound()
            : !ContentTypeProvider.TryGetContentType(fullPath, out string? contentType)
            ? NotFound()
            : PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }
}