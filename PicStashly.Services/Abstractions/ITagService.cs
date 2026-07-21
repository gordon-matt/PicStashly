namespace PicStashly.Services.Abstractions;

public interface ITagService
{
    Task<Result<List<TagItem>>> GetTagsAsync();

    Task<Result<TagItem>> RenameTagAsync(int tagId, RenameTagRequest request);

    Task<Result> MergeTagsAsync(MergeTagsRequest request);

    Task<Result> DeleteTagAsync(int tagId);
}
