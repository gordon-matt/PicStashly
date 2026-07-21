namespace PicStashly.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    [Route("")]
    public IActionResult Index() => View();
}
