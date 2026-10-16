using Microsoft.Extensions.Options;

namespace PicStashly.Services;

public class ImageService(
    ILogger<ImageService> logger,
    IOptions<UploadOptions> uploadOptions,
    IHttpClientFactory httpClientFactory,
    IUserContextService userContext,
    IFileStorageService fileStorage,
    IPerceptualHashService perceptualHash,
    IOcrService ocr,
    IRepository<Image> imageRepository,
    IRepository<Tag> tagRepository,
    IRepository<ImageTag> imageTagRepository,
    IRepository<Category> categoryRepository) : IImageService
{
    public async Task<Result<ImageSearchResponse>> SearchAsync(ImageSearchRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            int page = Math.Max(1, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 200);

            var predicate = BuildSearchPredicate(request, userId);

            if (request.Sort.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                return await SearchShuffledAsync(predicate, page, pageSize, request.ShuffleSeed);
            }

            var options = new SearchOptions<Image>
            {
                Query = predicate,

                Include = query => query
                    .Include(i => i.Category)
                    .Include(i => i.ImageTags)
                        .ThenInclude(it => it.Tag),

                OrderBy = request.Sort.Equals("oldest", StringComparison.OrdinalIgnoreCase)
                    ? query => query.OrderBy(i => i.DateAdded).ThenBy(i => i.Id)
                    : query => query.OrderByDescending(i => i.DateAdded).ThenByDescending(i => i.Id),

                PageNumber = page,
                PageSize = pageSize,
                SplitQuery = true
            };

            var images = await imageRepository.FindAsync(options);
            int totalCount = images.ItemCount;

            return Result.Success(new ImageSearchResponse(
                images.Select(MapToListItem).ToList(),
                totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize),
                page,
                pageSize));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error searching images");
            }

            return Result.Error("An error occurred while searching images");
        }
    }

    public async Task<Result<ImageDetail>> GetByIdAsync(int id)
    {
        try
        {
            var image = await FindWithTagsAsync(id, userContext.GetRequiredUserId());
            return image is null
                ? Result.NotFound("Image not found")
                : Result.Success(MapToDetail(image));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error retrieving image {ImageId}", id);
            }

            return Result.Error("An error occurred while retrieving the image");
        }
    }

    public async Task<Result<IngestResult>> IngestAsync(
        Stream stream,
        string fileName,
        string? sourceUrl,
        List<string>? tagNames,
        int? categoryId,
        bool force,
        CancellationToken cancellationToken = default)
    {
        string userId = userContext.GetRequiredUserId();
        var opts = uploadOptions.Value;

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!opts.AllowedExtensionSet.Contains(extension))
        {
            return Result.Invalid([new ValidationError("FileName",
                $"File type '{extension}' is not allowed. Allowed types: {opts.AllowedExtensions}")]);
        }

        Category? category = null;
        if (categoryId is int wantedCategoryId)
        {
            category = await categoryRepository.FindOneAsync(new SearchOptions<Category>
            {
                Query = c => c.Id == wantedCategoryId && c.UserId == userId
            });
            if (category is null)
            {
                return Result.Invalid([new ValidationError("CategoryId", "Category not found")]);
            }
        }

        string categoryFolder = fileStorage.GetCategoryFolderName(category?.Id, category?.Name);
        string storageFolder = $"{userId}/{categoryFolder}";

        StoredImageFile? stored = null;
        try
        {
            stored = await fileStorage.SaveImageAsync(stream, fileName, storageFolder, cancellationToken);

            if (stored.FileSizeBytes > opts.MaxFileSizeBytes)
            {
                fileStorage.DeleteFile(stored.StoragePath);
                fileStorage.DeleteFile(stored.ThumbnailPath);
                return Result.Invalid([new ValidationError("FileName",
                    $"File size exceeds the maximum allowed size of {opts.MaxFileSizeBytes / 1_048_576.0:F1} MB")]);
            }

            long hash = await perceptualHash.ComputeHashAsync(fileStorage.GetFullPath(stored.StoragePath), cancellationToken);

            if (!force)
            {
                var duplicates = await FindDuplicatesAsync(hash, userId);
                if (duplicates.Count > 0)
                {
                    fileStorage.DeleteFile(stored.StoragePath);
                    fileStorage.DeleteFile(stored.ThumbnailPath);
                    return Result.Success(IngestResult.DuplicateFound(duplicates));
                }
            }

            string? ocrText = await ocr.ExtractTextAsync(fileStorage.GetFullPath(stored.StoragePath), cancellationToken);

            var image = await imageRepository.InsertAsync(new Image
            {
                FileName = fileName,
                StoragePath = stored.StoragePath,
                ThumbnailPath = stored.ThumbnailPath,
                ContentType = stored.ContentType,
                FileSizeBytes = stored.FileSizeBytes,
                Width = stored.Width,
                Height = stored.Height,
                PerceptualHash = hash,
                OcrText = ocrText,
                SourceUrl = sourceUrl,
                DateAdded = DateTime.UtcNow,
                UserId = userId,
                CategoryId = category?.Id
            });

            var normalizedTags = NormalizeTagNames(tagNames);
            if (normalizedTags.Count > 0)
            {
                await AddTagsToImageAsync(image.Id, normalizedTags, userId);
            }

            var saved = await FindWithTagsAsync(image.Id, userId);
            return Result.Success(IngestResult.Saved(MapToDetail(saved!)));
        }
        catch (SixLabors.ImageSharp.ImageFormatException)
        {
            CleanUpStoredFiles(stored);
            return Result.Invalid([new ValidationError("FileName", "The file is not a valid image")]);
        }
        catch (Exception ex)
        {
            CleanUpStoredFiles(stored);

            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error ingesting image '{FileName}'", fileName);
            }

            return Result.Error("An error occurred while saving the image");
        }
    }

    public async Task<Result<IngestResult>> IngestFromUrlAsync(CaptureRequest request, bool force, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl) ||
            !Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var imageUri) ||
            (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Invalid([new ValidationError("ImageUrl", "A valid absolute image URL is required")]);
        }

        try
        {
            var client = httpClientFactory.CreateClient("capture");
            using var response = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Invalid([new ValidationError("ImageUrl",
                    $"Could not download image (HTTP {(int)response.StatusCode})")]);
            }

            string fileName = Path.GetFileName(imageUri.LocalPath);
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                string? contentType = response.Content.Headers.ContentType?.MediaType;
                string extension = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    "image/bmp" => ".bmp",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(extension))
                {
                    return Result.Invalid([new ValidationError("ImageUrl", "The URL does not point to a supported image type")]);
                }

                fileName = string.IsNullOrEmpty(fileName) ? $"capture{extension}" : $"{fileName}{extension}";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await IngestAsync(stream, fileName, request.PageUrl ?? request.ImageUrl, request.TagNames, request.CategoryId, force, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error capturing image from {ImageUrl}", request.ImageUrl);
            }

            return Result.Error("An error occurred while downloading the image");
        }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var image = await imageRepository.FindOneAsync(new SearchOptions<Image>
            {
                Query = x => x.Id == id && x.UserId == userId
            });
            if (image is null)
            {
                return Result.NotFound("Image not found");
            }

            await imageTagRepository.DeleteAsync(x => x.ImageId == id);
            await imageRepository.DeleteAsync(image);

            fileStorage.DeleteFile(image.StoragePath);
            fileStorage.DeleteFile(image.ThumbnailPath);

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error deleting image {ImageId}", id);
            }

            return Result.Error("An error occurred while deleting the image");
        }
    }

    public async Task<Result<List<string>>> SetTagsAsync(int id, SetImageTagsRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            bool imageExists = await imageRepository.ExistsAsync(x => x.Id == id && x.UserId == userId);
            if (!imageExists)
            {
                return Result.NotFound("Image not found");
            }

            var selectedTagNames = NormalizeTagNames(request.TagNames);

            var existingImageTags = await imageTagRepository.FindAsync(new SearchOptions<ImageTag>
            {
                Query = x => x.ImageId == id,
                Include = query => query.Include(x => x.Tag)
            });

            var toDelete = existingImageTags
                .Where(it => !selectedTagNames.Contains(it.Tag.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var existingNames = existingImageTags.Select(it => it.Tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = selectedTagNames.Where(n => !existingNames.Contains(n)).ToList();

            if (toDelete.Count > 0)
            {
                await imageTagRepository.DeleteAsync(toDelete);
            }

            if (toAdd.Count > 0)
            {
                await AddTagsToImageAsync(id, toAdd, userId);
            }

            return Result.Success(selectedTagNames);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error setting tags for image {ImageId}", id);
            }

            return Result.Error("An error occurred while updating tags");
        }
    }

    public async Task<Result<ImageDetail>> SetCategoryAsync(int id, SetImageCategoryRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var image = await imageRepository.FindOneAsync(new SearchOptions<Image>
            {
                Query = x => x.Id == id && x.UserId == userId
            });
            if (image is null)
            {
                return Result.NotFound("Image not found");
            }

            Category? category = null;
            if (request.CategoryId is int wantedCategoryId)
            {
                category = await categoryRepository.FindOneAsync(new SearchOptions<Category>
                {
                    Query = c => c.Id == wantedCategoryId && c.UserId == userId
                });
                if (category is null)
                {
                    return Result.Invalid([new ValidationError("CategoryId", "Category not found")]);
                }
            }

            string newCategoryFolder = fileStorage.GetCategoryFolderName(category?.Id, category?.Name);
            string newStorageFolder = $"{userId}/{newCategoryFolder}";
            var (newStoragePath, newThumbnailPath) = await fileStorage.MoveToCategoryAsync(
                image.StoragePath, image.ThumbnailPath, newStorageFolder);

            image.CategoryId = category?.Id;
            image.StoragePath = newStoragePath;
            image.ThumbnailPath = newThumbnailPath;
            await imageRepository.UpdateAsync(image);

            var saved = await FindWithTagsAsync(id, userId);
            return Result.Success(MapToDetail(saved!));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error setting category for image {ImageId}", id);
            }

            return Result.Error("An error occurred while updating the category");
        }
    }

    public async Task<Result> UpdateOcrTextAsync(int id, UpdateOcrTextRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var image = await imageRepository.FindOneAsync(new SearchOptions<Image>
            {
                Query = x => x.Id == id && x.UserId == userId
            });
            if (image is null)
            {
                return Result.NotFound("Image not found");
            }

            image.OcrText = string.IsNullOrWhiteSpace(request.OcrText) ? null : request.OcrText.Trim();
            await imageRepository.UpdateAsync(image);

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error updating OCR text for image {ImageId}", id);
            }

            return Result.Error("An error occurred while updating the text");
        }
    }

    public async Task<Result<ImageDetail>> RenameAsync(int id, RenameImageRequest request)
    {
        try
        {
            var image = await FindWithTagsAsync(id, userContext.GetRequiredUserId());
            if (image is null)
            {
                return Result.NotFound("Image not found");
            }

            // Strip any path segments the client may have pasted; FileName is display-only.
            string name = Path.GetFileName(request.FileName?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Invalid([new ValidationError(nameof(request.FileName), "File name is required.")]);
            }

            if (name.Length > 512)
            {
                return Result.Invalid([new ValidationError(nameof(request.FileName), "File name must be 512 characters or fewer.")]);
            }

            // Keep the original extension when the user renames without one (helps downloads).
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                string existingExt = Path.GetExtension(image.FileName);
                if (!string.IsNullOrEmpty(existingExt))
                {
                    name += existingExt;
                }
            }

            image.FileName = name;
            await imageRepository.UpdateAsync(image);

            return Result.Success(MapToDetail(image));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error renaming image {ImageId}", id);
            }

            return Result.Error("An error occurred while renaming the image");
        }
    }

    public async Task<Result> BulkTagAsync(BulkTagRequest request)
    {
        try
        {
            string userId = userContext.GetRequiredUserId();

            var requestedImageIds = request.ImageIds?.Distinct().ToList() ?? [];
            var tagNames = NormalizeTagNames(request.TagNames);

            if (requestedImageIds.Count == 0 || tagNames.Count == 0)
            {
                return Result.Invalid([new ValidationError("ImageIds", "At least one image and one tag are required")]);
            }

            // Only ever touch images owned by the current user, even if extra/foreign ids were sent.
            var imageIds = (await imageRepository.FindAsync(
                new SearchOptions<Image> { Query = x => requestedImageIds.Contains(x.Id) && x.UserId == userId },
                x => x.Id)).ToList();

            if (imageIds.Count == 0)
            {
                return Result.Invalid([new ValidationError("ImageIds", "At least one image and one tag are required")]);
            }

            if (request.Remove)
            {
                var tagIds = (await tagRepository.FindAsync(
                    new SearchOptions<Tag> { Query = x => tagNames.Contains(x.Name) && x.UserId == userId },
                    x => x.Id)).ToList();

                if (tagIds.Count > 0)
                {
                    await imageTagRepository.DeleteAsync(x =>
                        imageIds.Contains(x.ImageId) && tagIds.Contains(x.TagId));
                }

                return Result.Success();
            }

            var tagByName = await EnsureTagsAsync(tagNames, userId);

            var existingLinks = await imageTagRepository.FindAsync(new SearchOptions<ImageTag>
            {
                Query = x => imageIds.Contains(x.ImageId) && tagByName.Values.Select(t => t.Id).Contains(x.TagId)
            });

            var existingPairs = existingLinks.Select(x => (x.ImageId, x.TagId)).ToHashSet();

            var newLinks = imageIds
                .SelectMany(imageId => tagByName.Values.Select(tag => new ImageTag { ImageId = imageId, TagId = tag.Id }))
                .Where(link => !existingPairs.Contains((link.ImageId, link.TagId)))
                .ToList();

            if (newLinks.Count > 0)
            {
                await imageTagRepository.InsertAsync(newLinks);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error bulk-tagging images");
            }

            return Result.Error("An error occurred while applying tags");
        }
    }

    private static System.Linq.Expressions.Expression<Func<Image, bool>> BuildSearchPredicate(ImageSearchRequest request, string userId)
    {
        var predicate = PredicateBuilder.New<Image>(i => i.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToLower();
            predicate = predicate.And(i =>
                i.FileName.ToLower().Contains(search) ||
                (i.OcrText != null && i.OcrText.ToLower().Contains(search)) ||
                i.ImageTags.Any(it => it.Tag.Name.Contains(search)));
        }

        var tagNames = NormalizeTagNames(request.Tags?.Split(',').ToList());
        if (tagNames.Count > 0)
        {
            // Every requested tag must be present on the image.
            predicate = predicate.And(i =>
                i.ImageTags
                    .Where(it => tagNames.Contains(it.Tag.Name))
                    .Select(it => it.TagId)
                    .Distinct()
                    .Count() == tagNames.Count);
        }

        if (request.CategoryId is int categoryId)
        {
            predicate = predicate.And(i => i.CategoryId == categoryId);
        }

        if (request.UntaggedOnly)
        {
            predicate = predicate.And(i => !i.ImageTags.Any());
        }

        if (request.DateFrom is DateTime from)
        {
            predicate = predicate.And(i => i.DateAdded >= from);
        }

        if (request.DateTo is DateTime to)
        {
            var toExclusive = to.Date.AddDays(1);
            predicate = predicate.And(i => i.DateAdded < toExclusive);
        }

        return predicate;
    }

    /// <summary>
    /// Random shuffle sort: fetch matching ids, shuffle deterministically with the client's seed
    /// (so infinite scroll pages stay consistent), then load the requested page by id.
    /// </summary>
    private async Task<Result<ImageSearchResponse>> SearchShuffledAsync(
        System.Linq.Expressions.Expression<Func<Image, bool>> predicate,
        int page,
        int pageSize,
        int seed)
    {
        var allIds = (await imageRepository.FindAsync(
            new SearchOptions<Image> { Query = predicate },
            x => x.Id)).ToList();

        var random = new Random(seed);
        for (int i = allIds.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (allIds[i], allIds[j]) = (allIds[j], allIds[i]);
        }

        var pageIds = allIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var images = await imageRepository.FindAsync(new SearchOptions<Image>
        {
            Query = i => pageIds.Contains(i.Id),

            Include = query => query
                .Include(i => i.Category)
                .Include(i => i.ImageTags)
                    .ThenInclude(it => it.Tag),

            SplitQuery = true
        });

        var byId = images.ToDictionary(i => i.Id);
        var ordered = pageIds.Where(byId.ContainsKey).Select(id => MapToListItem(byId[id])).ToList();

        return Result.Success(new ImageSearchResponse(
            ordered,
            allIds.Count,
            (int)Math.Ceiling(allIds.Count / (double)pageSize),
            page,
            pageSize));
    }

    private async Task<List<DuplicateMatch>> FindDuplicatesAsync(long hash, string userId)
    {
        int threshold = uploadOptions.Value.DuplicateHammingThreshold;

        // pHash comparison has to happen in memory (no SQL popcount), but the projection is tiny.
        var candidates = await imageRepository.FindAsync(
            new SearchOptions<Image> { Query = x => x.UserId == userId },
            x => new { x.Id, x.PerceptualHash });

        var matchDistances = candidates
            .Select(c => new { c.Id, Distance = perceptualHash.HammingDistance(hash, c.PerceptualHash) })
            .Where(c => c.Distance <= threshold)
            .OrderBy(c => c.Distance)
            .Take(5)
            .ToDictionary(c => c.Id, c => c.Distance);

        if (matchDistances.Count == 0)
        {
            return [];
        }

        var matchIds = matchDistances.Keys.ToList();
        var matches = await imageRepository.FindAsync(new SearchOptions<Image>
        {
            Query = i => matchIds.Contains(i.Id),

            Include = query => query
                .Include(i => i.ImageTags)
                    .ThenInclude(it => it.Tag)
        });

        return matches
            .Select(i => new DuplicateMatch(
                i.Id,
                i.FileName,
                GetMediaUrl(i.StoragePath),
                GetMediaUrl(i.ThumbnailPath),
                matchDistances[i.Id],
                GetTagNames(i)))
            .OrderBy(m => m.Distance)
            .ToList();
    }

    private async Task AddTagsToImageAsync(int imageId, List<string> tagNames, string userId)
    {
        var tagByName = await EnsureTagsAsync(tagNames, userId);
        await imageTagRepository.InsertAsync(
            tagNames.Select(name => new ImageTag { ImageId = imageId, TagId = tagByName[name].Id }));
    }

    private async Task<Dictionary<string, Tag>> EnsureTagsAsync(List<string> normalizedNames, string userId)
    {
        var result = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        if (normalizedNames.Count == 0)
        {
            return result;
        }

        var existing = await tagRepository.FindAsync(new SearchOptions<Tag>
        {
            Query = x => normalizedNames.Contains(x.Name) && x.UserId == userId
        });

        foreach (var tag in existing)
        {
            result[tag.Name] = tag;
        }

        var missing = normalizedNames.Where(n => !result.ContainsKey(n)).ToList();
        if (missing.Count > 0)
        {
            var newTags = missing.Select(n => new Tag { Name = n, UserId = userId }).ToList();
            await tagRepository.InsertAsync(newTags);
            foreach (var tag in newTags)
            {
                result[tag.Name] = tag;
            }
        }

        return result;
    }

    private void CleanUpStoredFiles(StoredImageFile? stored)
    {
        if (stored is null)
        {
            return;
        }

        try
        {
            fileStorage.DeleteFile(stored.StoragePath);
            fileStorage.DeleteFile(stored.ThumbnailPath);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private async Task<Image?> FindWithTagsAsync(int id, string userId) => await imageRepository.FindOneAsync(new SearchOptions<Image>
    {
        Query = x => x.Id == id && x.UserId == userId,

        Include = query => query
            .Include(i => i.Category)
            .Include(i => i.ImageTags)
                .ThenInclude(it => it.Tag)
    });

    internal static List<string> NormalizeTagNames(List<string>? tagNames) => tagNames
            ?.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    internal static string GetMediaUrl(string relativePath) => $"{Constants.MediaRequestPath}/{relativePath}";

    private static List<string> GetTagNames(Image image) => image.ImageTags.Select(it => it.Tag.Name).OrderBy(n => n).ToList();

    internal static ImageListItem MapToListItem(Image image) => new(
        image.Id,
        image.FileName,
        GetMediaUrl(image.StoragePath),
        GetMediaUrl(image.ThumbnailPath),
        image.Width,
        image.Height,
        image.DateAdded,
        GetTagNames(image),
        image.CategoryId,
        image.Category?.Name);

    private static ImageDetail MapToDetail(Image image) => new(
        image.Id,
        image.FileName,
        GetMediaUrl(image.StoragePath),
        GetMediaUrl(image.ThumbnailPath),
        image.ContentType,
        image.FileSizeBytes,
        image.Width,
        image.Height,
        image.PerceptualHash.ToString("x16"),
        image.OcrText,
        image.SourceUrl,
        image.DateAdded,
        GetTagNames(image),
        image.CategoryId,
        image.Category?.Name);
}