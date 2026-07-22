namespace PicStashly.Services;

public class AdminService(
    ILogger<AdminService> logger,
    IFileStorageService fileStorage,
    IPerceptualHashService perceptualHash,
    IOcrService ocr,
    IRepository<Image> imageRepository,
    IRepository<Tag> tagRepository) : IAdminService
{
    public async Task<Result<StorageStats>> GetStorageStatsAsync()
    {
        try
        {
            // Tiny projection; aggregating in memory keeps this portable across all four providers.
            var summaries = (await imageRepository.FindAsync(
                new SearchOptions<Image>(),
                i => new { i.FileSizeBytes, i.DateAdded, IsUntagged = !i.ImageTags.Any() })).ToList();

            int tagCount = await tagRepository.CountAsync();

            return Result.Success(new StorageStats(
                summaries.Count,
                summaries.Sum(s => s.FileSizeBytes),
                tagCount,
                summaries.Count(s => s.IsUntagged),
                summaries.Count > 0 ? summaries.Min(s => s.DateAdded) : null,
                summaries.Count > 0 ? summaries.Max(s => s.DateAdded) : null));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error retrieving storage stats");
            }

            return Result.Error("An error occurred while retrieving storage stats");
        }
    }

    public async Task<Result<OrphanCheckResult>> CheckOrphansAsync()
    {
        try
        {
            var images = await imageRepository.FindAsync(new SearchOptions<Image>
            {
                Include = query => query
                    .Include(i => i.ImageTags)
                        .ThenInclude(it => it.Tag)
            });

            var knownPaths = images.Select(i => i.StoragePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var orphanedFiles = fileStorage.EnumerateImageFiles()
                .Where(path => !knownPaths.Contains(path))
                .OrderBy(path => path)
                .ToList();

            var brokenImages = images
                .Where(i => !File.Exists(fileStorage.GetFullPath(i.StoragePath)))
                .Select(ImageService.MapToListItem)
                .ToList();

            return Result.Success(new OrphanCheckResult(orphanedFiles, brokenImages));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error checking for orphaned files");
            }

            return Result.Error("An error occurred while checking for orphans");
        }
    }

    public async Task<Result<MaintenanceResult>> RehashAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var images = await imageRepository.FindAsync(new SearchOptions<Image>());
            int processed = 0, failed = 0;

            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string fullPath = fileStorage.GetFullPath(image.StoragePath);
                    if (!File.Exists(fullPath))
                    {
                        failed++;
                        continue;
                    }

                    image.PerceptualHash = await perceptualHash.ComputeHashAsync(fullPath, cancellationToken);
                    await imageRepository.UpdateAsync(image);
                    processed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;

                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(ex, "Failed to rehash image {ImageId}", image.Id);
                    }
                }
            }

            return Result.Success(new MaintenanceResult(processed, failed));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error rehashing images");
            }

            return Result.Error("An error occurred while rehashing images");
        }
    }

    public async Task<Result<MaintenanceResult>> ReRunOcrAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var images = await imageRepository.FindAsync(new SearchOptions<Image>());
            int processed = 0, failed = 0;

            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string fullPath = fileStorage.GetFullPath(image.StoragePath);
                    if (!File.Exists(fullPath))
                    {
                        failed++;
                        continue;
                    }

                    image.OcrText = await ocr.ExtractTextAsync(fullPath, cancellationToken);
                    await imageRepository.UpdateAsync(image);
                    processed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;

                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(ex, "Failed to re-run OCR for image {ImageId}", image.Id);
                    }
                }
            }

            return Result.Success(new MaintenanceResult(processed, failed));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error re-running OCR");
            }

            return Result.Error("An error occurred while re-running OCR");
        }
    }
}