namespace PicStashly.Services.Abstractions;

public interface IPerceptualHashService
{
    /// <summary>Computes a 64-bit difference hash (dHash) of the image at the given path.</summary>
    Task<long> ComputeHashAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>Number of differing bits between two hashes (0 = identical, 64 = completely different).</summary>
    int HammingDistance(long hashA, long hashB);
}