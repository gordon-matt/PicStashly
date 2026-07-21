namespace PicStashly.Controllers;

[Route("tags")]
public class TagsController : Controller
{
    [Route("")]
    public IActionResult Index() => View();
}
