namespace PicStashly.Services.Abstractions;

public interface IAdminService
{
    Task<Result<StorageStats>> GetStorageStatsAsync();

    /// <summary>Finds files on disk with no DB record and DB records whose file is missing on disk.</summary>
    Task<Result<OrphanCheckResult>> CheckOrphansAsync();

    /// <summary>Recomputes the perceptual hash for every image (for when the pipeline changes).</summary>
    Task<Result<MaintenanceResult>> RehashAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-runs OCR for every image, overwriting stored text.</summary>
    Task<Result<MaintenanceResult>> ReRunOcrAsync(CancellationToken cancellationToken = default);
}
