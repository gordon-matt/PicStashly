namespace PicStashly.Services;

public class TagService(
    ILogger<TagService> logger,
    IUserContextService userContext,
    IRepository<Tag> tagRepository,
    IRepository<ImageTag> imageTagRepository) : ITagService
{
    public async Task<Result<List<TagItem>>> GetTagsAsync()
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var tags = await tagRepository.FindAsync(new SearchOptions<Tag>
            {
                Query = t => t.UserId == userId,
                OrderBy = query => query.OrderBy(t => t.Name)
            }, t => new TagItem(t.Id, t.Name, t.ImageTags.Count));

            var items = tags.ToList();

            return Result.Success(items);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error retrieving tags");
            }

            return Result.Error("An error occurred while retrieving tags");
        }
    }

    public async Task<Result<TagItem>> RenameTagAsync(int tagId, RenameTagRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            string name = request.Name?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Invalid([new ValidationError("Name", "Tag name cannot be empty")]);
            }

            var tag = await tagRepository.FindOneAsync(new SearchOptions<Tag>
            {
                Query = t => t.Id == tagId && t.UserId == userId
            });
            if (tag is null)
            {
                return Result.NotFound("Tag not found");
            }

            bool nameTaken = await tagRepository.ExistsAsync(x => x.UserId == userId && x.Id != tagId && x.Name == name);
            if (nameTaken)
            {
                return Result.Invalid([new ValidationError("Name",
                    "A tag with this name already exists — use merge instead")]);
            }

            tag.Name = name;
            await tagRepository.UpdateAsync(tag);

            int usageCount = await imageTagRepository.CountAsync(x => x.TagId == tagId);
            return Result.Success(new TagItem(tag.Id, tag.Name, usageCount));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error renaming tag {TagId}", tagId);
            }

            return Result.Error("An error occurred while renaming the tag");
        }
    }

    public async Task<Result> MergeTagsAsync(MergeTagsRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var sourceIds = request.SourceTagIds
                ?.Where(id => id != request.TargetTagId)
                .Distinct()
                .ToList() ?? [];

            if (sourceIds.Count == 0)
            {
                return Result.Invalid([new ValidationError("SourceTagIds", "At least one source tag is required")]);
            }

            var target = await tagRepository.FindOneAsync(new SearchOptions<Tag>
            {
                Query = t => t.Id == request.TargetTagId && t.UserId == userId
            });
            if (target is null)
            {
                return Result.NotFound("Target tag not found");
            }

            // Restrict the merge to tags owned by the current user, even if extra/foreign ids were sent.
            var ownedSourceIds = (await tagRepository.FindAsync(
                new SearchOptions<Tag> { Query = t => sourceIds.Contains(t.Id) && t.UserId == userId },
                t => t.Id)).ToList();

            if (ownedSourceIds.Count == 0)
            {
                return Result.Invalid([new ValidationError("SourceTagIds", "At least one source tag is required")]);
            }

            var sourceLinks = await imageTagRepository.FindAsync(new SearchOptions<ImageTag>
            {
                Query = x => ownedSourceIds.Contains(x.TagId)
            });

            var imageIdsAlreadyLinkedToTarget = (await imageTagRepository.FindAsync(
                new SearchOptions<ImageTag> { Query = x => x.TagId == request.TargetTagId },
                x => x.ImageId)).ToHashSet();

            // Re-point links to the target tag, skipping images already linked to it.
            var newLinks = sourceLinks
                .Select(link => link.ImageId)
                .Distinct()
                .Where(imageId => !imageIdsAlreadyLinkedToTarget.Contains(imageId))
                .Select(imageId => new ImageTag { ImageId = imageId, TagId = request.TargetTagId })
                .ToList();

            if (newLinks.Count > 0)
            {
                await imageTagRepository.InsertAsync(newLinks);
            }

            await imageTagRepository.DeleteAsync(x => ownedSourceIds.Contains(x.TagId));
            await tagRepository.DeleteAsync(x => ownedSourceIds.Contains(x.Id));

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error merging tags into {TargetTagId}", request.TargetTagId);
            }

            return Result.Error("An error occurred while merging tags");
        }
    }

    public async Task<Result> DeleteTagAsync(int tagId)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var tag = await tagRepository.FindOneAsync(new SearchOptions<Tag>
            {
                Query = t => t.Id == tagId && t.UserId == userId
            });
            if (tag is null)
            {
                return Result.NotFound("Tag not found");
            }

            await imageTagRepository.DeleteAsync(x => x.TagId == tagId);
            await tagRepository.DeleteAsync(tag);

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error deleting tag {TagId}", tagId);
            }

            return Result.Error("An error occurred while deleting the tag");
        }
    }
}
