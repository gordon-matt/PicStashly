namespace PicStashly.Controllers;

/// <summary>
/// Landing page for the bookmarklet. The bookmarklet opens this page with imageUrl/pageUrl in the
/// query string; the page then posts to <c>/api/capture</c> same-origin (avoiding any CORS setup)
/// and shows the result, including the dedup prompt.
/// </summary>
[Authorize]
[Route("capture")]
public class CaptureController : Controller
{
    [Route("")]
    public IActionResult Index(string? imageUrl, string? pageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Redirect("/");
        }

        ViewBag.ImageUrl = imageUrl;
        ViewBag.PageUrl = pageUrl;
        return View();
    }
}
