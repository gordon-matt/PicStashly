using Heroicsoft.NetForge.Drawing;

namespace PicStashly.Services;

/// <summary>
/// 64-bit difference hash (dHash): downscale to 9×8 grayscale and set one bit per pixel
/// depending on whether it is brighter than its right-hand neighbour. Robust against
/// re-encoding, scaling and small edits; compared via Hamming distance.
/// </summary>
public class PerceptualHashService(IImageProcessor images) : IPerceptualHashService
{
    public Task<long> ComputeHashAsync(string fullPath, CancellationToken cancellationToken = default) =>
        images.ComputeDifferenceHashAsync(fullPath, cancellationToken);

    public int HammingDistance(long hashA, long hashB) => images.HammingDistance(hashA, hashB);
}
