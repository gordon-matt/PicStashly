using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace PicStashly.Services;

/// <summary>
/// 64-bit difference hash (dHash): downscale to 9×8 grayscale and set one bit per pixel
/// depending on whether it is brighter than its right-hand neighbour. Robust against
/// re-encoding, scaling and small edits; compared via Hamming distance.
/// </summary>
public class PerceptualHashService : IPerceptualHashService
{
    private const int HashWidth = 9;
    private const int HashHeight = 8;

    public async Task<long> ComputeHashAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        using var image = await ImageSharpImage.LoadAsync<L8>(fullPath, cancellationToken);

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Stretch,
            Size = new Size(HashWidth, HashHeight)
        }));

        ulong hash = 0;
        int bit = 0;

        for (int y = 0; y < HashHeight; y++)
        {
            for (int x = 0; x < HashWidth - 1; x++)
            {
                if (image[x, y].PackedValue > image[x + 1, y].PackedValue)
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return unchecked((long)hash);
    }

    public int HammingDistance(long hashA, long hashB)
        => BitOperations.PopCount(unchecked((ulong)(hashA ^ hashB)));
}
