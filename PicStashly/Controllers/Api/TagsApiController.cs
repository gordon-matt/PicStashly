namespace PicStashly.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/tags")]
[TranslateResultToActionResult]
public class TagsApiController : ControllerBase
{
    private readonly ITagService tagService;

    public TagsApiController(ITagService tagService)
    {
        this.tagService = tagService;
    }

    [HttpGet]
    public Task<Result<List<TagItem>>> GetTags() => tagService.GetTagsAsync();

    [HttpPut("{id:int}")]
    public Task<Result<TagItem>> Rename(int id, [FromBody] RenameTagRequest request) => tagService.RenameTagAsync(id, request);

    [HttpPost("merge")]
    public Task<Result> Merge([FromBody] MergeTagsRequest request) => tagService.MergeTagsAsync(request);

    [HttpDelete("{id:int}")]
    public Task<Result> Delete(int id) => tagService.DeleteTagAsync(id);
}