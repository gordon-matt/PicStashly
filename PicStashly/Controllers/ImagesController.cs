namespace PicStashly.Controllers;

[Authorize]
[Route("image")]
public class ImagesController : Controller
{
    private readonly IRepository<Image> imageRepository;

    public ImagesController(IRepository<Image> imageRepository)
    {
        this.imageRepository = imageRepository;
    }

    [Route("{id}")]
    public async Task<IActionResult> Details(int id)
    {
        bool imageExists = await imageRepository.ExistsAsync(x => x.Id == id);
        if (!imageExists)
        {
            return NotFound();
        }

        ViewBag.ImageId = id;
        return View();
    }
}
