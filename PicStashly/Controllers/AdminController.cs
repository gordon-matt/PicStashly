namespace PicStashly.Controllers;

[Authorize(Roles = Constants.Roles.Administrator)]
[Route("admin")]
public class AdminController : Controller
{
    [Route("")]
    public IActionResult Index() => View();
}