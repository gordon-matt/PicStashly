namespace PicStashly.Controllers.Api;

[ApiController]
[Route("api/tags")]
public class TagsApiController : ControllerBase
{
    private readonly ITagService tagService;

    public TagsApiController(ITagService tagService)
    {
        this.tagService = tagService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTags()
    {
        var result = await tagService.GetTagsAsync();
        return result.ToActionResult(this, value => Ok(new { tags = value }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameTagRequest request)
    {
        var result = await tagService.RenameTagAsync(id, request);
        return result.ToActionResult(this, value => Ok(value));
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeTagsRequest request)
    {
        var result = await tagService.MergeTagsAsync(request);
        return result.ToActionResult(this, () => Ok(new { message = "Tags merged." }));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await tagService.DeleteTagAsync(id);
        return result.ToActionResult(this, () => Ok(new { message = "Tag deleted." }));
    }
}
