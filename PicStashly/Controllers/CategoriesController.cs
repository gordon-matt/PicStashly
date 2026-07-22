namespace PicStashly.Controllers;

[Authorize]
[Route("categories")]
public class CategoriesController : Controller
{
    [Route("")]
    public IActionResult Index() => View();
}
