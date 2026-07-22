namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/categories")]
[TranslateResultToActionResult]
public class CategoriesApiController : ControllerBase
{
    private readonly ICategoryService categoryService;

    public CategoriesApiController(ICategoryService categoryService)
    {
        this.categoryService = categoryService;
    }

    [HttpGet]
    public Task<Result<List<CategoryItem>>> GetCategories() => categoryService.GetCategoriesAsync();

    [HttpPost]
    public Task<Result<CategoryItem>> Create([FromBody] CreateCategoryRequest request) =>
        categoryService.CreateCategoryAsync(request);

    [HttpPut("{id:int}")]
    public Task<Result<CategoryItem>> Rename(int id, [FromBody] RenameCategoryRequest request) =>
        categoryService.RenameCategoryAsync(id, request);

    [HttpDelete("{id:int}")]
    public Task<Result> Delete(int id) => categoryService.DeleteCategoryAsync(id);
}
